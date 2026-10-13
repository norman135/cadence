import { Rows3 } from 'lucide-react';
import { lazy, Suspense, useState } from 'react';
import { useSearchParams } from 'react-router';
import type { GetBoardParams } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from '@/shared/ui/dropdown-menu';
import { Skeleton } from '@/shared/ui/skeleton';
import { useCurrentOrganization, useCurrentProject, useWorkflow } from '@/shared/workspace';
import type { GroupBy } from '../components/board/board';
import { useBoard, useMoveCardOnBoard } from '../components/board/use-board';
import { FilterBar } from '../components/filter-bar';
import { IssueDrawer } from '../components/issue-drawer';
import { ProjectHeader } from '../components/project-header';
import { useIssueFilters, type IssueFilters } from '../lib/filters';

const CreateIssueDialog = lazy(() => import('../dialogs/create-issue-dialog'));

// The board and dnd-kit are their own chunk, requested as soon as this page's code runs, so they
// download alongside the board's data rather than after it.
const loadBoard = () => import('../components/board/board');
void loadBoard();
const Board = lazy(() => loadBoard().then((module) => ({ default: module.Board })));

const GROUPS: readonly { value: GroupBy; label: string }[] = [
  { value: 'none', label: 'No grouping' },
  { value: 'assignee', label: 'Assignee' },
  { value: 'priority', label: 'Priority' },
  { value: 'epic', label: 'Epic' },
];

/** The board's quick filters, from the same URL filters as the list. */
function toBoardParams(filters: IssueFilters): GetBoardParams {
  const assignees = filters.assignee.filter((id) => id !== 'none');
  return {
    ...(assignees.length ? { assigneeId: assignees } : {}),
    ...(filters.assignee.includes('none') ? { unassigned: true } : {}),
    ...(filters.type.length ? { type: filters.type } : {}),
    ...(filters.priority.length ? { priority: filters.priority } : {}),
    ...(filters.label.length ? { labelId: filters.label } : {}),
    ...(filters.q ? { q: filters.q } : {}),
  };
}

/**
 * A project's Kanban board (`/:org/projects/:key/board`, design board 07): a column per workflow
 * status, cards moved by drag and drop, quick filters and swimlanes kept in the URL.
 */
export function BoardPage() {
  const { organization } = useCurrentOrganization();
  const { project, isPending } = useCurrentProject();
  const { filters, update, active } = useIssueFilters();
  const [params, setParams] = useSearchParams();
  const groupBy = GROUPS.find((group) => group.value === params.get('group'))?.value ?? 'none';
  const [creatingIn, setCreatingIn] = useState<string | null | undefined>(undefined);

  const boardParams = toBoardParams(filters);
  const board = useBoard(project?.id, boardParams);
  const { data: workflow } = useWorkflow(project?.id);
  const move = useMoveCardOnBoard(project?.id ?? '', boardParams);

  if (isPending) return null;
  if (!project) {
    return (
      <div className="py-16 text-center">
        <p className="font-semibold">Project not found</p>
        <p className="mt-1 text-sm text-muted-foreground">
          It doesn&apos;t exist, or you don&apos;t have access to it.
        </p>
      </div>
    );
  }

  const canWrite = project.role !== 'Viewer' && project.archivedAt == null;
  const basePath = `/${organization?.slug ?? ''}`;

  return (
    <div className="flex flex-col gap-4">
      <ProjectHeader
        project={project}
        basePath={basePath}
        onNewIssue={
          canWrite
            ? () => {
                setCreatingIn(null);
              }
            : undefined
        }
      />

      <FilterBar
        variant="board"
        filters={filters}
        update={update}
        active={active}
        projectId={project.id}
        actions={
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="sm">
                <Rows3 />
                {groupBy === 'none'
                  ? 'Group by'
                  : `Group by ${GROUPS.find((group) => group.value === groupBy)?.label ?? ''}`}
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuLabel>Swimlanes</DropdownMenuLabel>
              <DropdownMenuRadioGroup
                value={groupBy}
                onValueChange={(value) => {
                  setParams(
                    (current) => {
                      const next = new URLSearchParams(current);
                      if (value === 'none') next.delete('group');
                      else next.set('group', value);
                      return next;
                    },
                    { replace: true },
                  );
                }}
              >
                {GROUPS.map((group) => (
                  <DropdownMenuRadioItem key={group.value} value={group.value}>
                    {group.label}
                  </DropdownMenuRadioItem>
                ))}
              </DropdownMenuRadioGroup>
            </DropdownMenuContent>
          </DropdownMenu>
        }
      />

      {board.data ? (
        <Suspense fallback={<BoardSkeleton />}>
          <Board
            board={board.data}
            workflow={workflow}
            groupBy={groupBy}
            canMove={canWrite}
            listHref={`${basePath}/projects/${project.key}`}
            onMove={move}
            onCreate={
              canWrite
                ? (statusId) => {
                    setCreatingIn(statusId);
                  }
                : undefined
            }
          />
        </Suspense>
      ) : (
        <BoardSkeleton />
      )}

      <IssueDrawer />
      {creatingIn !== undefined && (
        <Suspense fallback={null}>
          <CreateIssueDialog
            open
            projectId={project.id}
            statusId={creatingIn ?? undefined}
            onOpenChange={(open) => {
              if (!open) setCreatingIn(undefined);
            }}
          />
        </Suspense>
      )}
    </div>
  );
}

function BoardSkeleton() {
  return (
    <div className="flex gap-3" aria-busy="true">
      {[0, 1, 2, 3].map((index) => (
        <Skeleton key={index} className="h-64 w-72 shrink-0" />
      ))}
    </div>
  );
}
