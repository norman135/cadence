import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';
import { ApiError, errorMessage } from '@/shared/api/api-error';

/**
 * Shows an API failure on a form: per-field validation errors go to their fields, anything else
 * becomes the form-level error (`root.server`). Returns the form-level message, if any.
 */
export function applyServerErrors<TValues extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<TValues>,
  fields: readonly Path<TValues>[],
): string | undefined {
  const fieldErrors = error instanceof ApiError ? error.problem?.errors : undefined;

  if (fieldErrors) {
    let assigned = false;
    for (const field of fields) {
      const message = fieldErrors[field]?.[0];
      if (message) {
        setError(field, { type: 'server', message });
        assigned = true;
      }
    }
    if (assigned) return undefined;
  }

  const message = errorMessage(error);
  setError('root.server', { type: 'server', message });
  return message;
}
