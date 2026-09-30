import { useTranslation } from 'react-i18next';
import type { EssayCriterionResult } from '@/shared/api/generated/model';
import { formatNumber } from '@/shared/lib/format';

export interface EssayCriteriaListProps {
  criteria: readonly EssayCriterionResult[];
}

export function EssayCriteriaList({ criteria }: EssayCriteriaListProps) {
  const { t, i18n } = useTranslation('questions');
  const lng = i18n.resolvedLanguage ?? i18n.language;

  return (
    <ul aria-label={t('essayGrade.criteria')} className="flex flex-col gap-2">
      {criteria.map((c) => (
        <li key={c.criterionId} className="flex flex-col gap-1 rounded-md border border-border p-3">
          <div className="flex items-baseline justify-between gap-3">
            <p className="text-ui font-semibold text-text">{c.title}</p>
            <span dir="ltr" className="text-caption text-text">
              {t('essayGrade.criterionPoints', {
                points: formatNumber(Number(c.points), lng, 'latin'),
                maxPoints: formatNumber(Number(c.maxPoints), lng, 'latin'),
              })}
            </span>
          </div>
          <p className="text-caption text-text-muted">{c.justification}</p>
        </li>
      ))}
    </ul>
  );
}
