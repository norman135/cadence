import {
  House,
  Menu,
  Monitor,
  Moon,
  Search,
  Settings,
  Sun,
  UserRound,
  Users,
  type LucideIcon,
} from 'lucide-react';
import { lazy, Suspense, useEffect, useState, type ReactNode } from 'react';
import { Link, NavLink, Outlet, useLocation, useMatches } from 'react-router';
import { OrganizationSwitcher } from '../components/organization-switcher';
import { NotFoundPage } from '../routes/not-found-page';
import { useSignOut } from '@/shared/auth';
import { cn } from '@/shared/lib/utils';
import { useTheme, type ThemePreference } from '@/shared/theme';
import { Avatar } from '@/shared/ui/avatar';
import { Button } from '@/shared/ui/button';
import { Dialog, DialogContent, DialogTitle } from '@/shared/ui/dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger,
  DropdownMenuTrigger,
} from '@/shared/ui/dropdown-menu';
import { Kbd } from '@/shared/ui/kbd';
import { Toaster } from '@/shared/ui/toaster';
import { Tooltip, TooltipProvider } from '@/shared/ui/tooltip';
import { rememberOrganization, useCurrentOrganization, useCurrentUser } from '@/shared/workspace';

// The palette (and cmdk) is downloaded the first time it is opened.
const CommandPalette = lazy(() => import('../command-palette'));

/** Route handles may name the page for the top bar: `handle: { title: 'Members' }`. */
function usePageTitle() {
  const matches = useMatches();
  for (const match of [...matches].reverse()) {
    const handle = match.handle as { title?: string } | undefined;
    if (handle?.title) return handle.title;
  }
  return undefined;
}

/**
 * The signed-in app (design board 06): a sidebar on the canvas, and the page on a floating
 * panel. Below 768 px the sidebar becomes a drawer and a tab bar takes over navigation.
 */
export function AppShell() {
  const { organization, isPending } = useCurrentOrganization();
  const [paletteOpen, setPaletteOpen] = useState(false);
  // The drawer belongs to the page it was opened on, so navigating closes it.
  const [drawerPath, setDrawerPath] = useState<string | null>(null);
  const title = usePageTitle();
  const location = useLocation();
  const drawerOpen = drawerPath === location.pathname;
  const setDrawerOpen = (open: boolean) => {
    setDrawerPath(open ? location.pathname : null);
  };

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
  const openPalette = () => {
    setPaletteOpen(true);
  };

  return (
    <TooltipProvider>
      <div className="flex min-h-svh bg-canvas">
        <aside className="sticky top-0 hidden h-svh w-[248px] shrink-0 md:flex">
          <Sidebar base={base} onSearch={openPalette} />
        </aside>

        <Dialog open={drawerOpen} onOpenChange={setDrawerOpen}>
          <DialogContent
            aria-describedby={undefined}
            className="top-0 left-0 h-svh w-[280px] max-w-none translate-x-0 rounded-none rounded-r-xl border-y-0 border-l-0 bg-canvas p-0"
          >
            <DialogTitle className="sr-only">Navigation</DialogTitle>
            <Sidebar base={base} onSearch={openPalette} />
          </DialogContent>
        </Dialog>

        <div className="flex min-w-0 flex-1 flex-col pb-16 md:my-2 md:mr-2 md:pb-0">
          <div className="flex flex-1 flex-col bg-background md:rounded-lg md:border">
            <header className="sticky top-0 z-10 flex h-[52px] shrink-0 items-center gap-3 border-b bg-background/95 px-3 backdrop-blur md:rounded-t-lg md:px-4">
              <Button
                variant="ghost"
                size="icon"
                className="md:hidden"
                aria-label="Open navigation"
                onClick={() => {
                  setDrawerOpen(true);
                }}
              >
                <Menu />
              </Button>
              <nav aria-label="Breadcrumb" className="flex min-w-0 items-center gap-2 font-medium">
                <span className="truncate text-muted-foreground max-sm:hidden">
                  {organization.name}
                </span>
                {title && (
                  <>
                    <span className="text-subtle-foreground max-sm:hidden">/</span>
                    <span className="truncate">{title}</span>
                  </>
                )}
              </nav>
              <button
                type="button"
                onClick={openPalette}
                className="ml-auto hidden h-[30px] w-72 cursor-default items-center gap-2 rounded-md border bg-sunken px-2.5 text-sm text-subtle-foreground outline-none hover:border-input focus-visible:outline-2 focus-visible:outline-ring sm:flex"
              >
                <Search className="size-4" />
                Search or jump to…
                <span className="ml-auto flex gap-1">
                  <Kbd>Ctrl</Kbd>
                  <Kbd>K</Kbd>
                </span>
              </button>
            </header>

            <main className="flex-1 px-4 py-6 sm:px-8 sm:py-8">
              <Outlet />
            </main>
          </div>
        </div>

        <MobileTabBar base={base} onSearch={openPalette} />

        {paletteOpen && (
          <Suspense fallback={null}>
            <CommandPalette open onOpenChange={setPaletteOpen} />
          </Suspense>
        )}
        <Toaster />
      </div>
    </TooltipProvider>
  );
}

