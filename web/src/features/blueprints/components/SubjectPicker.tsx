import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import type { SubjectResult } from '@/shared/api/generated/model';
import { Label } from '@/shared/ui/label';

export interface SubjectPickerProps {
  subjects: readonly SubjectResult[];
  value: string;
  onChange: (id: string) => void;
}

export function SubjectPicker({ subjects, value, onChange }: SubjectPickerProps) {
  const { t } = useTranslation('blueprints');
  const id = useId();

  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{t('page.subject')}</Label>
      <select
        id={id}
        value={value}
        onChange={(event) => {
          onChange(event.target.value);
        }}
        className="h-11 w-full rounded-sm border border-border-strong bg-surface px-3 text-ui text-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden disabled:opacity-45 aria-invalid:border-danger"
      >
        {subjects.map((subject) => (
          <option key={subject.id} value={subject.id}>
            {subject.name}
          </option>
        ))}
      </select>
    </div>
  );
}
