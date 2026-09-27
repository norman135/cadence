import { Link, Outlet } from 'react-router';
import { useSession, useSignOut } from '@/shared/auth';
import { Button } from '@/shared/ui/button';
import { CadenceLogo } from '@/shared/ui/cadence-logo';

function SessionActions() {
  const { status } = useSession();
  const signOut = useSignOut();

  if (status === 'unknown') return null;

  if (status === 'authenticated') {
    return (
      <Button variant="ghost" size="sm" onClick={() => void signOut()}>
        Sign out
      </Button>
    );
  }

  return (
    <div className="flex items-center gap-2">
      <Button variant="ghost" size="sm" asChild>
        <Link to="/login">Sign in</Link>
      </Button>
      <Button size="sm" asChild>
        <Link to="/register">Get started</Link>
      </Button>
    </div>
  );
}

export function RootLayout() {
  return (
    <div className="flex min-h-svh flex-col">
      <header className="border-b">
        <div className="mx-auto flex h-14 max-w-5xl items-center justify-between px-4 sm:px-6">
          <Link to="/" className="flex items-center gap-2 font-semibold tracking-tight">
            <CadenceLogo className="size-7" />
            Cadence
          </Link>
          <SessionActions />
        </div>
      </header>

      <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-10 sm:px-6">
        <Outlet />
      </main>

      <footer className="border-t">
        <div className="mx-auto max-w-5xl px-4 py-4 text-sm text-muted-foreground sm:px-6">
          © {new Date().getFullYear()} Norman Mico · MIT License
        </div>
      </footer>
    </div>
  );
}
