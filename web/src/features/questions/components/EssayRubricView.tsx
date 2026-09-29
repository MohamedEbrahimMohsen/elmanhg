import { useTranslation } from 'react-i18next';
import { RichTextViewer } from '@/features/content';
import { rubricTotalPoints } from '../api/essayValues';
import type { QuestionValues } from '../schemas/questionEditorSchema';

export interface EssayRubricViewProps {
  criteria: QuestionValues['criteria'];
  modelAnswers: QuestionValues['modelAnswers'];
}

const cardClassName = 'flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5';

export function EssayRubricView({ criteria, modelAnswers }: EssayRubricViewProps) {
  const { t } = useTranslation('questions');

  return (
    <section aria-label={t('validation.detail.rubric')} className={cardClassName}>
      <h2 className="font-display text-h3 font-semibold">{t('validation.detail.rubric')}</h2>
      <p className="text-caption text-text-muted">
        {t('validation.detail.rubricTotal', { total: rubricTotalPoints(criteria) })}
      </p>
      <ol className="flex flex-col gap-3">
        {criteria.map((criterion) => (
          <li key={criterion.id} className="flex flex-col gap-1.5">
            <h3 className="text-ui font-semibold text-text">
              {criterion.title} {t('validation.detail.criterionPoints', { points: criterion.points })}
            </h3>
            {criterion.description === '' ? null : (
              <p className="text-caption text-text-muted">{criterion.description}</p>
            )}
            <ul className="flex flex-col gap-1 text-caption text-text">
              {criterion.levels.map((level) => (
                <li key={`${criterion.id}-${level.points}`}>
                  {t('validation.detail.level', { points: level.points, description: level.description })}
                </li>
              ))}
            </ul>
          </li>
        ))}
      </ol>
      <h3 className="text-ui font-semibold text-text">{t('validation.detail.modelAnswers')}</h3>
      {modelAnswers.map((answer, index) => {
        const number = index + 1;
        return (
          <div
            key={`model-${String(number)}`}
            role="group"
            className="rounded-md border border-border p-3"
            aria-label={t('validation.detail.modelAnswer', { number })}
          >
            <RichTextViewer html={answer.text} />
          </div>
        );
      })}
    </section>
  );
}
