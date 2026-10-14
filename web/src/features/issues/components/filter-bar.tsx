import { ArrowDownUp, Columns3, Filter, Search, X } from 'lucide-react';
import { useEffect, useState, type ReactNode } from 'react';
import { useListMembers } from '@/shared/api/generated/endpoints';
import { Avatar } from '@/shared/ui/avatar';
import { Button } from '@/shared/ui/button';
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger,
  DropdownMenuTrigger,
} from '@/shared/ui/dropdown-menu';
import { Input } from '@/shared/ui/input';
import { PriorityIcon, StatusIcon } from '@/shared/ui/work-glyphs';
import { useCurrentOrganization, useLabels, useProjectPeople } from '@/shared/workspace';
import type { IssueFilters } from '../lib/filters';
import { LABEL_COLORS, PRIORITIES, SORTS, STATUSES, TYPES } from '../lib/options';
import { COLUMNS, type ColumnId } from './issue-columns';

function toggle<T>(values: readonly T[], value: T) {
  return values.includes(value) ? values.filter((item) => item !== value) : [...values, value];
}

/** People to filter by: the project's people, or the organization's members across projects. */
function usePeople(projectId: string | undefined) {
  const { organization } = useCurrentOrganization();
  const project = useProjectPeople(projectId);
  const members = useListMembers(organization?.id ?? '', undefined, {
    query: { enabled: !!organization && !projectId, staleTime: 60_000 },
  });
  return projectId
    ? (project.data ?? []).map((person) => ({ id: person.userId, name: person.displayName }))
    : (members.data?.items ?? []).map((member) => ({
        id: member.userId,
        name: member.displayName,
      }));
}

/**
 * Search, filters, sort and columns for the issue list (design board 09's toolbar). Changes go to
 * the URL immediately; typing in search waits for a pause.
 */
