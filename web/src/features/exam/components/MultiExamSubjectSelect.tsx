import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import type { SubjectResult } from '@/shared/api/generated/model';
import { Label } from '@/shared/ui/label';
import { Select } from '@/shared/ui/select';

export interface MultiExamSubjectSelectProps {
  subjects: readonly SubjectResult[];
  value: string;
  onChange: (id: string) => void;
}

export function MultiExamSubjectSelect({ subjects, value, onChange }: MultiExamSubjectSelectProps) {
  const { t } = useTranslation('exam');
  const id = useId();

  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{t('multi.subject')}</Label>
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
