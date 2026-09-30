import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Link, useSearchParams } from 'react-router';
import { useForgotPassword, useResetPassword } from '@/shared/api/generated/endpoints';
import { applyServerErrors } from '@/shared/forms/server-errors';
import { Alert } from '@/shared/ui/alert';
import { Button } from '@/shared/ui/button';
import { TextField } from '@/shared/ui/text-field';
import { AuthCard } from '../components/auth-card';
import {
  forgotPasswordSchema,
  resetPasswordSchema,
  type ForgotPasswordValues,
  type ResetPasswordValues,
} from '../schemas';

const backToSignIn = (
  <Link to="/login" className="font-medium text-primary hover:underline">
    Back to sign in
  </Link>
);

export function ForgotPasswordPage() {
  const forgot = useForgotPassword();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<ForgotPasswordValues>({ resolver: zodResolver(forgotPasswordSchema) });

  const onSubmit = handleSubmit(async (values) => {
    try {
      await forgot.mutateAsync({ data: values });
    } catch (error) {
      applyServerErrors(error, setError, ['email']);
    }
  });

  return (
    <AuthCard
      title="Reset your password"
      description="Enter your email and we'll send you a link to choose a new password."
      footer={backToSignIn}
    >
      {forgot.isSuccess ? (
        // The same answer whether or not the account exists, so emails can't be probed.
        <Alert variant="success">
          If an account exists for that address, a reset link is on its way.
        </Alert>
      ) : (
        <form onSubmit={(event) => void onSubmit(event)} className="flex flex-col gap-4" noValidate>
          {errors.root?.server && <Alert variant="destructive">{errors.root.server.message}</Alert>}
          <TextField
            label="Email"
            type="email"
            autoComplete="email"
            error={errors.email?.message}
            {...register('email')}
          />
          <Button type="submit" disabled={isSubmitting}>
            Send reset link
          </Button>
        </form>
      )}
    </AuthCard>
  );
}

export function ResetPasswordPage() {
  const [params] = useSearchParams();
  const reset = useResetPassword();
  const email = params.get('email') ?? '';
  const token = params.get('token') ?? '';

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<ResetPasswordValues>({ resolver: zodResolver(resetPasswordSchema) });

  const onSubmit = handleSubmit(async ({ newPassword }) => {
    try {
      await reset.mutateAsync({ data: { email, token, newPassword } });
    } catch (error) {
      applyServerErrors(error, setError, ['newPassword']);
    }
  });

  if (reset.isSuccess) {
    return (
      <AuthCard title="Password changed">
        <Alert variant="success">
          Your password was changed and every device was signed out. Sign in with your new password.
        </Alert>
        <Button asChild>
          <Link to="/login">Sign in</Link>
        </Button>
      </AuthCard>
    );
  }

  return (
    <AuthCard title="Choose a new password" description={email} footer={backToSignIn}>
      <form onSubmit={(event) => void onSubmit(event)} className="flex flex-col gap-4" noValidate>
        {errors.root?.server && <Alert variant="destructive">{errors.root.server.message}</Alert>}
        <TextField
          label="New password"
          type="password"
          autoComplete="new-password"
          error={errors.newPassword?.message}
          {...register('newPassword')}
        />
        <TextField
          label="Confirm new password"
          type="password"
          autoComplete="new-password"
          error={errors.confirmPassword?.message}
          {...register('confirmPassword')}
        />
        <Button type="submit" disabled={isSubmitting || !token}>
          Change password
        </Button>
      </form>
    </AuthCard>
  );
}