function Sidebar({ base, onSearch }: { base: string; onSearch: () => void }) {
  return (
    <div className="flex h-full w-full flex-col gap-0.5 px-2.5 py-3">
      <div className="mb-2 flex items-center gap-1">
        <OrganizationSwitcher />
        <Tooltip content="Search">
          <Button variant="ghost" size="icon-sm" aria-label="Search" onClick={onSearch}>
            <Search />
          </Button>
        </Tooltip>
      </div>

      <nav aria-label="Main" className="flex flex-col gap-0.5">
        <SidebarLink to={base} end icon={House}>
          My work
        </SidebarLink>
      </nav>

      <p className="mt-4 mb-1 px-2 text-xs font-medium text-subtle-foreground">Projects</p>
      <p className="px-2 py-1 text-[13px] text-subtle-foreground">No projects yet</p>

      <p className="mt-4 mb-1 px-2 text-xs font-medium text-subtle-foreground">Team</p>
      <nav aria-label="Team" className="flex flex-col gap-0.5">
        <SidebarLink to={`${base}/settings/members`} icon={Users}>
          Members
        </SidebarLink>
        <SidebarLink to={`${base}/settings/organization`} icon={Settings}>
          Settings
        </SidebarLink>
      </nav>

      <div className="mt-auto pt-2">
        <UserMenu base={base} />
      </div>
    </div>
  );
}

function SidebarLink({
  to,
  end,
  icon: Icon,
  children,
}: {
  to: string;
  end?: boolean;
  icon: LucideIcon;
  children: ReactNode;
}) {
  return (
    <NavLink
      to={to}
      end={end ?? false}
      className={({ isActive }) =>
        cn(
          'flex h-[30px] items-center gap-2.5 rounded-md px-2 font-medium outline-none focus-visible:outline-2 focus-visible:outline-ring',
          isActive
            ? 'bg-background text-foreground shadow-sm dark:bg-muted dark:shadow-none'
            : 'text-muted-foreground hover:bg-accent/70 hover:text-foreground',
        )
      }
    >
      <Icon className="size-4" strokeWidth={1.75} />
      {children}
    </NavLink>
  );
}

const themeOptions: { value: ThemePreference; label: string; icon: LucideIcon }[] = [
  { value: 'system', label: 'System', icon: Monitor },
  { value: 'light', label: 'Light', icon: Sun },
  { value: 'dark', label: 'Dark', icon: Moon },
];

function UserMenu({ base }: { base: string }) {
  const { data: me } = useCurrentUser();
  const { organization } = useCurrentOrganization();
  const { preference, setPreference } = useTheme();
  const signOut = useSignOut();
  if (!me) return null;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        aria-label="Account menu"
        className="flex w-full cursor-default items-center gap-2.5 rounded-md p-2 text-left outline-none hover:bg-accent focus-visible:outline-2 focus-visible:outline-ring data-[state=open]:bg-accent"
      >
        <Avatar name={me.displayName} />
        <span className="min-w-0 leading-tight">
          <span className="block truncate text-[13px] font-medium">{me.displayName}</span>
          <span className="block truncate text-xs text-muted-foreground">{organization?.role}</span>
        </span>
      </DropdownMenuTrigger>
      <DropdownMenuContent side="top" align="start" className="w-60">
        <DropdownMenuLabel className="text-foreground">
          <span className="block text-sm font-medium">{me.displayName}</span>
          <span className="block text-xs font-normal text-muted-foreground">{me.email}</span>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem asChild>
          <Link to={`${base}/settings/profile`}>
            <UserRound />
            Profile
          </Link>
        </DropdownMenuItem>
        <DropdownMenuSub>
          <DropdownMenuSubTrigger>
            <Sun />
            Theme
          </DropdownMenuSubTrigger>
          <DropdownMenuSubContent>
            <DropdownMenuRadioGroup
              value={preference}
              onValueChange={(value) => {
                setPreference(value as ThemePreference);
              }}
            >
              {themeOptions.map(({ value, label, icon: Icon }) => (
                <DropdownMenuRadioItem key={value} value={value}>
                  <Icon />
                  {label}
                </DropdownMenuRadioItem>
              ))}
            </DropdownMenuRadioGroup>
          </DropdownMenuSubContent>
        </DropdownMenuSub>
        <DropdownMenuSeparator />
        <DropdownMenuItem onSelect={() => void signOut()}>Sign out</DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function MobileTabBar({ base, onSearch }: { base: string; onSearch: () => void }) {
  const tab =
    'flex flex-col items-center justify-center gap-0.5 text-[10px] font-medium text-subtle-foreground outline-none focus-visible:text-foreground [&_svg]:size-5';
  const active = ({ isActive }: { isActive: boolean }) => cn(tab, isActive && 'text-primary');

  return (
    <nav
      aria-label="Tabs"
      className="fixed inset-x-0 bottom-0 z-20 grid h-16 grid-cols-4 border-t bg-background pb-[env(safe-area-inset-bottom)] md:hidden"
    >
      <NavLink to={base} end className={active}>
        <House />
        My work
      </NavLink>
      <NavLink to={`${base}/settings/members`} className={active}>
        <Users />
        Members
      </NavLink>
      <button type="button" className={cn(tab, 'cursor-default')} onClick={onSearch}>
        <Search />
        Search
      </button>
      <NavLink to={`${base}/settings/profile`} className={active}>
        <UserRound />
        Profile
      </NavLink>
    </nav>
  );
}
