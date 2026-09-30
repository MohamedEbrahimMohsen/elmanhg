import { CircleAlert, CircleCheck, CircleX } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { MathStepScoreList } from '@/features/questions';
import type { MathStepGradeResult } from '@/shared/api/generated/model';
import { formatNumber } from '@/shared/lib/format';
import { cn } from '@/shared/lib/utils';
import { TeacherReviewNote } from './TeacherReviewNote';

export interface MathStepGradeOutcomeProps {
  grade: MathStepGradeResult;
  showOutcome: boolean;
}

const verdicts = {
  Correct: {
    icon: CircleCheck,
    panel: 'border-success bg-success-soft',
    color: 'text-success',
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

export function MathStepGradeOutcome({ grade, showOutcome }: MathStepGradeOutcomeProps) {
  const { t, i18n } = useTranslation('quiz');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const verdict = verdicts[toVerdict(grade.outcome)];
  const Icon = verdict.icon;

  return (
    <div
      role="group"
      aria-label={t('mathStepGrade.label')}
      className={cn(
        'flex flex-col gap-3 rounded-md border px-3.5 py-3',
        showOutcome ? verdict.panel : 'border-border bg-surface',
      )}
    >
      {showOutcome ? (
        <div role="status" className="flex items-center gap-3">
          <Icon aria-hidden className={cn('size-6.5 shrink-0', verdict.color)} />
          <div>
            <p className="text-ui font-semibold text-text">{t(verdict.key)}</p>
            <p className="text-caption text-text">
              {t('feedback.score', {
                score: formatNumber(Number(grade.score), lng),
                maxScore: formatNumber(Number(grade.maxScore), lng),
              })}
            </p>
          </div>
        </div>
      ) : null}
      {grade.finalAnswerVerdict !== null ? (
        <p className="text-ui text-text">
          <span className="font-semibold">{t('mathStepGrade.finalAnswer')}</span>{' '}
          {t(`mathStepGrade.verdicts.${grade.finalAnswerVerdict}`, { defaultValue: grade.finalAnswerVerdict })}
        </p>
      ) : null}
      {grade.steps.length > 0 ? <MathStepScoreList steps={grade.steps} /> : null}
      {grade.justification ? (
        <div className="flex flex-col gap-1">
          <h3 className="text-caption font-semibold text-text">{t('mathStepGrade.justification')}</h3>
          <p className="text-ui text-text-muted">{grade.justification}</p>
        </div>
      ) : null}
      {grade.review ? <TeacherReviewNote review={grade.review} /> : null}
    </div>
  );
}
