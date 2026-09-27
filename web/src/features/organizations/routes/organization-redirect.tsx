import { Navigate } from 'react-router';
import { lastOrganization, useCurrentUser } from '@/shared/workspace';

/** Where a signed-in user lands: their last organization, their first one, or onboarding. */
export function OrganizationRedirect() {
  const { data, isPending } = useCurrentUser();

  if (isPending || !data) {
    return <p className="p-6 text-sm text-muted-foreground">Loading…</p>;
  }

  const remembered = lastOrganization();
  const target =
    data.organizations.find((organization) => organization.slug === remembered) ??
    data.organizations[0];

  return <Navigate to={target ? `/${target.slug}` : '/welcome'} replace />;
}
