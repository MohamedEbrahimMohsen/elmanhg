import { useId } from 'react';
import { useController, type FieldValues, type Path } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';

export interface TextFieldProps<TValues extends FieldValues> {
  name: Path<TValues>;
  label: string;
  type?: 'text' | 'email' | 'tel' | 'password' | 'date';
  autoComplete?: string;
  description?: string;
}

export function TextField<TValues extends FieldValues>({
  name,
  label,
  type,
  autoComplete,
  description,
}: TextFieldProps<TValues>) {
  const { t } = useTranslation();
  const {
    field: { ref, name: fieldName, value: fieldValue, onChange, onBlur, disabled },
    fieldState,
  } = useController<TValues>({ name });
  const id = useId();
  const descriptionId = `${id}-description`;
  const errorId = `${id}-error`;
  const describedByIds = [description ? descriptionId : null, fieldState.error ? errorId : null].filter(
    (value) => value !== null,
  );
  const describedBy = describedByIds.length > 0 ? describedByIds.join(' ') : undefined;
  const value: unknown = fieldValue;

  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        type={type}
        autoComplete={autoComplete}
        name={fieldName}
        value={typeof value === 'string' ? value : ''}
        onChange={onChange}
        onBlur={onBlur}
        ref={ref}
        disabled={disabled}
        aria-invalid={fieldState.invalid}
        aria-describedby={describedBy}
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
