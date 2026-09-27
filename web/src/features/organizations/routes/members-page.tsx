import { zodResolver } from '@hookform/resolvers/zod';
import { useInfiniteQuery, useQueryClient } from '@tanstack/react-query';
import { MoreHorizontal } from 'lucide-react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';
import { errorMessage } from '@/shared/api/api-error';
import {
  getGetCurrentUserQueryKey,
  getListInvitationsQueryKey,
  getListMembersQueryKey,
  listMembers,
  useChangeMemberRole,
  useCreateInvitation,
  useListInvitations,
  useRemoveMember,
  useRevokeInvitation,
} from '@/shared/api/generated/endpoints';
import type { MemberResponse, OrganizationRole } from '@/shared/api/generated/model';
import { useSession } from '@/shared/auth';
import { applyServerErrors } from '@/shared/forms/server-errors';
import { Alert } from '@/shared/ui/alert';
import { Avatar } from '@/shared/ui/avatar';
import { Badge } from '@/shared/ui/badge';
import { Button } from '@/shared/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/shared/ui/dropdown-menu';
import { Label } from '@/shared/ui/label';
import { Select } from '@/shared/ui/select';
import { TextField } from '@/shared/ui/text-field';
import { can, ROLES, useCurrentOrganization, useCurrentUser } from '@/shared/workspace';

const PAGE_SIZE = 50;

export function MembersPage() {
  const { organization } = useCurrentOrganization();
  if (!organization) return null;

  return (
    <div className="flex max-w-3xl flex-col gap-6">
      <h1 className="text-2xl font-semibold tracking-tight">Members</h1>
      {can(organization.role, 'members.invite') && (
        <InviteCard organizationId={organization.id} role={organization.role} />
      )}
      <MemberList organizationId={organization.id} role={organization.role} />
      {can(organization.role, 'members.invite') && (
        <PendingInvitations organizationId={organization.id} />
      )}
    </div>
  );
}

const inviteSchema = z.object({
  email: z.email('Enter a valid email address.'),
  role: z.enum(['Owner', 'Admin', 'Member', 'Guest']),
});
type InviteValues = z.infer<typeof inviteSchema>;

