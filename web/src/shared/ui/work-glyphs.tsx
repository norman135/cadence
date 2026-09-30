import { cn } from '@/shared/lib/utils';

/*
 * Workflow status and priority glyphs from the design boards (02 Color). The shape carries the
 * meaning and color reinforces it, so they read in grayscale and for color-blind users. Drawn on
 * a 14 px grid; the label is available to screen readers and as a tooltip.
 */

export type WorkflowCategory = 'backlog' | 'todo' | 'progress' | 'review' | 'done' | 'canceled';
export type Priority = 'urgent' | 'high' | 'medium' | 'low' | 'none';

const statusLabels: Record<WorkflowCategory, string> = {
  backlog: 'Backlog',
  todo: 'Todo',
  progress: 'In progress',
  review: 'In review',
  done: 'Done',
  canceled: 'Canceled',
};

const statusColors: Record<WorkflowCategory, string> = {
  backlog: 'text-status-backlog',
  todo: 'text-status-todo',
  progress: 'text-status-progress',
  review: 'text-status-review',
  done: 'text-status-done',
  canceled: 'text-status-canceled',
};

export function StatusIcon({
  category,
  label = statusLabels[category],
  className,
}: {
  category: WorkflowCategory;
  label?: string;
  className?: string;
}) {
  return (
    <svg
      viewBox="0 0 14 14"
      role="img"
      aria-label={label}
      className={cn('size-3.5 shrink-0', statusColors[category], className)}
    >
      <title>{label}</title>
      {category === 'backlog' && (
        <circle
          cx="7"
          cy="7"
          r="5.5"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.5"
          strokeDasharray="2.2 1.9"
        />
      )}
      {(category === 'todo' || category === 'progress' || category === 'review') && (
        <circle cx="7" cy="7" r="5.5" fill="none" stroke="currentColor" strokeWidth="1.5" />
      )}
      {category === 'progress' && <path d="M7 3.5 A3.5 3.5 0 0 1 7 10.5 Z" fill="currentColor" />}
      {category === 'review' && <path d="M7 3.5 A3.5 3.5 0 1 1 3.5 7 L7 7 Z" fill="currentColor" />}
      {(category === 'done' || category === 'canceled') && (
        <circle cx="7" cy="7" r="6.25" fill="currentColor" />
      )}
      {category === 'done' && (
        <path
          d="M4.4 7.1 6.2 8.9 9.7 5.3"
          fill="none"
          stroke="white"
          strokeWidth="1.6"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
      )}
      {category === 'canceled' && (
        <path d="M5 5 9 9M9 5 5 9" stroke="white" strokeWidth="1.5" strokeLinecap="round" />
      )}
    </svg>
  );
}

const priorityLabels: Record<Priority, string> = {
  urgent: 'Urgent',
  high: 'High priority',
  medium: 'Medium priority',
  low: 'Low priority',
  none: 'No priority',
};

const filledBars: Record<Exclude<Priority, 'urgent' | 'none'>, number> = {
  high: 3,
  medium: 2,
  low: 1,
};

export function PriorityIcon({ priority, className }: { priority: Priority; className?: string }) {
  const label = priorityLabels[priority];

  return (
    <svg
      viewBox="0 0 14 14"
      role="img"
      aria-label={label}
      className={cn('size-3.5 shrink-0 text-muted-foreground', className)}
    >
      <title>{label}</title>
      {priority === 'urgent' && (
        <>
          <rect x="0.5" y="0.5" width="13" height="13" rx="3.5" className="fill-priority-urgent" />
          <path d="M7 3.6v4.2" stroke="white" strokeWidth="1.8" strokeLinecap="round" />
          <circle cx="7" cy="10.3" r="1.05" fill="white" />
        </>
      )}
      {priority === 'none' && (
        <path
          d="M2 7h2M6 7h2M10 7h2"
          stroke="currentColor"
          strokeWidth="1.5"
          strokeLinecap="round"
          opacity="0.6"
        />
      )}
      {priority !== 'urgent' &&
        priority !== 'none' &&
        [0, 1, 2].map((bar) => (
          <rect
            key={bar}
            x={1.5 + bar * 4}
            y={12 - (4 + bar * 3)}
            width="3"
            height={4 + bar * 3}
            rx="1"
            fill="currentColor"
            opacity={bar < filledBars[priority] ? 1 : 0.25}
          />
        ))}
    </svg>
  );
}
