import type {
  IssuePriority,
  IssueSort,
  IssueStatus,
  IssueType,
  LabelColor,
} from '@/shared/api/generated/model';
import type { Priority, WorkflowCategory } from '@/shared/ui/work-glyphs';

/** Display order and names of the fixed status categories (design board 02). */
export const STATUSES: readonly { value: IssueStatus; label: string; glyph: WorkflowCategory }[] = [
  { value: 'Backlog', label: 'Backlog', glyph: 'backlog' },
  { value: 'Todo', label: 'Todo', glyph: 'todo' },
  { value: 'InProgress', label: 'In progress', glyph: 'progress' },
  { value: 'InReview', label: 'In review', glyph: 'review' },
  { value: 'Done', label: 'Done', glyph: 'done' },
  { value: 'Canceled', label: 'Canceled', glyph: 'canceled' },
];

export const OPEN_STATUSES: readonly IssueStatus[] = ['Backlog', 'Todo', 'InProgress', 'InReview'];

export const PRIORITIES: readonly { value: IssuePriority; label: string; glyph: Priority }[] = [
  { value: 'Urgent', label: 'Urgent', glyph: 'urgent' },
  { value: 'High', label: 'High', glyph: 'high' },
  { value: 'Medium', label: 'Medium', glyph: 'medium' },
  { value: 'Low', label: 'Low', glyph: 'low' },
  { value: 'None', label: 'No priority', glyph: 'none' },
];

export const TYPES: readonly { value: IssueType; label: string }[] = [
  { value: 'Task', label: 'Task' },
  { value: 'Bug', label: 'Bug' },
  { value: 'Story', label: 'Story' },
  { value: 'Epic', label: 'Epic' },
];

export const SORTS: readonly { value: NonNullable<IssueSort>; label: string }[] = [
  { value: 'Updated', label: 'Recently updated' },
  { value: 'Created', label: 'Newest' },
  { value: 'Priority', label: 'Priority' },
  { value: 'DueDate', label: 'Due date' },
];

export const ESTIMATES = [1, 2, 3, 5, 8, 13] as const;

export const statusOf = (value: IssueStatus) =>
  STATUSES.find((status) => status.value === value) ?? {
    value,
    label: value,
    glyph: 'todo' as const,
  };

export const priorityOf = (value: IssuePriority) =>
  PRIORITIES.find((priority) => priority.value === value) ?? {
    value,
    label: value,
    glyph: 'none' as const,
  };

/** The label palette's token for each color (design board 02). */
export const LABEL_COLORS: Record<LabelColor, string> = {
  Teal: 'var(--tempo-500)',
  Blue: '#3b82f6',
  Violet: '#8b5cf6',
  Pink: '#e0508f',
  Red: '#e5484d',
  Orange: 'var(--ember-500)',
  Amber: '#e8a317',
  Green: '#22a35a',
  Slate: '#64748b',
};

/** "Oct 14" this year, "Oct 14, 2027" otherwise. */
export function formatDate(value: string | Date) {
  const date = typeof value === 'string' ? new Date(value) : value;
  const sameYear = date.getFullYear() === new Date().getFullYear();
  return date.toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    ...(sameYear ? {} : { year: 'numeric' }),
  });
}

/** Due dates are calendar dates (no time zone): parse them as local dates. */
export function parseDueDate(value: string) {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year ?? 1970, (month ?? 1) - 1, day ?? 1);
}

/** Overdue, due within two days, or later: the due badge turns amber then red. */
export function dueTone(value: string): 'destructive' | 'warning' | 'default' {
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const days = (parseDueDate(value).getTime() - today.getTime()) / 86_400_000;
  if (days < 0) return 'destructive';
  if (days <= 2) return 'warning';
  return 'default';
}

/** "3 min ago", "5 h ago", "2 d ago", then a date (design board 03 number rules). */
export function relativeTime(value: string) {
  const seconds = (Date.now() - new Date(value).getTime()) / 1000;
  if (seconds < 60) return 'just now';
  if (seconds < 3600) return `${String(Math.floor(seconds / 60))} min ago`;
  if (seconds < 86_400) return `${String(Math.floor(seconds / 3600))} h ago`;
  if (seconds < 7 * 86_400) return `${String(Math.floor(seconds / 86_400))} d ago`;
  return formatDate(value);
}
