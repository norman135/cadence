import { Command } from 'cmdk';
import {
  CircleDot,
  House,
  Monitor,
  Moon,
  Plus,
  Search,
  Settings,
  Sun,
  UserRound,
  Users,
} from 'lucide-react';
import { useEffect, useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router';
import { OrganizationTile } from './components/organization-switcher';
import { ProjectTile } from './components/project-tile';
import { useListIssues } from '@/shared/api/generated/endpoints';
import { useTheme } from '@/shared/theme';
import { Dialog, DialogContent, DialogTitle } from '@/shared/ui/dialog';
import { Kbd } from '@/shared/ui/kbd';
import { useCurrentOrganization, useCurrentUser, useProjects } from '@/shared/workspace';

const group =
  '[&_[cmdk-group-heading]]:px-2.5 [&_[cmdk-group-heading]]:pt-2.5 [&_[cmdk-group-heading]]:pb-1 [&_[cmdk-group-heading]]:text-xs [&_[cmdk-group-heading]]:font-medium [&_[cmdk-group-heading]]:text-muted-foreground';

const issueValue = (issue: { key: string; title: string } | undefined) =>
  issue ? `${issue.key} ${issue.title}` : '';

/** Waits until typing pauses, so searching sends one request rather than one per keystroke. */
function useDebounced(value: string, delay: number) {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebounced(value);
    }, delay);
    return () => {
      clearTimeout(timer);
    };
  }, [value, delay]);
  return debounced;
}

/**
 * Ctrl+K: one box for issues, projects, pages, organizations and actions (design board 10).
 * Issues are searched on the server by title or exact key. Loaded on first use, so cmdk isn't in
 * the initial bundle.
 */
