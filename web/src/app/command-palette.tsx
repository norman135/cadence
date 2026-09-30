import { Command } from 'cmdk';
import { House, Monitor, Moon, Search, Settings, Sun, UserRound, Users } from 'lucide-react';
import type { ReactNode } from 'react';
import { useNavigate } from 'react-router';
import { OrganizationTile } from './components/organization-switcher';
import { useTheme } from '@/shared/theme';
import { Dialog, DialogContent, DialogTitle } from '@/shared/ui/dialog';
import { Kbd } from '@/shared/ui/kbd';
import { useCurrentOrganization, useCurrentUser } from '@/shared/workspace';

const group =
  '[&_[cmdk-group-heading]]:px-2.5 [&_[cmdk-group-heading]]:pt-2.5 [&_[cmdk-group-heading]]:pb-1 [&_[cmdk-group-heading]]:text-xs [&_[cmdk-group-heading]]:font-medium [&_[cmdk-group-heading]]:text-subtle-foreground';

/**
 * Ctrl+K: one box for pages, organizations and actions (design board 10). Later milestones add
 * issues, projects and people. Loaded on first use, so cmdk isn't in the initial bundle.
 */
export default function CommandPalette({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const navigate = useNavigate();
  const { data: me } = useCurrentUser();
  const { organization } = useCurrentOrganization();
  const { resolved, setPreference } = useTheme();

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
        <Command label="Command palette" className="flex flex-col">
          <div className="flex h-[52px] items-center gap-2.5 border-b px-4">
            <Search className="size-[18px] text-muted-foreground" aria-hidden="true" />
            <Command.Input
              autoFocus
              placeholder="Search or type a command…"
              className="h-full flex-1 bg-transparent text-base outline-none placeholder:text-subtle-foreground"
            />
            <Kbd>Esc</Kbd>
          </div>
          <Command.List className="max-h-[360px] overflow-y-auto px-1.5 pb-1.5">
            <Command.Empty className="px-4 py-8 text-center text-sm text-muted-foreground">
              No results.
            </Command.Empty>

            {organization && (
              <Command.Group heading="Go to" className={group}>
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
}: {
  icon: ReactNode;
  children: ReactNode;
  onSelect: () => void;
}) {
  return (
    <Command.Item
      onSelect={onSelect}
      className="flex h-[38px] cursor-default items-center gap-2.5 rounded-lg px-2.5 text-sm text-foreground data-[selected=true]:bg-accent data-[selected=true]:shadow-[inset_2px_0_0_var(--primary)] [&_svg]:size-4 [&_svg]:text-muted-foreground"
    >
      {icon}
      {children}
    </Command.Item>
  );
}
