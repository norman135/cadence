import { useId, type ComponentProps, type ReactNode } from 'react';
import { Input } from './input';
import { Label } from './label';

type TextFieldProps = ComponentProps<'input'> & {
  label: string;
  /** Validation message shown under the field and announced to screen readers. */
  error?: string | undefined;
  /** Extra content next to the label, e.g. a "Forgot password?" link. */
  labelAccessory?: ReactNode;
};

/** A labelled input with an accessible error message; spread react-hook-form's `register()` into it. */
export function TextField({ label, error, labelAccessory, id, ...props }: TextFieldProps) {
  const generatedId = useId();
  const inputId = id ?? generatedId;
  const errorId = `${inputId}-error`;

  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-center justify-between">
        <Label htmlFor={inputId}>{label}</Label>
        {labelAccessory}
      </div>
      <Input
        id={inputId}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? errorId : undefined}
        {...props}
      />
      {error && (
        <p id={errorId} className="text-sm text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}
