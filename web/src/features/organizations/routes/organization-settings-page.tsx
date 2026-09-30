import { zodResolver } from '@hookform/resolvers/zod';
import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { useNavigate } from 'react-router';
import { toast } from 'sonner';
import { z } from 'zod/mini';
import { errorMessage } from '@/shared/api/api-error';
import {
  getGetCurrentUserQueryKey,
  useDeleteOrganization,
  useRenameOrganization,
} from '@/shared/api/generated/endpoints';
import { applyServerErrors } from '@/shared/forms/server-errors';
import { Alert } from '@/shared/ui/alert';
import { Button } from '@/shared/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card';
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogTitle,
  DialogTrigger,
} from '@/shared/ui/dialog';
import { TextField } from '@/shared/ui/text-field';
import { can, useCurrentOrganization } from '@/shared/workspace';
import { organizationNameSchema } from '../schemas';

const schema = z.object({ name: organizationNameSchema });
type Values = z.infer<typeof schema>;

export function OrganizationSettingsPage() {
  const { organization } = useCurrentOrganization();
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const rename = useRenameOrganization();
  const remove = useDeleteOrganization();
  const [confirmation, setConfirmation] = useState('');

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting, isDirty },
  } = useForm<Values>({
    resolver: zodResolver(schema),
    values: { name: organization?.name ?? '' },
  });

  if (!organization) return null;
  const canUpdate = can(organization.role, 'organization.update');

  const onRename = handleSubmit(async (values) => {
    try {
      await rename.mutateAsync({ organizationId: organization.id, data: values });
      await queryClient.invalidateQueries({ queryKey: getGetCurrentUserQueryKey() });
      toast.success('Organization renamed.');
    } catch (error) {
      applyServerErrors(error, setError, ['name']);
    }
  });

  const onDelete = async () => {
    try {
      await remove.mutateAsync({ organizationId: organization.id });
      await queryClient.invalidateQueries({ queryKey: getGetCurrentUserQueryKey() });
      toast.success(`${organization.name} was deleted.`);
      await navigate('/', { replace: true });
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  return (
    <div className="flex max-w-2xl flex-col gap-6">
      <h1 className="text-2xl font-semibold tracking-tight">Organization settings</h1>

      <Card>
        <CardHeader>
          <CardTitle>General</CardTitle>
          <CardDescription>
            The address <span className="font-mono">/{organization.slug}</span> stays the same when
            you rename, so links keep working.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form
            onSubmit={(event) => void onRename(event)}
            className="flex flex-col gap-4"
            noValidate
          >
            {errors.root?.server && (
              <Alert variant="destructive">{errors.root.server.message}</Alert>
            )}
            <TextField
              label="Name"
              disabled={!canUpdate}
              error={errors.name?.message}
              {...register('name')}
            />
            {canUpdate && (
              <Button type="submit" className="self-start" disabled={!isDirty || isSubmitting}>
                Save
              </Button>
            )}
          </form>
        </CardContent>
      </Card>

      {can(organization.role, 'organization.delete') && (
        <Card className="border-destructive/40">
          <CardHeader>
            <CardTitle>Delete organization</CardTitle>
            <CardDescription>
              Permanently deletes the organization, its members and invitations. This cannot be
              undone.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Dialog>
              <DialogTrigger asChild>
                <Button variant="destructive">Delete organization</Button>
              </DialogTrigger>
              <DialogContent className="flex flex-col gap-4">
                <DialogTitle>Delete {organization.name}?</DialogTitle>
                <DialogDescription>
                  Type <strong className="text-foreground">{organization.name}</strong> to confirm.
                </DialogDescription>
                <TextField
                  label="Organization name"
                  value={confirmation}
                  onChange={(event) => {
                    setConfirmation(event.target.value);
                  }}
                />
                <div className="flex justify-end gap-2">
                  <DialogClose asChild>
                    <Button variant="outline">Cancel</Button>
                  </DialogClose>
                  <Button
                    variant="destructive"
                    disabled={confirmation !== organization.name || remove.isPending}
                    onClick={() => void onDelete()}
                  >
                    Delete forever
                  </Button>
                </div>
              </DialogContent>
            </Dialog>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
