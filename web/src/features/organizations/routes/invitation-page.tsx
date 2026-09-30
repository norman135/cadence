import { useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useParams } from 'react-router';
import { errorMessage } from '@/shared/api/api-error';
import {
  getGetCurrentUserQueryKey,
  useAcceptInvitation,
  useGetInvitation,
} from '@/shared/api/generated/endpoints';
import { useSession } from '@/shared/auth';
import { Alert } from '@/shared/ui/alert';
import { Button } from '@/shared/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card';

/** The target of an invitation email. Anyone with the link sees the preview; accepting needs an account. */
export function InvitationPage() {
  const { token = '' } = useParams();
  const { status } = useSession();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const invitation = useGetInvitation(token, { query: { retry: false } });
  const accept = useAcceptInvitation();

  const returnTo = encodeURIComponent(`/invitations/${token}`);

  const onAccept = async () => {
    const joined = await accept.mutateAsync({ token });
    await queryClient.invalidateQueries({ queryKey: getGetCurrentUserQueryKey() });
    await navigate(`/${joined.slug}`, { replace: true });
  };

  if (invitation.isPending) {
    return <p className="text-sm text-muted-foreground">Loading invitation…</p>;
  }

  if (invitation.isError) {
    return (
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle>Invitation not found</CardTitle>
          <CardDescription>The link may be mistyped, revoked or already used.</CardDescription>
        </CardHeader>
      </Card>
    );
  }

  const {
    organizationName,
    invitedByName,
    email,
    role,
    status: invitationStatus,
  } = invitation.data;

  return (
    <Card className="w-full max-w-md">
      <CardHeader>
        <CardTitle className="text-xl">Join {organizationName}</CardTitle>
        <CardDescription>
          {invitedByName} invited <strong className="text-foreground">{email}</strong> to join as{' '}
          {role.toLowerCase()}.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {invitationStatus !== 'Pending' ? (
          <Alert>This invitation is {invitationStatus.toLowerCase()}. Ask for a new one.</Alert>
        ) : status === 'authenticated' ? (
          <>
            {accept.isError && <Alert variant="destructive">{errorMessage(accept.error)}</Alert>}
            <Button disabled={accept.isPending} onClick={() => void onAccept()}>
              Accept invitation
            </Button>
          </>
        ) : (
          <>
            <Button asChild>
              <Link to={`/register?email=${encodeURIComponent(email)}&returnTo=${returnTo}`}>
                Create an account to join
              </Link>
            </Button>
            <Button variant="outline" asChild>
              <Link to={`/login?returnTo=${returnTo}`}>I already have an account</Link>
            </Button>
          </>
        )}
      </CardContent>
    </Card>
  );
}
