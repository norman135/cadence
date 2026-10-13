import {
  closestCorners,
  DndContext,
  DragOverlay,
  KeyboardSensor,
  PointerSensor,
  useDroppable,
  useSensor,
  useSensors,
  type Announcements,
  type DragEndEvent,
  type DragOverEvent,
  type DragStartEvent,
  type UniqueIdentifier,
} from '@dnd-kit/core';
import {
  arrayMove,
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { Plus } from 'lucide-react';
import { memo, useCallback, useMemo, useState, type ReactNode } from 'react';
import { Link, useLocation } from 'react-router';
import { toast } from 'sonner';
import type {
  BoardCard,
  BoardColumn,
  BoardResponse,
  IssuePriority,
  WorkflowResponse,
} from '@/shared/api/generated/model';
import { cn } from '@/shared/lib/utils';
import { Avatar } from '@/shared/ui/avatar';
import { PriorityIcon, StatusIcon } from '@/shared/ui/work-glyphs';
import { PRIORITIES, priorityOf, statusOf } from '@/features/issues/lib/options';
import { BoardCardView } from './board-card';
import { rankForDrop } from './placement';

export type GroupBy = 'none' | 'assignee' | 'priority' | 'epic';

interface Lane {
  id: string;
  title: ReactNode;
  cards: BoardCard[];
}

const SEPARATOR = '|';
const containerId = (laneId: string, statusId: string) => `${laneId}${SEPARATOR}${statusId}`;
const parseContainer = (id: string) => {
  const [laneId = '', statusId = ''] = id.split(SEPARATOR);
  return { laneId, statusId };
};

/** Splits the board's cards into swimlanes; one lane holds everything when not grouped. */
function lanesOf(board: BoardResponse, groupBy: GroupBy): Lane[] {
  const cards = board.columns.flatMap((column) => column.cards);
  if (groupBy === 'none') return [{ id: 'all', title: null, cards }];

  const lanes = new Map<string, Lane>();
  const add = (id: string, title: ReactNode, card: BoardCard) => {
    const lane = lanes.get(id) ?? { id, title, cards: [] };
    lane.cards.push(card);
    lanes.set(id, lane);
  };

  for (const card of cards) {
    if (groupBy === 'assignee') {
      if (card.assignee) {
        add(
          card.assignee.id,
          <>
            <Avatar name={card.assignee.displayName} className="size-5 text-[9px]" />
            {card.assignee.displayName}
          </>,
          card,
        );
      } else {
        add('~none', 'Unassigned', card);
      }
    } else if (groupBy === 'priority') {
      const priority = priorityOf(card.priority);
      add(
        card.priority,
        <>
          <PriorityIcon priority={priority.glyph} />
          {priority.label}
        </>,
        card,
      );
    } else if (card.epic) {
      add(
        card.epic.id,
        <>
          <span className="font-mono text-xs text-muted-foreground">{card.epic.key}</span>
          {card.epic.title}
        </>,
        card,
      );
    } else {
      add('~none', 'No epic', card);
    }
  }

  const order = (lane: Lane) =>
    groupBy === 'priority'
      ? PRIORITIES.findIndex((priority) => priority.value === (lane.id as IssuePriority))
      : lane.id === '~none'
        ? Number.MAX_SAFE_INTEGER
        : 0;
  return [...lanes.values()].sort((a, b) => order(a) - order(b) || a.id.localeCompare(b.id));
}

/** Card ids per container (lane and status), in rank order. */
function itemsOf(board: BoardResponse, lanes: Lane[]) {
  const items: Record<string, string[]> = {};
  for (const lane of lanes) {
    const inLane = new Set(lane.cards.map((card) => card.id));
    for (const column of board.columns) {
      items[containerId(lane.id, column.statusId)] = column.cards
        .filter((card) => inLane.has(card.id))
        .map((card) => card.id);
    }
  }
  return items;
}

function findContainer(items: Record<string, string[]>, id: UniqueIdentifier) {
  const key = String(id);
  if (key in items) return key;
  return Object.keys(items).find((container) => items[container]?.includes(key));
}

/**
 * The Kanban board (design board 07): a column per workflow status, optionally split into
 * swimlanes. Cards move by drag and drop or the keyboard (Space to pick up, arrows, Space to drop).
 * While a card is held, columns its workflow doesn't allow are dimmed and refuse it.
 */
export function Board({
  board,
  workflow,
  groupBy,
  canMove,
  listHref,
  onMove,
  onCreate,
}: {
  board: BoardResponse;
  workflow: WorkflowResponse | undefined;
  groupBy: GroupBy;
  canMove: boolean;
  /** The project's issue list, for columns that show only their first cards. */
  listHref: string;
  onMove: (card: BoardCard, toStatusId: string, rank: string) => Promise<void>;
  onCreate?: ((statusId: string) => void) | undefined;
}) {
  const { search } = useLocation();
  const lanes = useMemo(() => lanesOf(board, groupBy), [board, groupBy]);
  const derived = useMemo(() => itemsOf(board, lanes), [board, lanes]);
  const [dragItems, setDragItems] = useState<Record<string, string[]> | null>(null);
  const [active, setActive] = useState<BoardCard | null>(null);
  const items = dragItems ?? derived;

  const cards = useMemo(
    () => new Map(board.columns.flatMap((column) => column.cards).map((card) => [card.id, card])),
    [board],
  );
  const columns = useMemo(
    () => new Map(board.columns.map((column) => [column.statusId, column])),
    [board],
  );

  const allowedTargets = useMemo(() => {
    if (!active || !workflow?.restrictTransitions) return null;
    return new Set([
      active.statusId,
      ...workflow.transitions
        .filter((transition) => transition.fromStatusId === active.statusId)
        .map((transition) => transition.toStatusId),
    ]);
  }, [active, workflow]);

  const sensors = useSensors(
    // A few pixels of movement before a drag starts, so clicking a card still opens it.
    useSensor(PointerSensor, { activationConstraint: { distance: 5 } }),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
      keyboardCodes: { start: ['Space'], cancel: ['Escape'], end: ['Space', 'Enter'] },
    }),
  );

  // Stable between drag frames, so memoized columns and cards don't re-render.
  const hrefFor = useCallback(
    (key: string) => {
      const params = new URLSearchParams(search);
      params.set('issue', key);
      return `?${params.toString()}`;
    },
    [search],
  );

  const columnName = (id: UniqueIdentifier | undefined) => {
    if (id === undefined) return '';
    const container = findContainer(items, id);
    return container ? (columns.get(parseContainer(container).statusId)?.name ?? '') : '';
  };

  const announcements: Announcements = {
    onDragStart: ({ active: picked }) =>
      `Picked up ${cards.get(String(picked.id))?.key ?? 'card'}.`,
    onDragOver: ({ active: picked, over }) =>
      over
        ? `${cards.get(String(picked.id))?.key ?? 'Card'} is over ${columnName(over.id)}.`
        : undefined,
    onDragEnd: ({ active: picked, over }) =>
      over
        ? `${cards.get(String(picked.id))?.key ?? 'Card'} dropped in ${columnName(over.id)}.`
        : 'Card dropped.',
    onDragCancel: ({ active: picked }) =>
      `Moving ${cards.get(String(picked.id))?.key ?? 'card'} canceled.`,
  };

  const onDragStart = ({ active: picked }: DragStartEvent) => {
    setActive(cards.get(String(picked.id)) ?? null);
    setDragItems(derived);
  };

  const onDragOver = ({ active: picked, over }: DragOverEvent) => {
    if (!over || !dragItems) return;
    const from = findContainer(dragItems, picked.id);
    const to = findContainer(dragItems, over.id);
    if (!from || !to || from === to) return;

    // Cards stay in their swimlane, and only go where the workflow allows.
    const target = parseContainer(to);
    if (parseContainer(from).laneId !== target.laneId) return;
    if (allowedTargets && !allowedTargets.has(target.statusId)) return;

    setDragItems((current) => {
      if (!current) return current;
      const source = (current[from] ?? []).filter((id) => id !== String(picked.id));
      const destination = [...(current[to] ?? [])];
      const overIndex = destination.indexOf(String(over.id));
      destination.splice(overIndex === -1 ? destination.length : overIndex, 0, String(picked.id));
      return { ...current, [from]: source, [to]: destination };
    });
  };

  const finish = () => {
    setActive(null);
    setDragItems(null);
  };

  const onDragEnd = ({ active: picked, over }: DragEndEvent) => {
    const card = cards.get(String(picked.id));
    const container = dragItems ? findContainer(dragItems, picked.id) : undefined;
    if (!card || !over || !dragItems || !container) {
      finish();
      return;
    }

    let lane = dragItems[container] ?? [];
    const overIndex = lane.indexOf(String(over.id));
    const fromIndex = lane.indexOf(card.id);
    if (overIndex !== -1 && overIndex !== fromIndex) lane = arrayMove(lane, fromIndex, overIndex);

    const { statusId } = parseContainer(container);
    const column = columns.get(statusId);
    if (!column) {
      finish();
      return;
    }

    if (allowedTargets && !allowedTargets.has(statusId)) {
      toast.error(`This workflow doesn't allow moving ${card.key} to ${column.name}.`);
      finish();
      return;
    }

    const index = lane.indexOf(card.id);
    const laneCards = lane.filter((id) => id !== card.id).flatMap((id) => cards.get(id) ?? []);
    const columnCards = column.cards.filter((candidate) => candidate.id !== card.id);
    const rank = rankForDrop(columnCards, laneCards, index, column.nextRank);

    if (
      column.statusId !== card.statusId &&
      column.wipLimit != null &&
      column.total + 1 > column.wipLimit
    ) {
      toast.warning(
        `${column.name} is over its WIP limit (${String(column.total + 1)} of ${String(column.wipLimit)}).`,
      );
    }

    finish();
    void onMove(card, statusId, rank);
  };

  const grouped = groupBy !== 'none';

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={closestCorners}
      accessibility={{
        announcements,
        screenReaderInstructions: {
          draggable:
            'To move a card, press Space. Use the arrow keys to move it, then Space to drop it, or Escape to cancel.',
        },
      }}
      onDragStart={onDragStart}
      onDragOver={onDragOver}
      onDragEnd={onDragEnd}
      onDragCancel={finish}
    >
      <div className="overflow-x-auto pb-4">
        <div className="flex w-max flex-col gap-3">
          <div className="flex gap-3">
            {board.columns.map((column) => (
              <ColumnHeader
                key={column.statusId}
                column={column}
                dimmed={!!allowedTargets && !allowedTargets.has(column.statusId)}
                onCreate={onCreate}
              />
            ))}
          </div>
          {lanes.map((lane) => (
            <div key={lane.id}>
              {grouped && (
                <h3 className="sticky left-0 mb-2 flex w-fit items-center gap-2 text-[13px] font-semibold">
                  {lane.title}
                  <span className="font-normal text-muted-foreground tabular-nums">
                    {lane.cards.length}
                  </span>
                </h3>
              )}
              <div className="flex gap-3">
                {board.columns.map((column) => {
                  const id = containerId(lane.id, column.statusId);
                  return (
                    <ColumnCell
                      key={id}
                      id={id}
                      column={column}
                      cardIds={items[id] ?? []}
                      cards={cards}
                      hrefFor={hrefFor}
                      canMove={canMove}
                      dimmed={!!allowedTargets && !allowedTargets.has(column.statusId)}
                      moreHref={grouped ? undefined : `${listHref}?status=${column.category}`}
                    />
                  );
                })}
              </div>
            </div>
          ))}
        </div>
      </div>
      <DragOverlay>
        {active && (
          <BoardCardView
            card={active}
            category={columns.get(active.statusId)?.category ?? 'Todo'}
            href={hrefFor(active.key)}
            lifted
          />
        )}
      </DragOverlay>
    </DndContext>
  );
}

