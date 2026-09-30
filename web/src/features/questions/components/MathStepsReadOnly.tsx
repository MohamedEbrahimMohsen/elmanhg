import { useTranslation } from 'react-i18next';
import { MathPreview, type MathStepsPayload } from '@/features/mathSteps';

export interface MathStepsReadOnlyProps {
  solution: MathStepsPayload;
}

export function MathStepsReadOnly({ solution }: MathStepsReadOnlyProps) {
  const { t } = useTranslation('questions');

  return (
    <div role="group" aria-label={t('view.mathReadOnly')} className="flex flex-col gap-3">
      {solution.steps.length === 0 ? (
        <p className="text-caption text-text-muted">{t('view.mathNoSteps')}</p>
      ) : (
        <ol className="flex flex-col gap-2">
          {solution.steps.map((step, index) => {
            const number = index + 1;
            return (
              <li key={`step-${String(index)}`} className="flex flex-col gap-1">
                <p className="text-caption font-semibold text-text-muted">{t('view.mathStep', { number })}</p>
                <MathPreview latex={step} label={t('view.mathStep', { number })} />
              </li>
            );
          })}
        </ol>
      )}
      <p className="text-caption font-semibold text-text-muted">{t('view.mathFinal')}</p>
      <MathPreview latex={solution.finalAnswer} label={t('view.mathFinal')} />
    </div>
  );
}
