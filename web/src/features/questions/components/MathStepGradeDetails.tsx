import { useTranslation } from 'react-i18next';
import type { MathStepGradeDetailResult } from '@/shared/api/generated/model';
import { formatNumber } from '@/shared/lib/format';
import { MathStepScoreList } from './MathStepScoreList';

export interface MathStepGradeDetailsProps {
  mathSteps: MathStepGradeDetailResult;
}

const percent = 100;

export function MathStepGradeDetails({ mathSteps }: MathStepGradeDetailsProps) {
  const { t, i18n } = useTranslation('questions');
  const lng = i18n.resolvedLanguage ?? i18n.language;

  return (
    <section aria-label={t('preview.essay.title')} className="flex flex-col gap-3">
      <p className="text-ui font-semibold text-text">
        {t('preview.mathSteps.verdict', {
          verdict: t(`preview.mathSteps.verdicts.${mathSteps.finalAnswerVerdict}`, {
            defaultValue: mathSteps.finalAnswerVerdict,
          }),
        })}
      </p>
      <MathStepScoreList steps={mathSteps.steps} />
      <div className="flex flex-col gap-1">
        <h3 className="text-ui font-semibold text-text">{t('preview.essay.justification')}</h3>
        <p className="text-body">{mathSteps.justification}</p>
      </div>
      <p className="text-caption text-text-muted">
        {t('preview.essay.confidence', {
          confidence: formatNumber(Math.round(Number(mathSteps.confidence) * percent), lng),
        })}
      </p>
      <p dir="ltr" className="text-caption text-text-muted">
        {t('preview.essay.model', { model: mathSteps.model, promptVersion: mathSteps.promptVersion })}
      </p>
    </section>
  );
}