const COLUMN_WIDTH = 'w-72 shrink-0';

function ColumnHeader({
  column,
  dimmed,
  onCreate,
}: {
  column: BoardColumn;
  dimmed: boolean;
  onCreate?: ((statusId: string) => void) | undefined;
}) {
  const atLimit = column.wipLimit != null && column.total >= column.wipLimit;
  return (
    <div
      className={cn(
        COLUMN_WIDTH,
        'flex h-9 items-center gap-2 rounded-lg px-2.5 transition-opacity',
        atLimit && 'bg-warning-soft',
        dimmed && 'opacity-40',
      )}
    >
      <StatusIcon category={statusOf(column.category).glyph} />
      <h2 className="truncate text-sm font-semibold">{column.name}</h2>
      <span className="text-xs text-muted-foreground tabular-nums">{column.total}</span>
      <span className="flex-1" />
      {column.wipLimit != null && (
        <span
          className={cn(
            'font-mono text-xs tabular-nums',
            atLimit ? 'text-warning' : 'text-muted-foreground',
          )}
          title="Work-in-progress limit"
        >
          {column.total} / {column.wipLimit}
        </span>
      )}
      {onCreate && (
        <button
          type="button"
          aria-label={`New issue in ${column.name}`}
          className="rounded p-0.5 text-muted-foreground hover:bg-accent hover:text-foreground"
          onClick={() => {
            onCreate(column.statusId);
          }}
        >
          <Plus className="size-4" />
        </button>
      )}
    </div>
  );
}

