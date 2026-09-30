import { useState } from 'react';
import { useFormContext, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { emptyAnswer, toStudentQuestion } from '../api/studentQuestion';
import { useTestGrade } from '../hooks/useTestGrade';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { EssayGradeDetails } from './EssayGradeDetails';
import { GradeResultPanel } from './GradeResultPanel';
import { QuestionView } from './QuestionView';

export interface QuestionPreviewPanelProps {
  lessonId: string;
}

export function QuestionPreviewPanel({ lessonId }: QuestionPreviewPanelProps) {
  const { t } = useTranslation('questions');
  const values = useWatch<QuestionValues>();
  const question = toStudentQuestion(values);
  const [answer, setAnswer] = useState(emptyAnswer);
  const { getValues } = useFormContext<QuestionValues>();
  const { grade, result, errorCode, isPending } = useTestGrade(lessonId);
  const isEssay = values.type === 'Essay';

  return (
    <section
      aria-label={t('preview.title')}
      className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5"
    >
      <h2 className="font-display text-h2 font-bold">{t('preview.title')}</h2>
      <QuestionView question={question} answer={answer} onAnswerChange={setAnswer} />
      {isEssay ? <p className="text-caption text-text-muted">{t('preview.essayGradingHint')}</p> : null}
      <div>
        <Button
          variant="secondary"
          disabled={isPending}
          onClick={() => {
            grade(getValues(), answer);
          }}
        >
          {isPending ? t(isEssay ? 'preview.essayGrading' : 'preview.grading') : t('preview.tryAnswer')}
        </Button>
      </div>
      {errorCode ? (
        <div role="alert" className="flex flex-col gap-1 rounded-md border border-danger bg-danger-soft px-3.5 py-3">
          <p className="text-ui font-semibold text-danger">{t('preview.gradeFailed')}</p>
          <p className="text-caption text-text">
            {t([`common:errors.${errorCode}`, 'common:errors.UNHANDLED_EXCEPTION'])}
          </p>
        </div>
      ) : null}
      {result ? <GradeResultPanel result={result} /> : null}
      {result?.essay ? <EssayGradeDetails essay={result.essay} /> : null}
    </section>
  );
}
