import { z } from 'zod';

/** Mirrors the server rule: organization names are 2 to 80 characters after trimming. */
export const organizationNameSchema = z
  .string()
  .trim()
  .min(2, 'Use at least 2 characters.')
  .max(80, 'Use at most 80 characters.');
