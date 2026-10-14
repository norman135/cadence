import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { beforeEach, describe, expect, it } from 'vitest';
import type {
  CurrentUserResponse,
  IssueListItem,
  KeysetPageOfIssueListItem,
  ProjectResponse,
} from '@/shared/api/generated/model';
import { server } from '@/test/msw-server';
import { IssuesPage } from './issues-page';

const me: CurrentUserResponse = {
  id: 'u1',
  email: 'ada@example.com',
  displayName: 'Ada',
  organizations: [{ id: 'o1', name: 'Acme', slug: 'acme', role: 'Member' }],
};

const project: ProjectResponse = {
  id: 'p1',
  key: 'ATL',
  name: 'Atlas',
  description: null,
  role: 'Member',
  openIssueCount: 2,
  createdAt: '2026-10-01T00:00:00Z',
  archivedAt: null,
};

const issue = (number: number, title: string): IssueListItem => ({
  id: `i${String(number)}`,
  key: `ATL-${String(number)}`,
  projectId: 'p1',
  type: 'Task',
  title,
  status: 'Todo',
  statusId: 's-todo',
  priority: 'High',
  assignee: { id: 'u1', displayName: 'Ada' },
  estimate: null,
  dueDate: null,
  labels: [],
  subIssueCount: 0,
  subIssueClosedCount: 0,
  commentCount: 0,
  createdAt: '2026-10-01T00:00:00Z',
  updatedAt: '2026-10-02T00:00:00Z',
});

function renderPage(path: string) {
  const router = createMemoryRouter(
    [
      { path: '/:orgSlug/projects/:projectKey', Component: IssuesPage },
      { path: '/:orgSlug/issues', Component: IssuesPage },
    ],
    { initialEntries: [path] },
  );
  render(
    <QueryClientProvider
      client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
    >
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

describe('IssuesPage', () => {
  let listRequests: URL[];

  beforeEach(() => {
    localStorage.clear();
    listRequests = [];
    server.use(
      http.get('*/api/v1/me', () => HttpResponse.json(me)),
      http.get('*/api/v1/organizations/o1/projects', () => HttpResponse.json([project])),
      http.get('*/api/v1/organizations/o1/projects/p1/members', () => HttpResponse.json([])),
      http.get('*/api/v1/organizations/o1/members', () =>
        HttpResponse.json({ items: [], nextCursor: null }),
      ),
      http.get('*/api/v1/organizations/o1/labels', () => HttpResponse.json([])),
      http.get('*/api/v1/organizations/o1/issues', ({ request }) => {
        listRequests.push(new URL(request.url));
        const page: KeysetPageOfIssueListItem = {
          items: [issue(2, 'Crash on launch'), issue(1, 'Offline mode')],
          nextCursor: null,
        };
        return HttpResponse.json(page);
      }),
    );
  });

  it("lists a project's issues, filtered from the URL", async () => {
    renderPage('/acme/projects/ATL?status=Todo');

    expect(await screen.findByRole('heading', { name: 'Atlas' })).toBeInTheDocument();
    expect(await screen.findByText('Crash on launch')).toBeInTheDocument();
    expect(screen.getByText('ATL-1')).toBeInTheDocument();

    const request = listRequests.at(-1);
    expect(request?.searchParams.getAll('projectId')).toEqual(['p1']);
    expect(request?.searchParams.getAll('status')).toEqual(['Todo']);
  });

  it('explains when a project does not exist', async () => {
    renderPage('/acme/projects/NOPE');

    expect(await screen.findByText('Project not found')).toBeInTheDocument();
  });
});
