import { Command } from 'cmdk';
import { Building2, Home, Settings, UserRound, Users } from 'lucide-react';
import type { ReactNode } from 'react';
import { useNavigate } from 'react-router';
import { Dialog, DialogContent, DialogTitle } from '@/shared/ui/dialog';
import { useCurrentOrganization, useCurrentUser } from '@/shared/workspace';

/**
 * ⌘K / Ctrl+K: jump to any page or organization. This is the skeleton later milestones extend
 * with projects, issues and actions. Loaded on first use, so cmdk isn't in the initial bundle.
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

  const go = (path: string) => {
    onOpenChange(false);
    void navigate(path);
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="overflow-hidden p-0" aria-describedby={undefined}>
        <DialogTitle className="sr-only">Command palette</DialogTitle>
        <Command label="Command palette" className="flex flex-col">
          <Command.Input
            autoFocus
            placeholder="Type a command or search…"
            className="h-12 border-b bg-transparent px-4 text-sm outline-none placeholder:text-muted-foreground"
          />
          <Command.List className="max-h-80 overflow-y-auto p-2">
            <Command.Empty className="p-4 text-center text-sm text-muted-foreground">
              No results.
            </Command.Empty>

            {organization && (
              <Command.Group heading="Navigation" className="text-xs text-muted-foreground">
                <Item
                  icon={<Home />}
                  onSelect={() => {
                    go(`/${organization.slug}`);
                  }}
                >
                  Home
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
              <Command.Group
                heading="Switch organization"
                className="text-xs text-muted-foreground"
              >
                {me.organizations
                  .filter((candidate) => candidate.id !== organization?.id)
                  .map((candidate) => (
                    <Item
                      key={candidate.id}
                      icon={<Building2 />}
                      onSelect={() => {
                        go(`/${candidate.slug}`);
                      }}
                    >
                      {candidate.name}
                    </Item>
                  ))}
              </Command.Group>
            )}
          </Command.List>
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
      className="flex cursor-default items-center gap-2 rounded-md px-2 py-2 text-sm text-foreground data-[selected=true]:bg-accent data-[selected=true]:text-accent-foreground [&_svg]:size-4 [&_svg]:text-muted-foreground"
    >
      {icon}
      {children}
    </Command.Item>
  );
}
