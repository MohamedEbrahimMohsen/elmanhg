import { useTranslation } from 'react-i18next';
import type { GradeReviewSubjectResult } from '@/shared/api/generated/model';
import { formatNumber } from '@/shared/lib/format';
import { cn } from '@/shared/lib/utils';

export interface GradeReviewSubjectPickerProps {
  subjects: GradeReviewSubjectResult[];
  selectedId: string;
  onSelect: (subjectId: string) => void;
}

const pillClassName =
  'inline-flex min-h-11 items-center rounded-pill border px-4 text-ui font-semibold focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden';

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
            className={cn(
              pillClassName,
              selected ? 'border-text bg-text text-surface' : 'border-border-strong bg-surface text-text hover:bg-soft',
            )}
          >
            {t('queue.subjectPill', { name: subject.name, count: formatNumber(count, lng, 'latin') })}
          </button>
        );
      })}
    </div>
  );
}
