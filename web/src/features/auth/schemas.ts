import { z } from 'zod/mini';

// These mirror the server's rules (AuthValidationRules) so users get instant feedback; the server
// remains the authority and its field errors are shown too.
export const MIN_PASSWORD_LENGTH = 10;

const email = z.email('Enter a valid email address.').check(z.maxLength(256));
const newPassword = z
  .string()
  .check(
    z.minLength(MIN_PASSWORD_LENGTH, `Use at least ${String(MIN_PASSWORD_LENGTH)} characters.`),
    z.maxLength(128, 'Use at most 128 characters.'),
  );

export const loginSchema = z.object({
  email,
  password: z.string().check(z.minLength(1, 'Enter your password.')),
});

export const registerSchema = z.object({
  displayName: z.string().check(z.trim(), z.minLength(1, 'Enter your name.'), z.maxLength(100)),
  email,
  password: newPassword,
});

export const forgotPasswordSchema = z.object({ email });

export const resetPasswordSchema = z.object({ newPassword, confirmPassword: z.string() }).check(
  z.refine((values) => values.newPassword === values.confirmPassword, {
    path: ['confirmPassword'],
    error: 'The passwords do not match.',
  }),
);

export type LoginValues = z.infer<typeof loginSchema>;
export type RegisterValues = z.infer<typeof registerSchema>;
export type ForgotPasswordValues = z.infer<typeof forgotPasswordSchema>;
export type ResetPasswordValues = z.infer<typeof resetPasswordSchema>;
