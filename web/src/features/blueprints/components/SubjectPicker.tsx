import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import type { SubjectResult } from '@/shared/api/generated/model';
import { Label } from '@/shared/ui/label';
import { Select } from '@/shared/ui/select';

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
      <Select
        id={id}
        value={value}
        onChange={(event) => {
          onChange(event.target.value);
        }}
      >
        {subjects.map((subject) => (
          <option key={subject.id} value={subject.id}>
            {subject.name}
          </option>
        ))}
      </Select>
    </div>
  );
}
