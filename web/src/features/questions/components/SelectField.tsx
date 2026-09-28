import { useId } from 'react';
import { useController, type FieldValues, type Path } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Label } from '@/shared/ui/label';

export interface SelectFieldProps<TValues extends FieldValues> {
  name: Path<TValues>;
  label: string;
  options: readonly { value: string; label: string }[];
  placeholder?: string | undefined;
  description?: string | undefined;
  disabled?: boolean | undefined;
}

export function SelectField<TValues extends FieldValues>({
  name,
  label,
  options,
  placeholder,
  description,
  disabled,
}: SelectFieldProps<TValues>) {
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
      <select
        id={id}
        ref={ref}
        name={fieldName}
        value={typeof value === 'string' ? value : ''}
        onChange={onChange}
        onBlur={onBlur}
        disabled={disabled}
        aria-invalid={fieldState.invalid}
        aria-describedby={describedByIds.length > 0 ? describedByIds.join(' ') : undefined}
        className="h-11 w-full rounded-sm border border-border-strong bg-surface px-3 text-ui text-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden disabled:opacity-45 aria-invalid:border-danger"
      >
        {placeholder === undefined ? null : <option value="">{placeholder}</option>}
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
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
