import { useWindowVirtualizer } from '@tanstack/react-virtual';
import { Inbox } from 'lucide-react';
import { useCallback, useEffect, useState, type MouseEvent } from 'react';
import { Link, useSearchParams } from 'react-router';
import { useListIssuesInfinite } from '@/shared/api/generated/endpoints';
import type { ListIssuesParams } from '@/shared/api/generated/model';
import { cn } from '@/shared/lib/utils';
import { Skeleton } from '@/shared/ui/skeleton';
import { useCurrentOrganization } from '@/shared/workspace';
import { COLUMNS, type ColumnId } from './issue-columns';

const ROW_HEIGHT = 41;

/**
 * The issue list (design board 09): one row per issue, virtualized so thousands of issues scroll
 * smoothly with only the visible rows in the DOM, and loaded a keyset page at a time as the user
 * nears the end. Clicking a row opens it in the drawer; Ctrl/⌘-click opens the full page.
 */
export function IssueList({
  params,
  columns,
  empty,
}: {
  params: ListIssuesParams;
  columns: readonly ColumnId[];
  empty: React.ReactNode;
}) {
  const { organization } = useCurrentOrganization();
  const [, setSearchParams] = useSearchParams();
  // The list's distance from the top of the page, which the window virtualizer offsets rows by.
  const [scrollMargin, setScrollMargin] = useState(0);
  const listRef = useCallback((node: HTMLDivElement | null) => {
    if (node) setScrollMargin(node.offsetTop);
  }, []);

  const query = useListIssuesInfinite(organization?.id ?? '', params, {
    query: {
      enabled: !!organization,
      initialPageParam: undefined,
      getNextPageParam: (page) => page.nextCursor ?? undefined,
      staleTime: 15_000,
    },
  });

  const issues = query.data?.pages.flatMap((page) => page.items) ?? [];
  const hasMore = query.hasNextPage;

  const virtualizer = useWindowVirtualizer({
    count: hasMore ? issues.length + 1 : issues.length,
    estimateSize: () => ROW_HEIGHT,
    overscan: 12,
    scrollMargin,
  });

  const items = virtualizer.getVirtualItems();
  const lastIndex = items.at(-1)?.index ?? 0;

  useEffect(() => {
    if (hasMore && !query.isFetchingNextPage && lastIndex >= issues.length - 1) {
      void query.fetchNextPage();
    }
  }, [hasMore, lastIndex, issues.length, query]);

  if (query.isPending) {
    return (
      <div className="flex flex-col gap-2 py-2" aria-busy="true">
        {Array.from({ length: 8 }, (_, index) => (
          <Skeleton key={index} className="h-8" />
        ))}
      </div>
    );
  }

  if (issues.length === 0) {
    return (
      <div className="flex flex-col items-center gap-2 rounded-lg border px-6 py-14 text-center">
        <Inbox className="size-8 text-subtle-foreground" aria-hidden="true" />
        {empty}
      </div>
    );
  }

  const shown = COLUMNS.filter((column) => columns.includes(column.id));

  const openInDrawer = (event: MouseEvent, key: string) => {
    if (event.metaKey || event.ctrlKey || event.shiftKey || event.button !== 0) return;
    event.preventDefault();
    setSearchParams((current) => {
      const next = new URLSearchParams(current);
      next.set('issue', key);
      return next;
    });
  };

  return (
    <div
      ref={listRef}
      role="list"
      aria-label="Issues"
      className="overflow-hidden rounded-lg border"
    >
      <div className="relative" style={{ height: virtualizer.getTotalSize() }}>
        {items.map((row) => {
          const issue = issues[row.index];
          return (
            <div
              key={row.key}
              role="listitem"
              className="absolute inset-x-0 border-b last:border-b-0"
              style={{
                height: ROW_HEIGHT,
                transform: `translateY(${String(row.start - virtualizer.options.scrollMargin)}px)`,
              }}
            >
              {issue ? (
                <Link
                  to={`/${organization?.slug ?? ''}/issues/${issue.key}`}
                  onClick={(event) => {
                    openInDrawer(event, issue.key);
                  }}
                  className="flex h-full items-center gap-3 px-3 text-sm outline-none hover:bg-accent/50 focus-visible:bg-accent sm:px-4"
                >
                  {shown.map((column) => (
                    <span
                      key={column.id}
                      className={cn('inline-flex items-center', column.className)}
                    >
                      {column.render(issue)}
                    </span>
                  ))}
                </Link>
              ) : (
                <div className="flex h-full items-center px-4 text-[13px] text-muted-foreground">
                  Loading more…
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}
