import { useTranslation } from 'react-i18next';
import type { GradeReviewSubjectResult } from '@/shared/api/generated/model';
import { formatNumber } from '@/shared/lib/format';
import { pillTabClassName } from '@/shared/ui/pillTab';

export interface GradeReviewSubjectPickerProps {
  subjects: GradeReviewSubjectResult[];
  selectedId: string;
  onSelect: (subjectId: string) => void;
}

export function GradeReviewSubjectPicker({ subjects, selectedId, onSelect }: GradeReviewSubjectPickerProps) {
  const { t, i18n } = useTranslation('gradeReview');
  const lng = i18n.resolvedLanguage ?? i18n.language;

  return (
    <div role="group" aria-label={t('queue.subjects')} className="flex flex-wrap gap-2">
      {subjects.map((subject) => {
        const selected = subject.subjectId === selectedId;
        const count = Number(subject.essayCount) + Number(subject.mathStepsCount);
        return (
          <button
            key={subject.subjectId}
            type="button"
            aria-pressed={selected}
            onClick={() => {
              onSelect(subject.subjectId);
            }}
            className={pillTabClassName}
          >
            {t('queue.subjectPill', { name: subject.name, count: formatNumber(count, lng) })}
          </button>
        );
      })}
    </div>
  );
}
