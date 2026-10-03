import { useId } from 'react';
import { useController } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Label } from '@/shared/ui/label';
import { Select } from '@/shared/ui/select';
import type { TrainingExportRequestValues } from '../schemas/trainingExportRequestSchema';

export interface TrainingExportSelectFieldProps {
  name: 'source' | 'subjectId';
  label: string;
  allLabel?: string;
  options: { value: string; label: string }[];
}

export function TrainingExportSelectField({ name, label, allLabel, options }: TrainingExportSelectFieldProps) {
  const { t } = useTranslation();
  const id = useId();
  const errorId = `${id}-error`;
  const {
    field: { ref, name: fieldName, value, onChange, onBlur },
    fieldState,
  } = useController<TrainingExportRequestValues, 'source' | 'subjectId'>({ name });

  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Select
        id={id}
        ref={ref}
        name={fieldName}
        value={value}
        onChange={onChange}
        onBlur={onBlur}
        aria-invalid={fieldState.invalid}
        aria-describedby={fieldState.error ? errorId : undefined}
      >
        {allLabel ? <option value="">{allLabel}</option> : null}
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </Select>
      {fieldState.error ? (
        <p id={errorId} className="text-caption text-danger">
          {t([fieldState.error.message ?? '', 'errors.UNHANDLED_EXCEPTION'])}
        </p>
      ) : null}
    </div>
  );
}
