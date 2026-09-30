import { zodResolver } from '@hookform/resolvers/zod';
import { useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { Link, useNavigate, useSearchParams } from 'react-router';
import { ApiError } from '@/shared/api/api-error';
import { useLogin, useResendConfirmation } from '@/shared/api/generated/endpoints';
import { session } from '@/shared/auth';
import { applyServerErrors } from '@/shared/forms/server-errors';
import { Alert } from '@/shared/ui/alert';
import { Button } from '@/shared/ui/button';
import { TextField } from '@/shared/ui/text-field';
import { AuthCard } from '../components/auth-card';
import { safeReturnTo } from '../return-to';
import { loginSchema, type LoginValues } from '../schemas';

export function LoginPage() {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const queryClient = useQueryClient();
  const login = useLogin();
  const resend = useResendConfirmation();

  const {
    register,
    handleSubmit,
    setError,
    getValues,
    formState: { errors, isSubmitting },
  } = useForm<LoginValues>({ resolver: zodResolver(loginSchema) });

  const onSubmit = handleSubmit(async (values) => {
    try {
      const token = await login.mutateAsync({ data: values });
      // A different user may have been signed in before; drop their cached data.
      queryClient.clear();
      session.start(token);
      await navigate(safeReturnTo(params.get('returnTo')), { replace: true });
    } catch (error) {
      applyServerErrors(error, setError, ['email', 'password']);
    }
  });

  const unconfirmed =
    login.error instanceof ApiError && login.error.code === 'auth.email_not_confirmed';

  return (
    <AuthCard
      title="Welcome back"
      description="Sign in to your Cadence workspace."
      footer={
        <>
          New to Cadence?{' '}
          <Link to="/register" className="font-medium text-primary hover:underline">
            Create an account
          </Link>
        </>
      }
    >
      <form onSubmit={(event) => void onSubmit(event)} className="flex flex-col gap-4" noValidate>
        {errors.root?.server && !unconfirmed && (
          <Alert variant="destructive">{errors.root.server.message}</Alert>
        )}
        {unconfirmed && (
          <Alert>
            Confirm your email address first.{' '}
            {resend.isSuccess ? (
              'We sent a new link.'
            ) : (
              <button
                type="button"
                className="font-medium text-primary hover:underline"
                onClick={() => {
                  resend.mutate({ data: { email: getValues('email') } });
                }}
              >
                Send the link again
              </button>
            )}
          </Alert>
        )}

        <TextField
          label="Email"
          type="email"
          autoComplete="email"
          error={errors.email?.message}
          {...register('email')}
        />
        <TextField
          label="Password"
          type="password"
          autoComplete="current-password"
          error={errors.password?.message}
          labelAccessory={
            <Link to="/forgot-password" className="text-sm text-primary hover:underline">
              Forgot password?
            </Link>
          }
          {...register('password')}
        />

        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? 'Signing in…' : 'Sign in'}
        </Button>
      </form>
    </AuthCard>
  );
}