export default function CommandPalette({
  open,
  onOpenChange,
  onNewIssue,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onNewIssue?: () => void;
}) {
  const navigate = useNavigate();
  const { data: me } = useCurrentUser();
  const { organization } = useCurrentOrganization();
  const { data: projects } = useProjects();
  const { resolved, setPreference } = useTheme();
  const [search, setSearch] = useState('');
  const term = useDebounced(search.trim(), 200);
  const { data: issues } = useListIssues(
    organization?.id ?? '',
    { q: term, limit: 8 },
    { query: { enabled: !!organization && term.length >= 2, staleTime: 10_000 } },
  );
  const foundIssues = term.length >= 2 && !!issues && issues.items.length > 0;

  // cmdk picks its selection as the user types, before results arrive, so select the first found
  // issue once it shows up; otherwise Enter would do nothing.
  const [selected, setSelected] = useState('');
  const firstIssue = foundIssues ? issueValue(issues.items[0]) : null;
  const [lastFirstIssue, setLastFirstIssue] = useState<string | null>(null);
  if (firstIssue !== lastFirstIssue) {
    setLastFirstIssue(firstIssue);
    if (firstIssue) setSelected(firstIssue);
  }

  const run = (action: () => void) => {
    onOpenChange(false);
    action();
  };
  const go = (path: string) => {
    run(() => void navigate(path));
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        className="max-w-[560px] overflow-hidden rounded-xl p-0"
        aria-describedby={undefined}
      >
        <DialogTitle className="sr-only">Command palette</DialogTitle>
        <Command
          label="Command palette"
          className="flex flex-col"
          value={selected}
          onValueChange={setSelected}
        >
          <div className="flex h-[52px] items-center gap-2.5 border-b px-4">
            <Search className="size-[18px] text-muted-foreground" aria-hidden="true" />
            <Command.Input
              autoFocus
              value={search}
              onValueChange={setSearch}
              placeholder="Search or type a command…"
              className="h-full flex-1 bg-transparent text-base outline-none placeholder:text-subtle-foreground"
            />
            <Kbd>Esc</Kbd>
          </div>
          <Command.List className="max-h-[360px] overflow-y-auto px-1.5 pb-1.5">
            {/* cmdk counts only the items it filters, so found issues must hide "No results" themselves. */}
            {!foundIssues && (
              <Command.Empty className="px-4 py-8 text-center text-sm text-muted-foreground">
                No results.
              </Command.Empty>
            )}

            {organization && foundIssues && (
              <Command.Group heading="Issues" className={group} forceMount>
                {issues.items.map((issue) => (
                  <Item
                    key={issue.id}
                    value={issueValue(issue)}
                    forceMount
                    icon={<CircleDot />}
                    onSelect={() => {
                      go(`/${organization.slug}/issues/${issue.key}`);
                    }}
                  >
                    <span className="w-16 shrink-0 font-mono text-xs text-muted-foreground">
                      {issue.key}
                    </span>
                    <span className="truncate">{issue.title}</span>
                  </Item>
                ))}
              </Command.Group>
            )}

            {organization && onNewIssue && (
              <Command.Group heading="Actions" className={group}>
                <Item
                  icon={<Plus />}
                  onSelect={() => {
                    run(onNewIssue);
                  }}
                >
                  New issue
                </Item>
              </Command.Group>
            )}

            {organization && projects && projects.length > 0 && (
              <Command.Group heading="Projects" className={group}>
                {projects.map((project) => (
                  <Item
                    key={project.id}
                    value={`project ${project.key} ${project.name}`}
                    icon={<ProjectTile projectKey={project.key} />}
                    onSelect={() => {
                      go(`/${organization.slug}/projects/${project.key}`);
                    }}
                  >
                    {project.name}
                  </Item>
                ))}
              </Command.Group>
            )}

            {organization && (
              <Command.Group heading="Go to" className={group}>
                <Item
                  icon={<CircleDot />}
                  onSelect={() => {
                    go(`/${organization.slug}/issues`);
                  }}
                >
                  All issues
                </Item>
                <Item
                  icon={<House />}
                  onSelect={() => {
                    go(`/${organization.slug}`);
                  }}
                >
                  My work
                </Item>
                <Item
                  icon={<Users />}
                  onSelect={() => {
                    go(`/${organization.slug}/settings/members`);
                  }}
                >
                  Members
                </Item>
                <Item
                  icon={<Settings />}
                  onSelect={() => {
                    go(`/${organization.slug}/settings/organization`);
                  }}
                >
                  Organization settings
                </Item>
                <Item
                  icon={<UserRound />}
                  onSelect={() => {
                    go(`/${organization.slug}/settings/profile`);
                  }}
                >
                  Your profile
                </Item>
              </Command.Group>
            )}

            {me && me.organizations.length > 1 && (
              <Command.Group heading="Switch organization" className={group}>
                {me.organizations
                  .filter((candidate) => candidate.id !== organization?.id)
                  .map((candidate) => (
                    <Item
                      key={candidate.id}
                      icon={<OrganizationTile name={candidate.name} />}
                      onSelect={() => {
                        go(`/${candidate.slug}`);
                      }}
                    >
                      {candidate.name}
                    </Item>
                  ))}
              </Command.Group>
            )}

            <Command.Group heading="Theme" className={group}>
              <Item
                icon={resolved === 'dark' ? <Sun /> : <Moon />}
                onSelect={() => {
                  run(() => {
                    setPreference(resolved === 'dark' ? 'light' : 'dark');
                  });
                }}
              >
                Switch to {resolved === 'dark' ? 'light' : 'dark'} theme
              </Item>
              <Item
                icon={<Monitor />}
                onSelect={() => {
                  run(() => {
                    setPreference('system');
                  });
                }}
              >
                Use the system theme
              </Item>
            </Command.Group>
          </Command.List>
          <div className="flex gap-4 border-t px-4 py-2.5 text-xs text-muted-foreground">
            <span className="flex items-center gap-1">
              <Kbd>↑</Kbd>
              <Kbd>↓</Kbd> move
            </span>
            <span className="flex items-center gap-1">
              <Kbd>↵</Kbd> open
            </span>
          </div>
        </Command>
      </DialogContent>
    </Dialog>
  );
}

function Item({
  icon,
  children,
  onSelect,
  value,
  forceMount,
}: {
  icon: ReactNode;
  children: ReactNode;
  onSelect: () => void;
  value?: string;
  forceMount?: boolean;
}) {
  return (
    <Command.Item
      onSelect={onSelect}
      {...(value === undefined ? {} : { value })}
      {...(forceMount ? { forceMount } : {})}
      className="flex h-[38px] items-center gap-2.5 rounded-lg px-2.5 text-sm text-foreground data-[selected=true]:bg-accent data-[selected=true]:shadow-[inset_2px_0_0_var(--primary)] [&_svg]:size-4 [&_svg]:text-muted-foreground"
    >
      {icon}
      {children}
    </Command.Item>
  );
}
