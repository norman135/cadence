import { FolderKanban } from 'lucide-react';
import { Link } from 'react-router';
import { Button } from '@/shared/ui/button';
import { Card, CardContent } from '@/shared/ui/card';
import { can, useCurrentOrganization } from '@/shared/workspace';

/** The organization's landing page. Projects arrive in M2; until then it points to the next steps. */
export function OrganizationHomePage() {
  const { organization } = useCurrentOrganization();
  if (!organization) return null;

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{organization.name}</h1>
        <p className="text-sm text-muted-foreground">
          You are {organization.role.toLowerCase()} here.
        </p>
      </div>

      <Card>
        <CardContent className="flex flex-col items-center gap-3 py-12 text-center">
          <FolderKanban className="size-10 text-muted-foreground" aria-hidden="true" />
          <h2 className="font-semibold">No projects yet</h2>
          <p className="max-w-sm text-sm text-muted-foreground">
            Projects, issues and boards are coming next. Meanwhile, bring your team in.
          </p>
          {can(organization.role, 'members.invite') && (
            <Button asChild>
              <Link to={`/${organization.slug}/settings/members`}>Invite teammates</Link>
            </Button>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
