import type { Page, Route } from '@playwright/test';
import type {
  AccessTokenResponse,
  BoardResponse,
  CommentResponse,
  CurrentUserResponse,
  InvitationResponse,
  IssueDetail,
  IssueListItem,
  IssuePriority,
  IssueStatus,
  KeysetPageOfMemberResponse,
  LabelResponse,
  PersonSummary,
  ProjectMemberResponse,
  ProjectResponse,
  WorkflowResponse,
} from '../src/shared/api/generated/model';

/*
 * A fixed API for the visual suite: pages render from these fixtures instead of a server, so a
 * screenshot changes only when the UI does. Requests without a fixture fail the test.
 */

export const organization = {
  id: '0199a1b2-0000-7000-8000-000000000001',
  name: 'Analytical Engines',
  slug: 'analytical-engines',
};

const me: CurrentUserResponse = {
  id: '0199a1b2-0000-7000-8000-00000000000a',
  email: 'ada@analytical.example',
  displayName: 'Ada Lovelace',
  organizations: [
    { ...organization, role: 'Owner' },
    {
      id: '0199a1b2-0000-7000-8000-000000000002',
      name: 'Difference Engines',
      slug: 'difference-engines',
      role: 'Admin',
    },
  ],
};

const members: KeysetPageOfMemberResponse = {
  items: [
    {
      userId: me.id,
      displayName: 'Ada Lovelace',
      email: me.email,
      role: 'Owner',
      joinedAt: '2026-09-01T09:00:00Z',
    },
    {
      userId: '0199a1b2-0000-7000-8000-00000000000b',
      displayName: 'Grace Hopper',
      email: 'grace@analytical.example',
      role: 'Admin',
      joinedAt: '2026-09-03T09:00:00Z',
    },
    {
      userId: '0199a1b2-0000-7000-8000-00000000000c',
      displayName: 'Alan Turing',
      email: 'alan@analytical.example',
      role: 'Member',
      joinedAt: '2026-09-10T09:00:00Z',
    },
  ],
  nextCursor: null,
};

const invitations: InvitationResponse[] = [
  {
    id: '0199a1b2-0000-7000-8000-0000000000f1',
    email: 'katherine@analytical.example',
    role: 'Member',
    invitedByName: 'Ada Lovelace',
    createdAt: '2026-09-29T09:00:00Z',
    expiresAt: '2026-10-06T09:00:00Z',
  },
];

const ada: PersonSummary = { id: me.id, displayName: 'Ada Lovelace' };
const grace: PersonSummary = {
  id: '0199a1b2-0000-7000-8000-00000000000b',
  displayName: 'Grace Hopper',
};
const alan: PersonSummary = {
  id: '0199a1b2-0000-7000-8000-00000000000c',
  displayName: 'Alan Turing',
};

const atlas: ProjectResponse = {
  id: '0199a1b2-0000-7000-8000-0000000000a1',
  key: 'ATL',
  name: 'Atlas Mobile',
  description: 'The iOS and Android apps.',
  role: 'Admin',
  openIssueCount: 9,
  createdAt: '2026-09-01T09:00:00Z',
  archivedAt: null,
};

const projects: ProjectResponse[] = [
  atlas,
  {
    id: '0199a1b2-0000-7000-8000-0000000000a2',
    key: 'WEB',
    name: 'Website',
    description: null,
    role: 'Member',
    openIssueCount: 4,
    createdAt: '2026-09-02T09:00:00Z',
    archivedAt: null,
  },
];

const bug: LabelResponse = {
  id: '0199a1b2-0000-7000-8000-0000000000b1',
  name: 'bug',
  color: 'Red',
};
const design: LabelResponse = {
  id: '0199a1b2-0000-7000-8000-0000000000b2',
  name: 'design',
  color: 'Violet',
};
const performance: LabelResponse = {
  id: '0199a1b2-0000-7000-8000-0000000000b3',
  name: 'performance',
  color: 'Amber',
};
const sync: LabelResponse = {
  id: '0199a1b2-0000-7000-8000-0000000000b4',
  name: 'sync',
  color: 'Teal',
};
const labels = [bug, design, performance, sync];

