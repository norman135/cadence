import { useSearchParams } from 'react-router';
import type {
  IssuePriority,
  IssueSort,
  IssueStatus,
  IssueType,
  ListIssuesParams,
} from '@/shared/api/generated/model';
import { PRIORITIES, SORTS, STATUSES, TYPES } from './options';

/** The list's filters and sort, kept in the URL so views can be shared and survive a reload. */
export interface IssueFilters {
  status: IssueStatus[];
  priority: IssuePriority[];
  type: IssueType[];
  /** User ids, plus "none" for unassigned. */
  assignee: string[];
  label: string[];
  q: string;
  sort: NonNullable<IssueSort>;
}

const pick = <T extends string>(values: string[], allowed: readonly { value: T }[]) =>
  values.filter((value): value is T => allowed.some((option) => option.value === value));

export function useIssueFilters() {
  const [params, setParams] = useSearchParams();

  const filters: IssueFilters = {
    status: pick(params.getAll('status'), STATUSES),
    priority: pick(params.getAll('priority'), PRIORITIES),
    type: pick(params.getAll('type'), TYPES),
    assignee: params.getAll('assignee'),
    label: params.getAll('label'),
    q: params.get('q') ?? '',
    sort: pick([params.get('sort') ?? ''], SORTS)[0] ?? 'Updated',
  };

  const update = (change: Partial<IssueFilters>) => {
    setParams(
      (current) => {
        const next = new URLSearchParams(current);
        for (const [key, value] of Object.entries(change)) {
          next.delete(key);
          if (Array.isArray(value)) {
            for (const item of value) next.append(key, item);
          } else if (value && !(key === 'sort' && value === 'Updated')) {
            next.set(key, value);
          }
        }
        return next;
      },
      { replace: true },
    );
  };

  const active =
    filters.status.length +
    filters.priority.length +
    filters.type.length +
    filters.assignee.length +
    filters.label.length;

  return { filters, update, active };
}

/** Turns the URL filters into API parameters. */
export function toListParams(filters: IssueFilters, projectId?: string): ListIssuesParams {
  const assignees = filters.assignee.filter((id) => id !== 'none');
  return {
    ...(projectId ? { projectId: [projectId] } : {}),
    ...(filters.status.length ? { status: filters.status } : {}),
    ...(filters.priority.length ? { priority: filters.priority } : {}),
    ...(filters.type.length ? { type: filters.type } : {}),
    ...(assignees.length ? { assigneeId: assignees } : {}),
    ...(filters.assignee.includes('none') ? { unassigned: true } : {}),
    ...(filters.label.length ? { labelId: filters.label } : {}),
    ...(filters.q ? { q: filters.q } : {}),
    sort: filters.sort,
    limit: 50,
  };
}