const ColumnCell = memo(function ColumnCell({
  id,
  column,
  cardIds,
  cards,
  hrefFor,
  canMove,
  dimmed,
  moreHref,
}: {
  id: string;
  column: BoardColumn;
  cardIds: string[];
  cards: Map<string, BoardCard>;
  hrefFor: (key: string) => string;
  canMove: boolean;
  dimmed: boolean;
  /** Where the rest of a partly shown column is listed; not shown in swimlanes. */
  moreHref: string | undefined;
}) {
  const { setNodeRef, isOver } = useDroppable({ id, disabled: !canMove });
  return (
    <SortableContext id={id} items={cardIds} strategy={verticalListSortingStrategy}>
      <div
        ref={setNodeRef}
        aria-label={`${column.name} cards`}
        role="list"
        className={cn(
          COLUMN_WIDTH,
          'flex min-h-24 flex-col gap-2 rounded-lg bg-sunken/60 p-1.5 transition-[opacity,background-color]',
          isOver && 'bg-primary-soft/40',
          dimmed && 'opacity-40',
        )}
      >
        {cardIds.map((cardId) => {
          const card = cards.get(cardId);
          return card ? (
            <SortableCard
              key={cardId}
              card={card}
              category={column.category}
              href={hrefFor(card.key)}
              disabled={!canMove}
            />
          ) : null;
        })}
        {moreHref && column.hasMore && (
          <Link
            to={moreHref}
            className="px-2 py-1.5 text-xs text-muted-foreground hover:text-foreground"
          >
            Showing {column.cards.length} of {column.total}. See all in the list
          </Link>
        )}
      </div>
    </SortableContext>
  );
});

function SortableCard({
  card,
  category,
  href,
  disabled,
}: {
  card: BoardCard;
  category: BoardColumn['category'];
  href: string;
  disabled: boolean;
}) {
  const {
    setNodeRef,
    setActivatorNodeRef,
    listeners,
    attributes,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: card.id, disabled });
  const describedBy = attributes['aria-describedby'];
  const activatorProps = useMemo(
    () => ({ ...listeners, 'aria-describedby': describedBy }),
    [listeners, describedBy],
  );

  return (
    <div
      ref={setNodeRef}
      role="listitem"
      style={{ transform: CSS.Translate.toString(transform), transition }}
      className={cn(isDragging && 'opacity-40')}
    >
      <BoardCardView
        card={card}
        category={category}
        href={href}
        activatorRef={setActivatorNodeRef}
        activatorProps={activatorProps}
      />
    </div>
  );
}
