import { z } from 'zod';

// These mirror the server's rules (AuthValidationRules) so users get instant feedback; the server
// remains the authority and its field errors are shown too.
export const MIN_PASSWORD_LENGTH = 10;

const email = z.email('Enter a valid email address.').max(256);
const newPassword = z
  .string()
  .min(MIN_PASSWORD_LENGTH, `Use at least ${String(MIN_PASSWORD_LENGTH)} characters.`)
  .max(128, 'Use at most 128 characters.');

export const loginSchema = z.object({
  email,
  password: z.string().min(1, 'Enter your password.'),
});

export const registerSchema = z.object({
  displayName: z.string().trim().min(1, 'Enter your name.').max(100),
  email,
  password: newPassword,
});

export const forgotPasswordSchema = z.object({ email });

export const resetPasswordSchema = z
  .object({ newPassword, confirmPassword: z.string() })
  .refine((values) => values.newPassword === values.confirmPassword, {
    path: ['confirmPassword'],
    message: 'The passwords do not match.',
  });

export type LoginValues = z.infer<typeof loginSchema>;
export type RegisterValues = z.infer<typeof registerSchema>;
export type ForgotPasswordValues = z.infer<typeof forgotPasswordSchema>;
export type ResetPasswordValues = z.infer<typeof resetPasswordSchema>;
