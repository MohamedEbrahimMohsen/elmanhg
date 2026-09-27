import type { FieldValues, Path, UseFormReturn } from 'react-hook-form';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';

export type ServerErrorFields<TValues extends FieldValues> = Partial<Record<string, Path<TValues>>>;

export function applyServerErrors<TValues extends FieldValues>(
  form: Pick<UseFormReturn<TValues>, 'setError'>,
  error: unknown,
  fields: ServerErrorFields<TValues>,
): void {
  const codes = error instanceof ApiError ? error.codes : [unhandledErrorCode];
  const unmatched: string[] = [];
  let focused = false;

  for (const code of codes) {
    const field = fields[code];
    if (field) {
      form.setError(field, { type: 'server', message: `errors.${code}` }, { shouldFocus: !focused });
      focused = true;
    } else {
      unmatched.push(code);
    }
  }

  if (unmatched[0]) {
    form.setError('root.server', { type: 'server', message: `errors.${unmatched[0]}` });
  }
}
