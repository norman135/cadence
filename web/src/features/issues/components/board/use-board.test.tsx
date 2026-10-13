import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import type { ReactNode } from 'react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { describe, expect, it } from 'vitest';
import { getGetBoardQueryKey, getGetCurrentUserQueryKey } from '@/shared/api/generated/endpoints';
import type {
  BoardCard,
  BoardColumn,
  BoardResponse,
  CurrentUserResponse,
} from '@/shared/api/generated/model';
import { server } from '@/test/msw-server';
import { applyMove, useMoveCardOnBoard } from './use-board';

const card = (id: string, statusId: string, rank: string): BoardCard => ({
  id,
  key: `ATL-${id}`,
  type: 'Task',
  title: `Card ${id}`,
  statusId,
  priority: 'None',
  rank,
  version: 7,
  assignee: null,
  labels: [],
  estimate: null,
  dueDate: null,
  subIssueCount: 0,
  subIssueClosedCount: 0,
  commentCount: 0,
  epic: null,
});

const one = card('1', 'todo', 'a0');
const two = card('2', 'todo', 'a1');
const three = card('3', 'doing', 'a0V');

const todo: BoardColumn = {
  statusId: 'todo',
  name: 'Todo',
  category: 'Todo',
  wipLimit: null,
  total: 2,
  hasMore: false,
  nextRank: null,
  cards: [one, two],
};

const doing: BoardColumn = {
  statusId: 'doing',
  name: 'In progress',
  category: 'InProgress',
  wipLimit: 3,
  total: 1,
  hasMore: false,
  nextRank: null,
  cards: [three],
};

const board: BoardResponse = {
  projectId: 'p1',
  projectKey: 'ATL',
  closedWindowDays: 14,
  columns: [todo, doing],
};

const column = (from: BoardResponse | undefined, statusId: string) =>
  from?.columns.find((candidate) => candidate.statusId === statusId);
const ids = (from: BoardResponse | undefined, statusId: string) =>
  column(from, statusId)?.cards.map((candidate) => candidate.id);

describe('applyMove', () => {
  it('moves a card to its status and place by rank, keeping totals right', () => {
    const moved = applyMove(board, two, 'doing', 'Zz');

    expect(ids(moved, 'todo')).toEqual(['1']);
    expect(column(moved, 'todo')?.total).toBe(1);
    expect(ids(moved, 'doing')).toEqual(['2', '3']);
    expect(column(moved, 'doing')?.total).toBe(2);
    expect(column(moved, 'doing')?.cards[0]).toMatchObject({ statusId: 'doing', rank: 'Zz' });
  });

  it('reorders within a column', () => {
    const moved = applyMove(board, one, 'todo', 'a2');
    expect(ids(moved, 'todo')).toEqual(['2', '1']);
    expect(column(moved, 'todo')?.total).toBe(2);
  });
});

describe('useMoveCardOnBoard', () => {
  const me: CurrentUserResponse = {
    id: 'u1',
    email: 'ada@example.com',
    displayName: 'Ada',
    organizations: [{ id: 'o1', name: 'Acme', slug: 'acme', role: 'Member' }],
  };

  function setup() {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    queryClient.setQueryData(getGetBoardQueryKey('o1', 'p1', {}), board);
    queryClient.setQueryData(getGetCurrentUserQueryKey(), me);
    const wrapper = ({ children }: { children: ReactNode }) => {
      const router = createMemoryRouter([{ path: '/:orgSlug', element: children }], {
        initialEntries: ['/acme'],
      });
      return (
        <QueryClientProvider client={queryClient}>
          <RouterProvider router={router} />
        </QueryClientProvider>
      );
    };
    server.use(http.get('*/api/v1/me', () => HttpResponse.json(me)));
    const hook = renderHook(() => useMoveCardOnBoard('p1', {}), { wrapper });
    return { queryClient, hook };
  }

  const current = (queryClient: QueryClient) =>
    queryClient.getQueryData<BoardResponse>(getGetBoardQueryKey('o1', 'p1', {}));

  it('keeps the move and takes the new version when the server agrees', async () => {
    server.use(
      http.post('*/api/v1/organizations/o1/projects/p1/board/moves', () =>
        HttpResponse.json({
          issueId: '2',
          statusId: 'doing',
          category: 'InProgress',
          rank: 'Zz',
          version: 8,
        }),
      ),
      http.get('*/api/v1/organizations/o1/issues', () =>
        HttpResponse.json({ items: [], nextCursor: null }),
      ),
      http.get('*/api/v1/organizations/o1/projects', () => HttpResponse.json([])),
    );
    const { queryClient, hook } = setup();

    await act(() => hook.result.current(two, 'doing', 'Zz'));

    const after = current(queryClient);
    expect(column(after, 'doing')?.cards.map((c) => [c.id, c.version])).toEqual([
      ['2', 8],
      ['3', 7],
    ]);
  });

  it('puts the card back and reloads the board when someone else changed it first', async () => {
    server.use(
      http.post('*/api/v1/organizations/o1/projects/p1/board/moves', () =>
        HttpResponse.json(
          { status: 409, title: 'Conflict', code: 'issues.changed', detail: 'Changed meanwhile.' },
          { status: 409, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    const { queryClient, hook } = setup();

    await act(() => hook.result.current(two, 'doing', 'Zz'));

    expect(ids(current(queryClient), 'todo')).toEqual(['1', '2']);
    // The board is marked stale, so it reloads wherever it's shown.
    expect(queryClient.getQueryState(getGetBoardQueryKey('o1', 'p1', {}))?.isInvalidated).toBe(
      true,
    );
  });
});