const issue = (
  number: number,
  title: string,
  status: IssueStatus,
  priority: IssuePriority,
  extra: Partial<IssueListItem> = {},
): IssueListItem => ({
  id: `0199a1b2-0000-7000-8000-${String(number).padStart(12, '0')}`,
  key: `ATL-${String(number)}`,
  projectId: atlas.id,
  type: 'Task',
  title,
  status,
  statusId: `status-${status}`,
  priority,
  assignee: ada,
  estimate: null,
  dueDate: null,
  labels: [],
  subIssueCount: 0,
  subIssueClosedCount: 0,
  commentCount: 0,
  createdAt: '2026-09-20T09:00:00Z',
  updatedAt: '2026-09-29T15:00:00Z',
  ...extra,
});

const offlineEdits = issue(
  142,
  'Offline edits are lost when the app is killed',
  'InProgress',
  'Urgent',
  {
    type: 'Bug',
    labels: [bug, sync],
    commentCount: 2,
    subIssueCount: 3,
    subIssueClosedCount: 1,
    dueDate: '2026-10-01',
    estimate: 5,
    updatedAt: '2026-09-30T08:10:00Z',
  },
);
const syncEngine = issue(120, 'Ship the offline sync engine', 'InProgress', 'High', {
  type: 'Epic',
  subIssueCount: 6,
  subIssueClosedCount: 4,
});

const issues: IssueListItem[] = [
  offlineEdits,
  issue(139, 'Board columns stutter while scrolling on older phones', 'Todo', 'High', {
    labels: [performance],
    assignee: grace,
    dueDate: '2026-10-08',
  }),
  issue(137, 'Redesign the empty inbox', 'InReview', 'Medium', {
    type: 'Story',
    labels: [design],
    commentCount: 1,
  }),
  issue(131, 'Push notifications arrive twice after signing in again', 'Todo', 'High', {
    type: 'Bug',
    labels: [bug],
    assignee: alan,
  }),
  issue(128, 'Cache avatars between sessions', 'Backlog', 'Low', { assignee: null }),
  issue(126, 'Pull to refresh on the issue list', 'Backlog', 'None', { assignee: null }),
  syncEngine,
  issue(117, 'Haptics on drag and drop', 'Done', 'Low', { labels: [design] }),
];

const detail: IssueDetail = {
  ...offlineEdits,
  projectName: atlas.name,
  role: 'Admin',
  description:
    'When the app is killed while offline, edits made since the last sync are gone on the next launch.\n\n**Steps**\n\n1. Turn on airplane mode\n2. Edit an issue title\n3. Force-quit the app\n\nThe outbox lives in memory only; it should be written to `SQLite` before the edit is acknowledged.',
  reporter: grace,
  parent: {
    id: syncEngine.id,
    key: syncEngine.key,
    title: syncEngine.title,
    status: syncEngine.status,
    type: syncEngine.type,
  },
  subIssues: [
    {
      id: 's1',
      key: 'ATL-143',
      title: 'Persist the outbox to SQLite',
      status: 'Done',
      type: 'Task',
    },
    {
      id: 's2',
      key: 'ATL-144',
      title: 'Replay the outbox on launch',
      status: 'InProgress',
      type: 'Task',
    },
    { id: 's3', key: 'ATL-145', title: 'Show unsynced edits', status: 'Todo', type: 'Task' },
  ],
  links: [
    {
      id: 'l1',
      relation: 'blocked_by',
      issue: {
        id: 'r1',
        key: 'WEB-31',
        title: 'Sync API accepts batched writes',
        status: 'InReview',
        type: 'Task',
      },
    },
  ],
  completedAt: null,
  version: 1,
};

const comments: CommentResponse[] = [
  {
    id: 'c1',
    author: grace,
    body: 'Reproduced on a Pixel 6 and an iPhone 12. It only happens when the app is **force-quit**, not when it is backgrounded.',
    createdAt: '2026-09-29T10:12:00Z',
    editedAt: null,
    revisionCount: 0,
  },
  {
    id: 'c2',
    author: ada,
    body: 'The outbox is in memory. I will persist it first, then replay it on launch.',
    createdAt: '2026-09-29T14:40:00Z',
    editedAt: '2026-09-29T14:45:00Z',
    revisionCount: 1,
  },
];

const categories: { status: IssueStatus; name: string; wipLimit: number | null }[] = [
  { status: 'Backlog', name: 'Backlog', wipLimit: null },
  { status: 'Todo', name: 'Todo', wipLimit: null },
  { status: 'InProgress', name: 'In progress', wipLimit: 2 },
  { status: 'InReview', name: 'In review', wipLimit: 3 },
  { status: 'Done', name: 'Done', wipLimit: null },
  { status: 'Canceled', name: 'Canceled', wipLimit: null },
];

