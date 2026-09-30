import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Link, useNavigate, useSearchParams } from 'react-router';
import { useLogin, useRegister } from '@/shared/api/generated/endpoints';
import { session } from '@/shared/auth';
import { applyServerErrors } from '@/shared/forms/server-errors';
import { Alert } from '@/shared/ui/alert';
import { Button } from '@/shared/ui/button';
import { TextField } from '@/shared/ui/text-field';
import { AuthCard } from '../components/auth-card';
import { safeReturnTo } from '../return-to';
import { MIN_PASSWORD_LENGTH, registerSchema, type RegisterValues } from '../schemas';

export function RegisterPage() {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const registerAccount = useRegister();
  const login = useLogin();

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<RegisterValues>({
    resolver: zodResolver(registerSchema),
    // An invitation link pre-fills the invited address.
    defaultValues: { email: params.get('email') ?? '' },
  });

  const onSubmit = handleSubmit(async (values) => {
    try {
      const result = await registerAccount.mutateAsync({ data: values });

      if (result.emailConfirmationRequired) {
        await navigate(`/check-email?email=${encodeURIComponent(values.email)}`);
        return;
      }

      session.start(
        await login.mutateAsync({ data: { email: values.email, password: values.password } }),
      );
      await navigate(safeReturnTo(params.get('returnTo')), { replace: true });
    } catch (error) {
      applyServerErrors(error, setError, ['displayName', 'email', 'password']);
    }
  });

  return (
    <AuthCard
      title="Create your account"
      description="Plan, track and ship with your team."
      footer={
        <>
          Already have an account?{' '}
          <Link to="/login" className="font-medium text-primary hover:underline">
            Sign in
          </Link>
        </>
      }
    >
      <form onSubmit={(event) => void onSubmit(event)} className="flex flex-col gap-4" noValidate>
        {errors.root?.server && <Alert variant="destructive">{errors.root.server.message}</Alert>}

        <TextField
          label="Name"
          autoComplete="name"
          error={errors.displayName?.message}
          {...register('displayName')}
        />
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
          autoComplete="new-password"
          placeholder={`At least ${String(MIN_PASSWORD_LENGTH)} characters`}
          error={errors.password?.message}
          {...register('password')}
        />

        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? 'Creating account…' : 'Create account'}
        </Button>
      </form>
    </AuthCard>
  );
}
