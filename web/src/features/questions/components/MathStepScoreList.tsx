import { useTranslation } from 'react-i18next';
import { MathPreview } from '@/features/mathSteps';
import type { MathStepScoreResult } from '@/shared/api/generated/model';
import { formatNumber } from '@/shared/lib/format';

export interface MathStepScoreListProps {
  steps: readonly MathStepScoreResult[];
}

export function MathStepScoreList({ steps }: MathStepScoreListProps) {
  const { t, i18n } = useTranslation('questions');
  const lng = i18n.resolvedLanguage ?? i18n.language;

  return (
    <section aria-label={t('mathStepGrade.steps')} className="flex flex-col gap-2">
      <h3 className="text-caption font-bold text-text">{t('mathStepGrade.steps')}</h3>
      <ol className="flex flex-col gap-2">
        {steps.map((step) => {
          const number = Number(step.stepIndex) + 1;
          return (
            <li
              key={`step-${String(step.stepIndex)}`}
              className="flex flex-col gap-1 rounded-md border border-border p-3"
            >
              <div className="flex items-baseline justify-between gap-3">
                <p className="text-ui font-bold text-text">{t('mathStepGrade.step', { number })}</p>
                <span dir="ltr" className="text-caption text-text">
                  {t('mathStepGrade.stepPoints', {
                    points: formatNumber(Number(step.points), lng),
                    maxPoints: formatNumber(Number(step.maxPoints), lng),
                  })}
                </span>
              </div>
              <MathPreview latex={step.step} label={t('mathStepGrade.step', { number })} />
              <p className="text-caption text-text-muted">{step.justification}</p>
            </li>
          );
        })}
      </ol>
    </section>
  );
}
