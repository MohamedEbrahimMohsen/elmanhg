import { useTranslation } from 'react-i18next';
import { formatNumber } from '@/shared/lib/format';
import { pillTabClassName } from '@/shared/ui/pillTab';
import { gradeReviewKinds, type GradeReviewKindValue } from '../api/gradeReviewOptions';

export interface GradeReviewKindTabsProps {
  kind: GradeReviewKindValue;
  essayCount: number;
  mathStepsCount: number;
  onSelect: (kind: GradeReviewKindValue) => void;
}

export function GradeReviewKindTabs({ kind, essayCount, mathStepsCount, onSelect }: GradeReviewKindTabsProps) {
  const { t, i18n } = useTranslation('gradeReview');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const counts = { Essay: essayCount, MathSteps: mathStepsCount };

  return (
    <div className="flex flex-wrap gap-2">
      {gradeReviewKinds.map((value) => (
        <button
          key={value}
          type="button"
          aria-pressed={kind === value}
          onClick={() => {
            onSelect(value);
          }}
          className={pillTabClassName}
        >
          {t(`queue.kinds.${value}`, { count: formatNumber(counts[value], lng, 'latin') })}
        </button>
      ))}
    </div>
  );
}
