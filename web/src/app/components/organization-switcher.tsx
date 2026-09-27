import { Check, ChevronsUpDown, Plus } from 'lucide-react';
import { Link } from 'react-router';
import { Button } from '@/shared/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/shared/ui/dropdown-menu';
import { useCurrentOrganization, useCurrentUser } from '@/shared/workspace';

/** Switches between the user's organizations; the choice lives in the URL (`/:orgSlug`). */
export function OrganizationSwitcher() {
  const { data } = useCurrentUser();
  const { organization: current } = useCurrentOrganization();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          className="w-full justify-between px-2"
          aria-label="Switch organization"
        >
          <span className="truncate font-semibold">{current?.name ?? 'Select organization'}</span>
          <ChevronsUpDown className="text-muted-foreground" aria-hidden="true" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="w-60">
        <DropdownMenuLabel>Organizations</DropdownMenuLabel>
        {data?.organizations.map((organization) => (
          <DropdownMenuItem key={organization.id} asChild>
            <Link to={`/${organization.slug}`}>
              <span className="flex-1 truncate">{organization.name}</span>
              {organization.id === current?.id && <Check aria-label="Current" />}
            </Link>
          </DropdownMenuItem>
        ))}
        <DropdownMenuSeparator />
        <DropdownMenuItem asChild>
          <Link to="/welcome">
            <Plus aria-hidden="true" />
            New organization
          </Link>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
