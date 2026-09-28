import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { QuestionView, type QuestionAnswer } from '@/features/questions';
import { toQuizQuestion } from '@/features/quiz';
import type { ExamItemResult } from '@/shared/api/generated/model';

export interface ExamQuestionCardProps {
  item: ExamItemResult;
  total: number;
  answer: QuestionAnswer;
  onChange: (answer: QuestionAnswer) => void;
  disabled: boolean;
}

export function ExamQuestionCard({ item, total, answer, onChange, disabled }: ExamQuestionCardProps) {
  const { t } = useTranslation('exam');
  const headingId = useId();
  const question = toQuizQuestion(item);

  return (
    <article
      aria-labelledby={headingId}
      className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-4.5 shadow-1"
    >
      <div className="flex flex-wrap items-center gap-2">
        <h2 id={headingId} className="font-display text-h3 font-semibold">
          {t('exam.counter', { position: Number(item.position), total })}
        </h2>
        <span className="rounded-full bg-soft px-2.5 py-0.5 text-micro font-semibold text-text-muted">
          {t(`questions:types.${question.type}`)}
        </span>
        <span className="text-caption text-text-muted">{t('exam.marks', { count: Number(item.maxScore) })}</span>
      </div>
      <QuestionView question={question} answer={answer} onAnswerChange={onChange} disabled={disabled} />
    </article>
  );
}
