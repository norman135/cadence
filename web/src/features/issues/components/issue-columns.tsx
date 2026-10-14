import { MessageSquare } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import type { IssueListItem } from '@/shared/api/generated/model';
import { Avatar } from '@/shared/ui/avatar';
import { Badge } from '@/shared/ui/badge';
import { PriorityIcon, StatusIcon } from '@/shared/ui/work-glyphs';
import {
  dueTone,
  formatDate,
  parseDueDate,
  priorityOf,
  relativeTime,
  statusOf,
} from '../lib/options';
import { LabelChip } from './pickers';

export type ColumnId =
  | 'priority'
  | 'key'
  | 'status'
  | 'title'
  | 'labels'
  | 'estimate'
  | 'due'
  | 'assignee'
  | 'updated'
  | 'created';

export interface Column {
  id: ColumnId;
  label: string;
  /** Always shown; not offered in the column chooser. */
  required?: boolean;
  className: string;
  render: (issue: IssueListItem) => ReactNode;
}

/** The list's columns, in display order. Title takes the remaining width. */
export const COLUMNS: readonly Column[] = [
  {
    id: 'priority',
    label: 'Priority',
    className: 'w-5 shrink-0',
    render: (issue) => <PriorityIcon priority={priorityOf(issue.priority).glyph} />,
  },
  {
    id: 'key',
    label: 'Key',
    className: 'w-[72px] shrink-0 font-mono text-xs text-muted-foreground tabular-nums',
    render: (issue) => issue.key,
  },
  {
    id: 'status',
    label: 'Status',
    className: 'w-4 shrink-0',
    render: (issue) => <StatusIcon category={statusOf(issue.status).glyph} />,
  },
  {
    id: 'title',
    label: 'Title',
    required: true,
    className: 'flex min-w-0 flex-1 items-center gap-2',
    render: (issue) => (
      <>
        <span className="truncate">{issue.title}</span>
        {issue.subIssueCount > 0 && (
          <span className="shrink-0 font-mono text-[11px] text-muted-foreground">
            {issue.subIssueClosedCount}/{issue.subIssueCount}
          </span>
        )}
        {issue.commentCount > 0 && (
          <span className="flex shrink-0 items-center gap-1 text-xs text-muted-foreground">
            <MessageSquare className="size-3.5" aria-hidden="true" />
            {issue.commentCount}
          </span>
        )}
      </>
    ),
  },
  {
    id: 'labels',
    label: 'Labels',
    className: 'hidden shrink-0 gap-1 md:flex',
    render: (issue) =>
      issue.labels.slice(0, 2).map((label) => <LabelChip key={label.id} label={label} />),
  },
  {
    id: 'estimate',
    label: 'Estimate',
    className: 'w-6 shrink-0 text-right font-mono text-[11px] text-muted-foreground',
    render: (issue) => issue.estimate ?? '',
  },
  {
    id: 'due',
    label: 'Due date',
    className: 'hidden w-16 shrink-0 justify-end sm:flex',
    render: (issue) =>
      issue.dueDate ? (
        <Badge variant={dueTone(issue.dueDate)} className="h-5">
          {formatDate(parseDueDate(issue.dueDate))}
        </Badge>
      ) : null,
  },
  {
    id: 'assignee',
    label: 'Assignee',
    className: 'w-5 shrink-0',
    render: (issue) =>
      issue.assignee ? (
        <Avatar name={issue.assignee.displayName} className="size-5 text-[9px]" />
      ) : (
        <span
          role="img"
          aria-label="Unassigned"
          className="inline-block size-5 rounded-full border border-dashed border-input"
        />
      ),
  },
  {
    id: 'updated',
    label: 'Updated',
    className: 'hidden w-20 shrink-0 text-right text-xs text-muted-foreground lg:block',
    render: (issue) => relativeTime(issue.updatedAt),
  },
  {
    id: 'created',
    label: 'Created',
    className: 'hidden w-16 shrink-0 text-right text-xs text-muted-foreground lg:block',
    render: (issue) => formatDate(issue.createdAt),
  },
];

const STORAGE_KEY = 'cadence.issue-columns';
const DEFAULT_COLUMNS: ColumnId[] = [
  'priority',
  'key',
  'status',
  'title',
  'labels',
  'due',
  'assignee',
];

/** The chosen columns, remembered per browser. */
export function useColumns() {
  const [columns, setColumns] = useState<ColumnId[]>(() => {
    try {
      const stored = JSON.parse(localStorage.getItem(STORAGE_KEY) ?? 'null') as unknown;
      return Array.isArray(stored) ? (stored as ColumnId[]) : DEFAULT_COLUMNS;
    } catch {
      return DEFAULT_COLUMNS;
    }
  });

  const change = (next: ColumnId[]) => {
    const withTitle = next.includes('title') ? next : [...next, 'title' as const];
    setColumns(withTitle);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(withTitle));
    } catch {
      // The choice lasts for this page when storage is unavailable.
    }
  };

  return [columns, change] as const;
}
