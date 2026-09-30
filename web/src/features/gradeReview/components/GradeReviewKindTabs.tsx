import { useTranslation } from 'react-i18next';
import { formatNumber } from '@/shared/lib/format';
import { cn } from '@/shared/lib/utils';
import { gradeReviewKinds, type GradeReviewKindValue } from '../api/gradeReviewOptions';

export interface GradeReviewKindTabsProps {
  kind: GradeReviewKindValue;
  essayCount: number;
  mathStepsCount: number;
  onSelect: (kind: GradeReviewKindValue) => void;
}

const tabClassName =
  'inline-flex min-h-11 items-center rounded-pill border px-4 text-ui font-semibold focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden';

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
          className={cn(
            tabClassName,
            kind === value
              ? 'border-text bg-text text-surface'
              : 'border-border-strong bg-surface text-text hover:bg-soft',
          )}
        >
          {t(`queue.kinds.${value}`, { count: formatNumber(counts[value], lng, 'latin') })}
        </button>
      ))}
    </div>
  );
}
