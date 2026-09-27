import { Navigate, Outlet, useLocation } from 'react-router';
import { useSession } from '@/shared/auth';

/** Shown while the stored session is being restored on page load. */
function RestoringSession() {
  return (
    <div className="flex min-h-svh items-center justify-center" aria-busy="true">
      <p className="text-sm text-muted-foreground">Loading…</p>
    </div>
  );
}

/** Protects child routes: anonymous visitors are sent to sign in and brought back afterwards. */
export function RequireAuth() {
  const { status } = useSession();
  const location = useLocation();

  if (status === 'unknown') return <RestoringSession />;
  if (status === 'anonymous') {
    const returnTo = encodeURIComponent(`${location.pathname}${location.search}`);
    return <Navigate to={`/login?returnTo=${returnTo}`} replace />;
  }
  return <Outlet />;
}

/** For sign-in and registration: signed-in users have nothing to do there. */
export function RedirectIfAuthenticated() {
  const { status } = useSession();

  if (status === 'unknown') return <RestoringSession />;
  if (status === 'authenticated') return <Navigate to="/" replace />;
  return <Outlet />;
}
