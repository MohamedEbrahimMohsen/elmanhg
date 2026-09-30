import { useTranslation } from 'react-i18next';
import { RichTextViewer } from '@/features/content';
import type { ValidationQuestionDetailResult } from '@/shared/api/generated/model';
import { toAnswerKey } from '../api/answerKey';
import { toDiagramModel } from '../api/dragDropValues';
import { toStudentQuestion } from '../api/studentQuestion';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { DragDropPreview } from './DragDropPreview';
import { EssayRubricView } from './EssayRubricView';
import { MathAnswerRulesView } from './MathAnswerRulesView';
import { QuestionView } from './QuestionView';

export interface ValidationQuestionContentProps {
  question: ValidationQuestionDetailResult;
  values: QuestionValues;
}

const cardClassName = 'flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5';

export function ValidationQuestionContent({ question, values }: ValidationQuestionContentProps) {
  const { t } = useTranslation('questions');

  return (
    <>
      <section aria-label={t('validation.detail.preview')} className={cardClassName}>
        <h2 className="font-display text-h3 font-semibold">{t('validation.detail.preview')}</h2>
        {values.type === 'DragDrop' ? (
          <DragDropPreview stem={values.stem} diagram={toDiagramModel(values)} showKey />
        ) : (
          <QuestionView
            question={toStudentQuestion(values)}
            answer={toAnswerKey(values)}
            onAnswerChange={() => undefined}
            disabled
          />
        )}
      </section>
      {values.type === 'Essay' ? (
        <EssayRubricView criteria={values.criteria} modelAnswers={values.modelAnswers} />
      ) : null}
      {values.type === 'MathSteps' ? (
        <MathAnswerRulesView
          answers={values.mathAnswers}
          form={values.mathForm}
          tolerance={values.mathTolerance}
          toleranceMode={values.mathToleranceMode}
        />
      ) : null}
      <section aria-label={t('validation.detail.explanation')} className={cardClassName}>
        <h2 className="font-display text-h3 font-semibold">{t('validation.detail.explanation')}</h2>
        <RichTextViewer html={question.explanation} />
        <details>
          <summary className="cursor-pointer text-caption text-accent">{t('validation.detail.gradingSpec')}</summary>
          <pre dir="ltr" className="mt-2 overflow-x-auto rounded-sm bg-soft p-3 font-mono text-mono">
            {JSON.stringify({ body: question.body, gradingSpec: question.gradingSpec }, null, 2)}
          </pre>
        </details>
      </section>
    </>
  );
}
