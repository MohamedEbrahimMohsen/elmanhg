import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { Label } from '@/shared/ui/label';

export interface SessionKindFilterProps {
  value: 'Quiz' | 'Exam' | undefined;
  onChange: (kind: 'Quiz' | 'Exam' | undefined) => void;
}

export function SessionKindFilter({ value, onChange }: SessionKindFilterProps) {
  const { t } = useTranslation('progress');
  const id = useId();

  return (
    <div className="flex flex-col gap-1.5 md:max-w-xs">
      <Label htmlFor={id}>{t('history.filterLabel')}</Label>
      <select
        id={id}
        value={value ?? ''}
        onChange={(event) => {
          onChange(event.target.value === 'Quiz' || event.target.value === 'Exam' ? event.target.value : undefined);
        }}
        className="h-11 w-full rounded-sm border border-border-strong bg-surface px-3 text-ui text-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
      >
        <option value="">{t('history.all')}</option>
        <option value="Quiz">{t('history.quizzes')}</option>
        <option value="Exam">{t('history.exams')}</option>
      </select>
    </div>
  );
}
