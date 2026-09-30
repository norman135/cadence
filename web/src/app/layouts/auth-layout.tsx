import { Link, Outlet } from 'react-router';
import { CadenceLogo } from '@/shared/ui/cadence-logo';

/** Centered layout for the account pages. */
export function AuthLayout() {
  return (
    <div className="flex min-h-svh flex-col items-center justify-center gap-8 bg-muted/40 p-6">
      <Link to="/" className="flex items-center gap-2 text-lg font-semibold tracking-tight">
        <CadenceLogo className="size-8" />
        Cadence
      </Link>
      <Outlet />
    </div>
  );
}
