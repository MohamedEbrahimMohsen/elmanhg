import { useId } from 'react';
import { useController } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Label } from '@/shared/ui/label';
import type { AskTeacherFormValues } from '../schemas/askTeacherFormSchema';

export function ImageField() {
  const { t } = useTranslation('askTeacher');
  const { t: tCommon } = useTranslation();
  const id = useId();
  const errorId = `${id}-error`;
  const {
    field: { ref, name, value, onChange, onBlur },
    fieldState,
  } = useController<AskTeacherFormValues, 'image'>({ name: 'image' });

  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{t('form.image')}</Label>
      <input
        id={id}
        ref={ref}
        name={name}
        type="file"
        accept="image/png,image/jpeg,image/webp"
        onChange={(event) => {
          onChange(event.target.files?.[0] ?? null);
        }}
        onBlur={onBlur}
        aria-invalid={fieldState.invalid}
        aria-describedby={fieldState.error ? errorId : undefined}
        className="text-ui text-text file:me-3 file:min-h-9 file:rounded-sm file:border file:border-border-strong file:bg-surface file:px-3 file:text-ui file:text-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
      />
      {value ? <p className="text-caption text-text-muted">{value.name}</p> : null}
      {fieldState.error ? (
        <p id={errorId} className="text-caption text-danger">
          {tCommon([fieldState.error.message ?? '', 'errors.UNHANDLED_EXCEPTION'])}
        </p>
      ) : null}
    </div>
  );
}
