import type { Page, Route } from '@playwright/test';
import type {
  AccessTokenResponse,
  CurrentUserResponse,
  InvitationResponse,
  KeysetPageOfMemberResponse,
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

    throw new Error(`No fixture for ${route.request().method()} ${pathname}`);
  });
}
