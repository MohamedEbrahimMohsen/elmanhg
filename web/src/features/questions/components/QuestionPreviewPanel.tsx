import { useState } from 'react';
import { useFormContext, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { toDiagramModel } from '../api/dragDropValues';
import { emptyAnswer, toStudentQuestion } from '../api/studentQuestion';
import { useTestGrade } from '../hooks/useTestGrade';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { DragDropPreview } from './DragDropPreview';
import { EssayGradeDetails } from './EssayGradeDetails';
import { GradeResultPanel } from './GradeResultPanel';
import { MathStepGradeDetails } from './MathStepGradeDetails';
import { QuestionView } from './QuestionView';
import { registerDiagramLocales } from '../diagramLocales';

registerDiagramLocales();

export interface QuestionPreviewPanelProps {
  lessonId: string;
}

function pendingLabel(isEssay: boolean, isStepGraded: boolean): string {
  if (isEssay) {
    return 'preview.essayGrading';
  }
  return isStepGraded ? 'preview.mathStepsGrading' : 'preview.grading';
}

export function QuestionPreviewPanel({ lessonId }: QuestionPreviewPanelProps) {
  const [showKey, setShowKey] = useState(false);
  const { t } = useTranslation('questions');
  const values = useWatch<QuestionValues>();
  const question = toStudentQuestion(values);
  const [answer, setAnswer] = useState(emptyAnswer);
  const { getValues } = useFormContext<QuestionValues>();
  const { grade, result, errorCode, isPending } = useTestGrade(lessonId);
  const isEssay = values.type === 'Essay';
  const isDragDrop = values.type === 'DragDrop';
  const isStepGraded = values.type === 'MathSteps' && Number(values.mathStepsWeight) > 0;

  return (
    <section
      aria-label={t('preview.title')}
      className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5"
    >
      <h2 className="font-display text-h2 font-bold">{t('preview.title')}</h2>
      {isDragDrop && showKey ? (
        <DragDropPreview stem={values.stem ?? ''} diagram={toDiagramModel(values)} />
      ) : (
        <QuestionView question={question} answer={answer} onAnswerChange={setAnswer} />
      )}
      {isDragDrop ? (
        <label className="flex min-h-11 items-center gap-2.5 text-ui">
          <input
            type="checkbox"
            checked={showKey}
            onChange={(event) => {
              setShowKey(event.target.checked);
            }}
            className="size-4.5 accent-accent"
          />
          {t('questionsDiagram:preview.showKey')}
        </label>
      ) : null}
      {isEssay ? <p className="text-caption text-text-muted">{t('preview.essayGradingHint')}</p> : null}
      {isStepGraded ? <p className="text-caption text-text-muted">{t('preview.mathStepsGradingHint')}</p> : null}
      <div>
        <Button
          variant="secondary"
          disabled={isPending}
          onClick={() => {
            grade(getValues(), answer);
          }}
        >
          {isPending ? t(pendingLabel(isEssay, isStepGraded)) : t('preview.tryAnswer')}
        </Button>
      </div>
      {errorCode ? (
        <div role="alert" className="flex flex-col gap-1 rounded-md border border-danger bg-danger-soft px-3.5 py-3">
          <p className="text-ui font-bold text-danger">{t('preview.gradeFailed')}</p>
          <p className="text-caption text-text">
            {t([`common:errors.${errorCode}`, 'common:errors.UNHANDLED_EXCEPTION'])}
          </p>
        </div>
      ) : null}
      {result ? <GradeResultPanel result={result} /> : null}
      {result?.essay ? <EssayGradeDetails essay={result.essay} /> : null}
      {result?.mathSteps ? <MathStepGradeDetails mathSteps={result.mathSteps} /> : null}
    </section>
  );
}
