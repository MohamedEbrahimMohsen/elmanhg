import { useId } from 'react';
import { useController, type FieldValues, type Path } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Label } from '@/shared/ui/label';

export interface TextAreaFieldProps<TValues extends FieldValues> {
  name: Path<TValues>;
  label: string;
  description?: string | undefined;
}

export function TextAreaField<TValues extends FieldValues>({ name, label, description }: TextAreaFieldProps<TValues>) {
  const { t } = useTranslation();
  const {
    field: { ref, name: fieldName, value: fieldValue, onChange, onBlur },
    fieldState,
  } = useController<TValues>({ name });
  const id = useId();
  const descriptionId = `${id}-description`;
  const errorId = `${id}-error`;
  const describedByIds = [description ? descriptionId : null, fieldState.error ? errorId : null].filter(
    (value) => value !== null,
  );
  const value: unknown = fieldValue;

  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      <textarea
        id={id}
        ref={ref}
        name={fieldName}
        rows={3}
        value={typeof value === 'string' ? value : ''}
        onChange={onChange}
        onBlur={onBlur}
        aria-invalid={fieldState.invalid}
        aria-describedby={describedByIds.length > 0 ? describedByIds.join(' ') : undefined}
        className="min-h-20 w-full rounded-sm border border-border-strong bg-surface px-3 py-2.25 text-ui text-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden aria-invalid:border-danger"
      />
      {description ? (
        <p id={descriptionId} className="text-caption text-text-muted">
          {description}
        </p>
      ) : null}
      {fieldState.error ? (
        <p id={errorId} className="text-caption text-danger">
          {t([fieldState.error.message ?? '', 'errors.UNHANDLED_EXCEPTION'])}
        </p>
      ) : null}
    </div>
  );
}
