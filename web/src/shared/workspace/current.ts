import { useParams } from 'react-router';
import { useGetCurrentUser } from '@/shared/api/generated/endpoints';
import type { MyOrganizationResponse } from '@/shared/api/generated/model';

const LAST_ORGANIZATION_KEY = 'cadence:last-organization';

/** The signed-in user with their organizations (cached; shared by every screen). */
export function useCurrentUser() {
  return useGetCurrentUser({ query: { staleTime: 60_000 } });
}

/**
 * The organization named by the `:orgSlug` route parameter, if the user belongs to it.
 * Routes use slugs for readable URLs; API calls use the id.
 */
export function useCurrentOrganization(): {
  organization: MyOrganizationResponse | undefined;
  isPending: boolean;
} {
  const { orgSlug } = useParams();
  const { data, isPending } = useCurrentUser();
  return {
    organization: data?.organizations.find((organization) => organization.slug === orgSlug),
    isPending,
  };
}

/** Remembers the last visited organization, so the app reopens where the user left off. */
export function rememberOrganization(slug: string) {
  try {
    localStorage.setItem(LAST_ORGANIZATION_KEY, slug);
  } catch {
    // Storage may be unavailable (private mode); remembering is only a convenience.
  }
}

export function lastOrganization(): string | null {
  try {
    return localStorage.getItem(LAST_ORGANIZATION_KEY);
  } catch {
    return null;
  }
}
