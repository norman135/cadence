import { zodResolver } from '@hookform/resolvers/zod';
import { useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { useNavigate } from 'react-router';
import { z } from 'zod';
import { getGetCurrentUserQueryKey, useCreateOrganization } from '@/shared/api/generated/endpoints';
import { applyServerErrors } from '@/shared/forms/server-errors';
import { Alert } from '@/shared/ui/alert';
import { Button } from '@/shared/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card';
import { TextField } from '@/shared/ui/text-field';
import { useCurrentUser } from '@/shared/workspace';
import { organizationNameSchema } from '../schemas';

const schema = z.object({ name: organizationNameSchema });
type Values = z.infer<typeof schema>;

/** Onboarding, and "new organization" from the switcher. */
export function CreateOrganizationPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const create = useCreateOrganization();
  const { data: me } = useCurrentUser();

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Values>({ resolver: zodResolver(schema) });

  const onSubmit = handleSubmit(async (values) => {
    try {
      const organization = await create.mutateAsync({ data: values });
      await queryClient.invalidateQueries({ queryKey: getGetCurrentUserQueryKey() });
      await navigate(`/${organization.slug}`);
    } catch (error) {
      applyServerErrors(error, setError, ['name']);
    }
  });

  return (
    <Card className="w-full max-w-md">
      <CardHeader>
        <CardTitle className="text-xl">
          {me?.organizations.length === 0 ? `Welcome, ${me.displayName}!` : 'New organization'}
        </CardTitle>
        <CardDescription>
          An organization is your team's workspace. You can invite people once it's created.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={(event) => void onSubmit(event)} className="flex flex-col gap-4" noValidate>
          {errors.root?.server && <Alert variant="destructive">{errors.root.server.message}</Alert>}
          <TextField
            label="Organization name"
            placeholder="Acme Inc."
            error={errors.name?.message}
            {...register('name')}
          />
          <Button type="submit" disabled={isSubmitting}>
            Create organization
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}
