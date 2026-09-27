import { Link, Outlet } from 'react-router';
import { CadenceLogo } from '@/shared/ui/cadence-logo';

export function RootLayout() {
  return (
    <div className="flex min-h-svh flex-col">
      <header className="border-b">
        <div className="mx-auto flex h-14 max-w-5xl items-center px-4 sm:px-6">
          <Link to="/" className="flex items-center gap-2 font-semibold tracking-tight">
            <CadenceLogo className="size-7" />
            Cadence
          </Link>
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