export function FilterBar({
  filters,
  update,
  active,
  projectId,
  columns,
  onColumnsChange,
  variant = 'list',
  actions,
}: {
  filters: IssueFilters;
  update: (change: Partial<IssueFilters>) => void;
  active: number;
  projectId: string | undefined;
  columns?: readonly ColumnId[];
  onColumnsChange?: (columns: ColumnId[]) => void;
  /** The board has a column per status and its own order, so it hides status, sort and columns. */
  variant?: 'list' | 'board';
  /** Controls shown at the end of the bar, such as the board's grouping. */
  actions?: ReactNode;
}) {
  const isList = variant === 'list';
  const [search, setSearch] = useState(filters.q);
  const { data: labels } = useLabels();
  const people = usePeople(projectId);

  useEffect(() => {
    if (search === filters.q) return;
    const timer = setTimeout(() => {
      update({ q: search.trim() });
    }, 300);
    return () => {
      clearTimeout(timer);
    };
  }, [search, filters.q, update]);

  return (
    <div className="flex flex-wrap items-center gap-2">
      <div className="relative w-full sm:w-64">
        <Search className="pointer-events-none absolute top-2 left-2.5 size-4 text-subtle-foreground" />
        <Input
          aria-label="Search issues"
          placeholder="Search by title or key"
          value={search}
          onChange={(event) => {
            setSearch(event.target.value);
          }}
          className="h-8 pl-8"
        />
      </div>

      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="secondary" size="sm">
            <Filter />
            Filter
            {active > 0 && (
              <span className="rounded-full bg-primary-soft px-1.5 text-[11px] text-primary-soft-foreground">
                {active}
              </span>
            )}
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="start">
          {isList && (
            <DropdownMenuSub>
              <DropdownMenuSubTrigger>Status</DropdownMenuSubTrigger>
              <DropdownMenuSubContent>
                {STATUSES.map((status) => (
                  <DropdownMenuCheckboxItem
                    key={status.value}
                    checked={filters.status.includes(status.value)}
                    onCheckedChange={() => {
                      update({ status: toggle(filters.status, status.value) });
                    }}
                  >
                    <StatusIcon category={status.glyph} />
                    {status.label}
                  </DropdownMenuCheckboxItem>
                ))}
              </DropdownMenuSubContent>
            </DropdownMenuSub>
          )}
          <DropdownMenuSub>
            <DropdownMenuSubTrigger>Priority</DropdownMenuSubTrigger>
            <DropdownMenuSubContent>
              {PRIORITIES.map((priority) => (
                <DropdownMenuCheckboxItem
                  key={priority.value}
                  checked={filters.priority.includes(priority.value)}
                  onCheckedChange={() => {
                    update({ priority: toggle(filters.priority, priority.value) });
                  }}
                >
                  <PriorityIcon priority={priority.glyph} />
                  {priority.label}
                </DropdownMenuCheckboxItem>
              ))}
            </DropdownMenuSubContent>
          </DropdownMenuSub>
          <DropdownMenuSub>
            <DropdownMenuSubTrigger>Assignee</DropdownMenuSubTrigger>
            <DropdownMenuSubContent className="max-h-72 overflow-y-auto">
              <DropdownMenuCheckboxItem
                checked={filters.assignee.includes('none')}
                onCheckedChange={() => {
                  update({ assignee: toggle(filters.assignee, 'none') });
                }}
              >
                Unassigned
              </DropdownMenuCheckboxItem>
              {people.map((person) => (
                <DropdownMenuCheckboxItem
                  key={person.id}
                  checked={filters.assignee.includes(person.id)}
                  onCheckedChange={() => {
                    update({ assignee: toggle(filters.assignee, person.id) });
                  }}
                >
                  <Avatar name={person.name} className="size-5 text-[9px]" />
                  {person.name}
                </DropdownMenuCheckboxItem>
              ))}
            </DropdownMenuSubContent>
          </DropdownMenuSub>
          <DropdownMenuSub>
            <DropdownMenuSubTrigger>Labels</DropdownMenuSubTrigger>
            <DropdownMenuSubContent className="max-h-72 overflow-y-auto">
              {labels?.map((label) => (
                <DropdownMenuCheckboxItem
                  key={label.id}
                  checked={filters.label.includes(label.id)}
                  onCheckedChange={() => {
                    update({ label: toggle(filters.label, label.id) });
                  }}
                >
                  <i
                    aria-hidden="true"
                    className="size-2 rounded-full"
                    style={{ backgroundColor: LABEL_COLORS[label.color] }}
                  />
                  {label.name}
                </DropdownMenuCheckboxItem>
              ))}
            </DropdownMenuSubContent>
          </DropdownMenuSub>
          <DropdownMenuSub>
            <DropdownMenuSubTrigger>Type</DropdownMenuSubTrigger>
            <DropdownMenuSubContent>
              {TYPES.map((type) => (
                <DropdownMenuCheckboxItem
                  key={type.value}
                  checked={filters.type.includes(type.value)}
                  onCheckedChange={() => {
                    update({ type: toggle(filters.type, type.value) });
                  }}
                >
                  {type.label}
                </DropdownMenuCheckboxItem>
              ))}
            </DropdownMenuSubContent>
          </DropdownMenuSub>
          {active > 0 && (
            <>
              <DropdownMenuSeparator />
              <DropdownMenuItem
                onSelect={() => {
                  update({ status: [], priority: [], type: [], assignee: [], label: [] });
                }}
              >
                <X />
                Clear filters
              </DropdownMenuItem>
            </>
          )}
        </DropdownMenuContent>
      </DropdownMenu>

      <span className="flex-1" />

      {actions}

      {isList && (
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="sm">
              <ArrowDownUp />
              {SORTS.find((sort) => sort.value === filters.sort)?.label}
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuLabel>Sort by</DropdownMenuLabel>
            <DropdownMenuRadioGroup
              value={filters.sort}
              onValueChange={(value) => {
                update({ sort: value as IssueFilters['sort'] });
              }}
            >
              {SORTS.map((sort) => (
                <DropdownMenuRadioItem key={sort.value} value={sort.value}>
                  {sort.label}
                </DropdownMenuRadioItem>
              ))}
            </DropdownMenuRadioGroup>
          </DropdownMenuContent>
        </DropdownMenu>
      )}

      {columns && onColumnsChange && (
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="sm" aria-label="Columns">
              <Columns3 />
              <span className="max-sm:hidden">Columns</span>
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuLabel>Show columns</DropdownMenuLabel>
            {COLUMNS.filter((column) => !column.required).map((column) => (
              <DropdownMenuCheckboxItem
                key={column.id}
                checked={columns.includes(column.id)}
                onCheckedChange={() => {
                  onColumnsChange(toggle(columns, column.id));
                }}
              >
                {column.label}
              </DropdownMenuCheckboxItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>
      )}
    </div>
  );
}
