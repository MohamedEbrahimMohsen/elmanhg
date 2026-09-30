import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import type { AvatarContextInput } from '@/features/avatar';
import { QuestionView } from '@/features/questions';
import type { AttemptResult } from '@/shared/api/generated/model';
import { choiceReview } from '../api/correctAnswer';
import { fromAnswerPayload, toQuizQuestion, type QuizItemContent } from '../api/quizItem';
import { FeedbackPanel } from './FeedbackPanel';

export interface QuizReviewItemProps {
  item: QuizItemContent;
  attempt: AttemptResult;
  ask: AvatarContextInput;
}

export function QuizReviewItem({ item, attempt, ask }: QuizReviewItemProps) {
  const { t } = useTranslation('quiz');
  const headingId = useId();
  const question = toQuizQuestion(item);

  return (
    <article
      aria-labelledby={headingId}
      className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-4.5 shadow-1"
    >
      <div className="flex flex-wrap items-center gap-2">
        <h3 id={headingId} className="font-display text-h3 font-semibold">
          {t('result.reviewItem', { position: Number(item.position) })}
        </h3>
        <span className="rounded-full bg-soft px-2.5 py-0.5 text-micro font-semibold text-text-muted">
          {t(`questions:types.${question.type}`)}
        </span>
      </div>
      <QuestionView
        question={question}
        answer={fromAnswerPayload(question, attempt.answer)}
        onAnswerChange={() => undefined}
        disabled
        review={choiceReview(question, item.correctAnswer)}
      />
      <FeedbackPanel item={item} attempt={attempt} question={question} ask={ask} />
    </article>
  );
}
