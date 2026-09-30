import { zodResolver } from '@hookform/resolvers/zod';
import { useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod/mini';
import {
  getGetCurrentUserQueryKey,
  useChangePassword,
  useUpdateProfile,
} from '@/shared/api/generated/endpoints';
import { session } from '@/shared/auth';
import { applyServerErrors } from '@/shared/forms/server-errors';
import { ThemePicker } from '@/shared/theme';
import { Alert } from '@/shared/ui/alert';
import { Button } from '@/shared/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card';
import { TextField } from '@/shared/ui/text-field';
import { useCurrentUser } from '@/shared/workspace';

const profileSchema = z.object({
  displayName: z.string().check(z.trim(), z.minLength(1, 'Enter your name.'), z.maxLength(100)),
});
const passwordSchema = z
  .object({
    currentPassword: z.string().check(z.minLength(1, 'Enter your current password.')),
    newPassword: z.string().check(z.minLength(10, 'Use at least 10 characters.'), z.maxLength(128)),
  })
  .check(
    z.refine((values) => values.newPassword !== values.currentPassword, {
      path: ['newPassword'],
      error: 'The new password must be different.',
    }),
  );

export function ProfileSettingsPage() {
  return (
    <div className="flex max-w-2xl flex-col gap-6">
      <h1 className="text-2xl font-semibold tracking-tight">Your profile</h1>
      <ProfileCard />
      <AppearanceCard />
      <PasswordCard />
    </div>
  );
}

function AppearanceCard() {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Appearance</CardTitle>
        <CardDescription>How Cadence looks in this browser.</CardDescription>
      </CardHeader>
      <CardContent>
        <ThemePicker />
      </CardContent>
    </Card>
  );
}

function ProfileCard() {
  const queryClient = useQueryClient();
  const { data: me } = useCurrentUser();
  const update = useUpdateProfile();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting, isDirty },
  } = useForm<z.infer<typeof profileSchema>>({
    resolver: zodResolver(profileSchema),
    values: { displayName: me?.displayName ?? '' },
  });

  const onSubmit = handleSubmit(async (values) => {
    try {
      await update.mutateAsync({ data: values });
      await queryClient.invalidateQueries({ queryKey: getGetCurrentUserQueryKey() });
      toast.success('Profile updated.');
    } catch (error) {
      applyServerErrors(error, setError, ['displayName']);
    }
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle>Profile</CardTitle>
        <CardDescription>Signed in as {me?.email}.</CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={(event) => void onSubmit(event)} className="flex flex-col gap-4" noValidate>
          {errors.root?.server && <Alert variant="destructive">{errors.root.server.message}</Alert>}
          <TextField
            label="Name"
            autoComplete="name"
            error={errors.displayName?.message}
            {...register('displayName')}
          />
          <Button type="submit" className="self-start" disabled={!isDirty || isSubmitting}>
            Save
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}

function PasswordCard() {
  const change = useChangePassword();
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<z.infer<typeof passwordSchema>>({ resolver: zodResolver(passwordSchema) });

  const onSubmit = handleSubmit(async (values) => {
    try {
      // The server ends every other session and returns a fresh one for this device.
      session.start(await change.mutateAsync({ data: values }));
      reset();
      toast.success('Password changed. Other devices were signed out.');
    } catch (error) {
      applyServerErrors(error, setError, ['currentPassword', 'newPassword']);
    }
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle>Password</CardTitle>
        <CardDescription>Changing it signs you out everywhere else.</CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={(event) => void onSubmit(event)} className="flex flex-col gap-4" noValidate>
          {errors.root?.server && <Alert variant="destructive">{errors.root.server.message}</Alert>}
          <TextField
            label="Current password"
            type="password"
            autoComplete="current-password"
            error={errors.currentPassword?.message}
            {...register('currentPassword')}
          />
          <TextField
            label="New password"
            type="password"
            autoComplete="new-password"
            error={errors.newPassword?.message}
            {...register('newPassword')}
          />
          <Button type="submit" className="self-start" disabled={isSubmitting}>
            Change password
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}
