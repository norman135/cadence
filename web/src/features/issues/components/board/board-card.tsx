import { ListChecks, MessageSquare } from 'lucide-react';
import { memo } from 'react';
import { Link } from 'react-router';
import type { BoardCard as BoardCardModel, IssueStatus } from '@/shared/api/generated/model';
import { cn } from '@/shared/lib/utils';
import { Avatar } from '@/shared/ui/avatar';
import { Badge } from '@/shared/ui/badge';
import { PriorityIcon, StatusIcon } from '@/shared/ui/work-glyphs';
import {
  dueTone,
  formatDate,
  parseDueDate,
  priorityOf,
  statusOf,
} from '@/features/issues/lib/options';
import { LabelChip } from '@/features/issues/components/pickers';

/**
 * A card (design board 07): priority, key and assignee on the first line, then the title, then meta
 * that only appears when it matters. Closed cards are quieter. Memoized, so dragging one card on a
 * full board re-renders only the cards that move.
 */
export const BoardCardView = memo(function BoardCardView({
  card,
  category,
  href,
  lifted,
  activatorRef,
  activatorProps,
}: {
  card: BoardCardModel;
  category: IssueStatus;
  /** Opens the issue over the board. */
  href: string;
  /** Picked up: shown in the drag overlay. */
  lifted?: boolean;
  /** The title link doubles as the drag handle: Space picks the card up, Enter opens it. */
  activatorRef?: ((element: HTMLElement | null) => void) | undefined;
  activatorProps?: Record<string, unknown> | undefined;
}) {
  const closed = category === 'Done' || category === 'Canceled';
  const meta =
    card.labels.length > 0 ||
    card.estimate != null ||
    card.subIssueCount > 0 ||
    card.commentCount > 0 ||
    (card.dueDate != null && !closed);

  return (
    <div
      className={cn(
        'relative rounded-lg border bg-card p-3 text-left shadow-xs transition-shadow duration-[120ms] hover:border-input',
        lifted && 'rotate-1 border-primary shadow-lg',
      )}
    >
      <div className="flex items-center gap-2">
        {closed ? (
          <StatusIcon category={statusOf(category).glyph} />
        ) : (
          <PriorityIcon priority={priorityOf(card.priority).glyph} />
        )}
        <span className="font-mono text-xs text-muted-foreground tabular-nums">{card.key}</span>
        <span className="flex-1" />
        {card.assignee ? (
          <Avatar name={card.assignee.displayName} className="size-5 text-[9px]" />
        ) : (
          <span
            role="img"
            aria-label="Unassigned"
            className="inline-block size-5 rounded-full border border-dashed border-input"
          />
        )}
      </div>
      <Link
        ref={activatorRef}
        {...activatorProps}
        to={href}
        draggable={false}
        className={cn(
          'mt-1.5 line-clamp-3 block text-sm leading-5 font-medium outline-none after:absolute after:inset-0 focus-visible:underline',
          closed && 'font-normal text-muted-foreground',
        )}
      >
        {card.title}
      </Link>
      {meta && (
        <div className="mt-2 flex flex-wrap items-center gap-1.5 text-xs text-muted-foreground">
          {card.labels.slice(0, 2).map((label) => (
            <LabelChip key={label.id} label={label} />
          ))}
          {card.estimate != null && (
            <span className="rounded border px-1 font-mono text-[11px]" title="Estimate">
              {card.estimate}
            </span>
          )}
          {card.subIssueCount > 0 && (
            <span className="flex items-center gap-1" title="Sub-issues done">
              <ListChecks className="size-3.5" aria-hidden="true" />
              {card.subIssueClosedCount}/{card.subIssueCount}
            </span>
          )}
          {card.commentCount > 0 && (
            <span className="flex items-center gap-1" title="Comments">
              <MessageSquare className="size-3.5" aria-hidden="true" />
              {card.commentCount}
            </span>
          )}
          {card.dueDate != null && !closed && (
            <Badge variant={dueTone(card.dueDate)} className="h-5">
              {formatDate(parseDueDate(card.dueDate))}
            </Badge>
          )}
        </div>
      )}
    </div>
  );
});
