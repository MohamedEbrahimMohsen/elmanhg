import { useState } from 'react';
import { useFormContext, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { emptyAnswer, toStudentQuestion } from '../api/studentQuestion';
import { useTestGrade } from '../hooks/useTestGrade';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { GradeResultPanel } from './GradeResultPanel';
import { QuestionView } from './QuestionView';

export function QuestionPreviewPanel() {
  const { t } = useTranslation('questions');
  const values = useWatch<QuestionValues>();
  const question = toStudentQuestion(values);
  const [answer, setAnswer] = useState(emptyAnswer);
  const { getValues } = useFormContext<QuestionValues>();
  const { grade, result, errorCode, isPending } = useTestGrade();

  return (
    <section
      aria-label={t('preview.title')}
      className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5"
    >
      <h2 className="font-display text-h2 font-bold">{t('preview.title')}</h2>
      <QuestionView question={question} answer={answer} onAnswerChange={setAnswer} />
      {values.type === 'Essay' ? (
        <p className="text-caption text-text-muted">{t('preview.essayNotGradable')}</p>
      ) : (
        <>
          <div>
            <Button
              variant="secondary"
              disabled={isPending}
              onClick={() => {
                grade(getValues(), answer);
              }}
            >
              {isPending ? t('preview.grading') : t('preview.tryAnswer')}
            </Button>
          </div>
          {errorCode ? (
            <div
              role="alert"
              className="flex flex-col gap-1 rounded-md border border-danger bg-danger-soft px-3.5 py-3"
            >
              <p className="text-ui font-semibold text-danger">{t('preview.gradeFailed')}</p>
              <p className="text-caption text-text">
                {t([`common:errors.${errorCode}`, 'common:errors.UNHANDLED_EXCEPTION'])}
              </p>
            </div>
          ) : null}
          {result ? <GradeResultPanel result={result} /> : null}
        </>
      )}
    </section>
  );
}
