import { useTranslation } from 'react-i18next';
import { MathPreview } from '@/features/mathSteps';
import type { QuestionValues } from '../schemas/questionEditorSchema';

export interface MathAnswerRulesViewProps {
  answers: QuestionValues['mathAnswers'];
  form: QuestionValues['mathForm'];
  tolerance: string;
  toleranceMode: QuestionValues['mathToleranceMode'];
  solution: QuestionValues['mathSolution'];
  stepsWeight: string;
}

const cardClassName = 'flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5';

export function MathAnswerRulesView({
  answers,
  form,
  tolerance,
  toleranceMode,
  solution,
  stepsWeight,
}: MathAnswerRulesViewProps) {
  const { t } = useTranslation('questions');
  const trimmed = tolerance.trim();

  return (
    <section aria-label={t('validation.detail.mathRules')} className={cardClassName}>
      <h2 className="font-display text-h3 font-bold">{t('validation.detail.mathRules')}</h2>
      <h3 className="text-body font-bold">{t('validation.detail.mathAccepted')}</h3>
      <ol className="flex flex-col gap-2">
        {answers.map((answer, index) => {
          const number = index + 1;
          return (
            <li key={`answer-${String(number)}`}>
              <MathPreview latex={answer.latex} label={t('editor.math.answer', { number })} />
            </li>
          );
        })}
      </ol>
      <p>{t('validation.detail.mathForm', { form: t(`editor.math.forms.${form}`) })}</p>
      {trimmed === '' ? null : (
        <p dir="ltr">
          {t(
            toleranceMode === 'percent' ? 'validation.detail.mathTolerancePercent' : 'validation.detail.mathTolerance',
            {
              tolerance: trimmed,
            },
          )}
        </p>
      )}
      <p>
        {Number(stepsWeight) > 0
          ? t('validation.detail.mathStepsWeight', { weight: stepsWeight })
          : t('validation.detail.mathFinalOnly')}
      </p>
      {solution.length > 0 ? (
        <>
          <h3 className="text-body font-bold">{t('validation.detail.mathSolution')}</h3>
          <ol className="flex flex-col gap-2">
            {solution.map((step, index) => {
              const number = index + 1;
              return (
                <li key={`step-${String(number)}`}>
                  <MathPreview latex={step.latex} label={t('editor.math.solutionStep', { number })} />
                </li>
              );
            })}
          </ol>
        </>
      ) : null}
    </section>
  );
}