function InviteCard({ organizationId, role }: { organizationId: string; role: OrganizationRole }) {
  const queryClient = useQueryClient();
  const invite = useCreateInvitation();
  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<InviteValues>({
    resolver: zodResolver(inviteSchema),
    defaultValues: { email: '', role: 'Member' },
  });

  const onSubmit = handleSubmit(async (values) => {
    try {
      await invite.mutateAsync({ organizationId, data: values });
      await queryClient.invalidateQueries({ queryKey: getListInvitationsQueryKey(organizationId) });
      toast.success(`Invitation sent to ${values.email}.`);
      // Keep the chosen role for the next invite; clear only the address.
      reset({ email: '', role: values.role });
    } catch (error) {
      applyServerErrors(error, setError, ['email', 'role']);
    }
  });

  // Only owners may create owners.
  const assignable = ROLES.filter((option) => option !== 'Owner' || role === 'Owner');

  return (
    <Card>
      <CardHeader>
        <CardTitle>Invite people</CardTitle>
        <CardDescription>They get an email with a link that works for 7 days.</CardDescription>
      </CardHeader>
      <CardContent>
        <form
          onSubmit={(event) => void onSubmit(event)}
          className="flex flex-col gap-4 sm:flex-row sm:items-start"
          noValidate
        >
          <div className="flex-1">
            <TextField
              label="Email"
              type="email"
              placeholder="teammate@example.com"
              error={errors.email?.message ?? errors.root?.server?.message}
              {...register('email')}
            />
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor="invite-role">Role</Label>
            <Select id="invite-role" {...register('role')}>
              {assignable.map((option) => (
                <option key={option}>{option}</option>
              ))}
            </Select>
          </div>
          <Button type="submit" className="sm:mt-6" disabled={isSubmitting}>
            Send invite
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}

function MemberList({ organizationId, role }: { organizationId: string; role: OrganizationRole }) {
  const members = useInfiniteQuery({
    queryKey: getListMembersQueryKey(organizationId),
    queryFn: ({ pageParam, signal }) =>
      listMembers(
        organizationId,
        { limit: PAGE_SIZE, ...(pageParam ? { after: pageParam } : {}) },
        { signal },
      ),
    initialPageParam: '',
    getNextPageParam: (page) => page.nextCursor ?? undefined,
    enabled: can(role, 'members.read'),
  });

  if (!can(role, 'members.read')) {
    return <Alert>Guests can't see the member list.</Alert>;
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Team</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col">
        {members.isPending && <p className="text-sm text-muted-foreground">Loading members…</p>}
        {members.isError && <Alert variant="destructive">{errorMessage(members.error)}</Alert>}
        <ul className="divide-y">
          {members.data?.pages
            .flatMap((page) => page.items)
            .map((member) => (
              <MemberRow
                key={member.userId}
                member={member}
                organizationId={organizationId}
                actorRole={role}
              />
            ))}
        </ul>
        {members.hasNextPage && (
          <Button
            variant="outline"
            className="mt-4 self-center"
            disabled={members.isFetchingNextPage}
            onClick={() => void members.fetchNextPage()}
          >
            Load more
          </Button>
        )}
      </CardContent>
    </Card>
  );
}

function MemberRow({
  member,
  organizationId,
  actorRole,
}: {
  member: MemberResponse;
  organizationId: string;
  actorRole: OrganizationRole;
}) {
  const queryClient = useQueryClient();
  const { data: me } = useCurrentUser();
  const { status } = useSession();
  const changeRole = useChangeMemberRole();
  const removeMember = useRemoveMember();
  const isSelf = me?.id === member.userId;
  const canManage =
    can(actorRole, 'members.manage') && (member.role !== 'Owner' || actorRole === 'Owner');

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: getListMembersQueryKey(organizationId) });
    if (isSelf) await queryClient.invalidateQueries({ queryKey: getGetCurrentUserQueryKey() });
  };

  const onRoleChange = async (next: OrganizationRole) => {
    try {
      await changeRole.mutateAsync({ organizationId, userId: member.userId, data: { role: next } });
      await refresh();
      toast.success(`${member.displayName} is now ${next.toLowerCase()}.`);
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const onRemove = async () => {
    try {
      await removeMember.mutateAsync({ organizationId, userId: member.userId });
      await refresh();
      toast.success(isSelf ? 'You left the organization.' : `${member.displayName} was removed.`);
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  return (
    <li className="flex items-center gap-3 py-3">
      <Avatar name={member.displayName} />
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-medium">
          {member.displayName} {isSelf && <span className="text-muted-foreground">(you)</span>}
        </p>
        <p className="truncate text-sm text-muted-foreground">{member.email}</p>
      </div>
      {canManage && status === 'authenticated' ? (
        <Select
          aria-label={`Role of ${member.displayName}`}
          value={member.role}
          disabled={changeRole.isPending}
          onChange={(event) => void onRoleChange(event.target.value as OrganizationRole)}
        >
          {ROLES.filter((option) => option !== 'Owner' || actorRole === 'Owner').map((option) => (
            <option key={option}>{option}</option>
          ))}
        </Select>
      ) : (
        <Badge variant="secondary">{member.role}</Badge>
      )}
      {(canManage || isSelf) && (
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="icon" aria-label={`Actions for ${member.displayName}`}>
              <MoreHorizontal />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem variant="destructive" onSelect={() => void onRemove()}>
              {isSelf ? 'Leave organization' : 'Remove from organization'}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      )}
    </li>
  );
}

function PendingInvitations({ organizationId }: { organizationId: string }) {
  const queryClient = useQueryClient();
  const invitations = useListInvitations(organizationId);
  const revoke = useRevokeInvitation();

  if (!invitations.data?.length) return null;

  const onRevoke = async (invitationId: string) => {
    try {
      await revoke.mutateAsync({ organizationId, invitationId });
      await queryClient.invalidateQueries({ queryKey: getListInvitationsQueryKey(organizationId) });
      toast.success('Invitation revoked.');
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>Pending invitations</CardTitle>
      </CardHeader>
      <CardContent>
        <ul className="divide-y">
          {invitations.data.map((invitation) => (
            <li key={invitation.id} className="flex items-center gap-3 py-3">
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium">{invitation.email}</p>
                <p className="text-sm text-muted-foreground">
                  {invitation.role} · invited by {invitation.invitedByName} · expires{' '}
                  {new Date(invitation.expiresAt).toLocaleDateString()}
                </p>
              </div>
              <Button variant="outline" size="sm" onClick={() => void onRevoke(invitation.id)}>
                Revoke
              </Button>
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}
