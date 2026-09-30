import { useEffect, useRef } from 'react';
import { Link, useSearchParams } from 'react-router';
import { errorMessage } from '@/shared/api/api-error';
import { useConfirmEmail, useResendConfirmation } from '@/shared/api/generated/endpoints';
import { Alert } from '@/shared/ui/alert';
import { Button } from '@/shared/ui/button';
import { AuthCard } from '../components/auth-card';

/** Shown after registering: the account exists but the email must be confirmed first. */
export function CheckEmailPage() {
  const [params] = useSearchParams();
  const email = params.get('email') ?? '';
  const resend = useResendConfirmation();

  return (
    <AuthCard
      title="Check your email"
      description={
        <>
          We sent a confirmation link to <strong className="text-foreground">{email}</strong>.
          Follow it to activate your account.
        </>
      }
      footer={
        <Link to="/login" className="font-medium text-primary hover:underline">
          Back to sign in
        </Link>
      }
    >
      {resend.isSuccess ? (
        <Alert variant="success">A new link is on its way.</Alert>
      ) : (
        <Button
          variant="outline"
          disabled={!email || resend.isPending}
          onClick={() => {
            resend.mutate({ data: { email } });
          }}
        >
          Send the link again
        </Button>
      )}
    </AuthCard>
  );
}

/** The target of the emailed confirmation link. */
export function ConfirmEmailPage() {
  const [params] = useSearchParams();
  const confirm = useConfirmEmail();
  const userId = params.get('userId');
  const token = params.get('token');
  const started = useRef(false);

  useEffect(() => {
    // Confirm once, even under StrictMode's double effects.
    if (started.current || !userId || !token) return;
    started.current = true;
    confirm.mutate({ data: { userId, token } });
  }, [confirm, userId, token]);

  const failed = confirm.isError || !userId || !token;

  return (
    <AuthCard
      title={
        failed ? 'Link not valid' : confirm.isSuccess ? 'Email confirmed' : 'Confirming your email'
      }
    >
      {confirm.isSuccess && (
        <>
          <Alert variant="success">Your email is confirmed. You can sign in now.</Alert>
          <Button asChild>
            <Link to="/login">Sign in</Link>
          </Button>
        </>
      )}
      {failed && (
        <Alert variant="destructive">
          {confirm.isError
            ? errorMessage(confirm.error)
            : 'This link is incomplete. Open it again from the email.'}
        </Alert>
      )}
      {confirm.isPending && <p className="text-sm text-muted-foreground">One moment…</p>}
    </AuthCard>
  );
}
