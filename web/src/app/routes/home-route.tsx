import { lazy } from 'react';
import { useSession } from '@/shared/auth';

const HomePage = lazy(async () => ({ default: (await import('@/features/home')).HomePage }));
const OrganizationRedirect = lazy(async () => ({
  default: (await import('@/features/organizations/routes/organization-redirect'))
    .OrganizationRedirect,
}));

/** `/`: the product page for visitors, the user's organization for signed-in users. */
export function HomeRoute() {
  const { status } = useSession();

  if (status === 'unknown') return null;
  return status === 'authenticated' ? <OrganizationRedirect /> : <HomePage />;
}
