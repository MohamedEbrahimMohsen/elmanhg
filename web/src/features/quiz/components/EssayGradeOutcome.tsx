import { Suspense } from 'react';
import { CircleAlert, CircleCheck, CircleX } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { EssayCriteriaList } from '@/features/questions';
import type { EssayGradeResult } from '@/shared/api/generated/model';
import { formatNumber } from '@/shared/lib/format';
import { cn } from '@/shared/lib/utils';
import { TeacherReviewNote } from './TeacherReviewNote';

export interface EssayGradeOutcomeProps {
  grade: EssayGradeResult;
}

const verdicts = {
  Correct: {
    icon: CircleCheck,
    panel: 'border-success bg-success-soft',
    color: 'text-success-text',
    key: 'feedback.correct',
  },
  Partial: {
    icon: CircleAlert,
    panel: 'border-warning bg-warning-soft',
    color: 'text-warning',
    key: 'feedback.partial',
  },
  Incorrect: { icon: CircleX, panel: 'border-danger bg-danger-soft', color: 'text-danger', key: 'feedback.incorrect' },
} as const;

function toVerdict(outcome: string | null): keyof typeof verdicts {
  return outcome === 'Correct' || outcome === 'Partial' ? outcome : 'Incorrect';
}

export function EssayGradeOutcome({ grade }: EssayGradeOutcomeProps) {
  const { t, i18n } = useTranslation('quiz');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const verdict = verdicts[toVerdict(grade.outcome)];
  const Icon = verdict.icon;

  return (
    <div
      role="group"
      aria-label={t('essayGrade.label')}
      className={cn('flex flex-col gap-3 rounded-md border px-3.5 py-3', verdict.panel)}
    >
      <div role="status" className="flex items-center gap-3">
        <Icon aria-hidden className={cn('size-6.5 shrink-0', verdict.color)} />
        <div>
          <p className="text-ui font-bold text-text">{t(verdict.key)}</p>
          <p className="text-caption text-text">
            {t('feedback.score', {
              score: formatNumber(Number(grade.score), lng),
              maxScore: formatNumber(Number(grade.maxScore), lng),
            })}
          </p>
        </div>
      </div>
      {grade.criteria.length > 0 ? <EssayCriteriaList criteria={grade.criteria} /> : null}
      {grade.justification !== null ? (
        <div className="flex flex-col gap-1">
          <h3 className="text-caption font-bold text-text">{t('essayGrade.justification')}</h3>
          <p className="text-ui text-text-muted">{grade.justification}</p>
        </div>
      ) : null}
      {grade.review ? (
        <Suspense fallback={null}>
          <TeacherReviewNote review={grade.review} />
        </Suspense>
      ) : null}
    </div>
  );
}
