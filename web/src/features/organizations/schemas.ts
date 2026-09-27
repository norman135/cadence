import { z } from 'zod/mini';

/** Mirrors the server rule: organization names are 2 to 80 characters after trimming. */
export const organizationNameSchema = z
  .string()
  .check(
    z.trim(),
    z.minLength(2, 'Use at least 2 characters.'),
    z.maxLength(80, 'Use at most 80 characters.'),
  );
