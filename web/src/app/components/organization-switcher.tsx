import { Check, ChevronsUpDown, Plus } from 'lucide-react';
import { Link } from 'react-router';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/shared/ui/dropdown-menu';
import { useCurrentOrganization, useCurrentUser } from '@/shared/workspace';

/** The organization's tile: its initial on the Ember gradient. */
export function OrganizationTile({ name }: { name: string }) {
  return (
    <span
      aria-hidden="true"
      className="grid size-6 shrink-0 place-items-center rounded-[7px] bg-linear-135 from-[#ff7b45] to-[#d44c1c] text-xs font-bold text-white"
    >
      {name.trim().charAt(0).toUpperCase() || '?'}
    </span>
  );
}

/** Switches between the user's organizations; the choice lives in the URL (`/:orgSlug`). */
export function OrganizationSwitcher() {
  const { data } = useCurrentUser();
  const { organization: current } = useCurrentOrganization();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        aria-label="Switch organization"
        className="flex h-9 min-w-0 flex-1 cursor-default items-center gap-2.5 rounded-md px-2 font-semibold tracking-[-0.01em] outline-none hover:bg-accent focus-visible:outline-2 focus-visible:outline-ring data-[state=open]:bg-accent"
      >
        {current && <OrganizationTile name={current.name} />}
        <span className="truncate">{current?.name ?? 'Select organization'}</span>
        <ChevronsUpDown className="ml-auto size-3.5 shrink-0 text-subtle-foreground" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="w-64">
        <DropdownMenuLabel>Organizations</DropdownMenuLabel>
        {data?.organizations.map((organization) => (
          <DropdownMenuItem key={organization.id} asChild>
            <Link to={`/${organization.slug}`}>
              <OrganizationTile name={organization.name} />
              <span className="flex-1 truncate">{organization.name}</span>
              {organization.id === current?.id && (
                <Check aria-label="Current" className="text-primary" />
              )}
            </Link>
          </DropdownMenuItem>
        ))}
        <DropdownMenuSeparator />
        <DropdownMenuItem asChild>
          <Link to="/welcome">
            <Plus />
            New organization
          </Link>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
