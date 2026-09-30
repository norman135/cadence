import { Home, Search, Settings, UserRound, Users } from 'lucide-react';
import { lazy, Suspense, useEffect, useState } from 'react';
import { Link, NavLink, Outlet } from 'react-router';
import { Toaster } from 'sonner';
import { OrganizationSwitcher } from '../components/organization-switcher';
import { useSignOut } from '@/shared/auth';
import { cn } from '@/shared/lib/utils';
import { Avatar } from '@/shared/ui/avatar';
import { Button } from '@/shared/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/shared/ui/dropdown-menu';
import { rememberOrganization, useCurrentOrganization, useCurrentUser } from '@/shared/workspace';
import { NotFoundPage } from '../routes/not-found-page';

// The palette (and cmdk) is downloaded the first time it is opened.
const CommandPalette = lazy(() => import('../command-palette'));

/** The signed-in app: sidebar with organization switcher and navigation, top bar, content. */
export function AppShell() {
  const { organization, isPending } = useCurrentOrganization();
  const [paletteOpen, setPaletteOpen] = useState(false);

  useEffect(() => {
    if (organization) rememberOrganization(organization.slug);
  }, [organization]);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key.toLowerCase() === 'k' && (event.metaKey || event.ctrlKey)) {
        event.preventDefault();
        setPaletteOpen((open) => !open);
      }
    };
    window.addEventListener('keydown', onKeyDown);
    return () => {
      window.removeEventListener('keydown', onKeyDown);
    };
  }, []);

  if (isPending) {
    return <p className="p-6 text-sm text-muted-foreground">Loading…</p>;
  }

  if (!organization) {
    return <NotFoundPage />;
  }

  const base = `/${organization.slug}`;

  return (
    <div className="flex min-h-svh">
      <aside className="hidden w-60 shrink-0 flex-col gap-4 border-r bg-muted/30 p-3 md:flex">
        <OrganizationSwitcher />
        <nav className="flex flex-col gap-1" aria-label="Main">
          <SidebarLink to={base} end icon={<Home />}>
            Home
          </SidebarLink>
          <SidebarLink to={`${base}/settings/members`} icon={<Users />}>
            Members
          </SidebarLink>
          <SidebarLink to={`${base}/settings/organization`} icon={<Settings />}>
            Settings
          </SidebarLink>
        </nav>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-14 items-center justify-between gap-4 border-b px-4">
          <div className="md:hidden">
            <OrganizationSwitcher />
          </div>
          <Button
            variant="secondary"
            size="sm"
            className="w-full max-w-xs justify-start text-muted-foreground"
            onClick={() => {
              setPaletteOpen(true);
            }}
          >
            <Search aria-hidden="true" />
            Search or jump to…
            <kbd className="ml-auto rounded border px-1.5 text-xs">Ctrl K</kbd>
          </Button>
          <UserMenu organizationSlug={organization.slug} />
        </header>

        <main className="flex-1 p-4 sm:p-6">
          <Outlet />
        </main>
      </div>

      {paletteOpen && (
        <Suspense fallback={null}>
          <CommandPalette open onOpenChange={setPaletteOpen} />
        </Suspense>
      )}
      <Toaster richColors closeButton position="bottom-right" />
    </div>
  );
}

function SidebarLink({
  to,
  end,
  icon,
  children,
}: {
  to: string;
  end?: boolean;
  icon: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <NavLink
      to={to}
      end={end ?? false}
      className={({ isActive }) =>
        cn(
          'flex items-center gap-2 rounded-md px-2 py-1.5 text-sm [&_svg]:size-4',
          isActive ? 'bg-accent font-medium text-accent-foreground' : 'hover:bg-accent/60',
        )
      }
    >
      {icon}
      {children}
    </NavLink>
  );
}

function UserMenu({ organizationSlug }: { organizationSlug: string }) {
  const { data: me } = useCurrentUser();
  const signOut = useSignOut();
  if (!me) return null;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon" className="rounded-full" aria-label="Account menu">
          <Avatar name={me.displayName} />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuLabel className="text-foreground">
          <p className="font-medium">{me.displayName}</p>
          <p className="text-xs font-normal text-muted-foreground">{me.email}</p>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem asChild>
          <Link to={`/${organizationSlug}/settings/profile`}>
            <UserRound aria-hidden="true" />
            Profile
          </Link>
        </DropdownMenuItem>
        <DropdownMenuItem onSelect={() => void signOut()}>Sign out</DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