const workflow: WorkflowResponse = {
  id: '0199a1b2-0000-7000-8000-0000000000c1',
  projectId: atlas.id,
  restrictTransitions: false,
  statuses: categories.map((category, position) => ({
    id: `status-${category.status}`,
    name: category.name,
    category: category.status,
    position,
    wipLimit: category.wipLimit,
    isDefault: category.status === 'Todo',
  })),
  transitions: [],
};

// Cards in rank order: the issues above, ranked by their place in the list.
const rankDigits = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz';
const board: BoardResponse = {
  projectId: atlas.id,
  projectKey: atlas.key,
  closedWindowDays: 14,
  columns: categories.map((category) => {
    const cards = issues
      .map((item, index) => ({ item, rank: `a${rankDigits.charAt(index + 1)}` }))
      .filter(({ item }) => item.status === category.status)
      .map(({ item, rank }) => ({
        id: item.id,
        key: item.key,
        type: item.type,
        title: item.title,
        statusId: item.statusId,
        priority: item.priority,
        rank,
        version: 1,
        assignee: item.assignee,
        labels: item.labels,
        estimate: item.estimate,
        dueDate: item.dueDate,
        subIssueCount: item.subIssueCount,
        subIssueClosedCount: item.subIssueClosedCount,
        commentCount: item.commentCount,
        epic:
          item.id === syncEngine.id || item.type === 'Epic'
            ? null
            : { id: syncEngine.id, key: syncEngine.key, title: syncEngine.title },
      }));
    return {
      statusId: `status-${category.status}`,
      name: category.name,
      category: category.status,
      wipLimit: category.wipLimit,
      total: cards.length,
      hasMore: false,
      nextRank: null,
      cards,
    };
  }),
};

const projectPeople: ProjectMemberResponse[] = [
  {
    userId: ada.id,
    displayName: ada.displayName,
    email: me.email,
    role: 'Admin',
    isImplicit: true,
  },
  {
    userId: grace.id,
    displayName: grace.displayName,
    email: 'grace@analytical.example',
    role: 'Member',
    isImplicit: false,
  },
  {
    userId: alan.id,
    displayName: alan.displayName,
    email: 'alan@analytical.example',
    role: 'Member',
    isImplicit: false,
  },
];

const json = (route: Route, body: unknown, status = 200) =>
  route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });

export async function mockApi(page: Page, { signedIn }: { signedIn: boolean }) {
  await page.route('**/api/v1/**', async (route) => {
    const { pathname } = new URL(route.request().url());
    const orgBase = `/api/v1/organizations/${organization.id}`;

    if (pathname === '/api/v1/auth/refresh') {
      if (!signedIn) return json(route, { title: 'Unauthorized', status: 401 }, 401);
      const token: AccessTokenResponse = {
        accessToken: 'visual-test-token',
        expiresAt: '2099-01-01T00:00:00Z',
      };
      return json(route, token);
    }
    if (pathname === '/api/v1/me') return json(route, me);
    if (pathname === `${orgBase}/members`) return json(route, members);
    if (pathname === `${orgBase}/invitations`) return json(route, invitations);
    if (pathname === orgBase) return json(route, { ...organization, role: 'Owner' });
    if (pathname === `${orgBase}/projects`) return json(route, projects);
    if (pathname === `${orgBase}/projects/${atlas.id}/members`) return json(route, projectPeople);
    if (pathname === `${orgBase}/labels`) return json(route, labels);
    if (pathname === `${orgBase}/projects/${atlas.id}/workflow`) return json(route, workflow);
    if (pathname === `${orgBase}/projects/${atlas.id}/board`) return json(route, board);
    if (pathname === `${orgBase}/issues`) {
      // My work asks for the user's open issues; project lists get everything.
      const mine = new URL(route.request().url()).searchParams.has('assigneeId');
      const items = mine
        ? issues.filter((item) => item.assignee?.id === me.id && item.status !== 'Done')
        : issues;
      return json(route, { items, nextCursor: null });
    }
    if (pathname === `${orgBase}/issues/by-key/${detail.key}`) return json(route, detail);
    if (pathname === `${orgBase}/issues/${detail.id}/comments`) {
      return json(route, { items: comments, nextCursor: null });
    }

    throw new Error(`No fixture for ${route.request().method()} ${pathname}`);
  });
}
