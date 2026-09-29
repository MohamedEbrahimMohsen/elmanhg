import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { AskTeacherLink } from '@/features/askTeacher';
import { QuestionView } from '@/features/questions';
import { PaywallDialog } from '@/features/subscription';
import type { SessionItemResult } from '@/shared/api/generated/model';
import { choiceReview } from '../api/correctAnswer';
import { fromAnswerPayload, toQuizQuestion } from '../api/quizItem';
import { useQuizAnswer } from '../hooks/useQuizAnswer';
import { FeedbackPanel } from './FeedbackPanel';
import { QuizQuestionActions } from './QuizQuestionActions';

export interface QuizQuestionCardProps {
  sessionId: string;
  item: SessionItemResult;
  position: number;
  total: number;
  isLast: boolean;
  focusOnMount: boolean;
  onNext: () => void;
}

export function QuizQuestionCard({
  sessionId,
  item,
  position,
  total,
  isLast,
  focusOnMount,
  onNext,
}: QuizQuestionCardProps) {
  const { t } = useTranslation('quiz');
  const headingId = useId();
  const question = toQuizQuestion(item);
  const quiz = useQuizAnswer(sessionId, item, question);
  const attempt = item.attempt;
  const answer = attempt ? fromAnswerPayload(question, attempt.answer) : quiz.answer;

  return (
    <article
      aria-labelledby={headingId}
      className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-4.5 shadow-1"
    >
      <div className="flex flex-wrap items-center gap-2">
        <h2
          id={headingId}
          tabIndex={-1}
          ref={
            focusOnMount
              ? (node) => {
                  node?.focus();
                }
              : undefined
          }
          className="font-display text-h3 font-semibold focus-visible:outline-hidden"
        >
          {t('session.counter', { position, total })}
        </h2>
        <span className="rounded-full bg-soft px-2.5 py-0.5 text-micro font-semibold text-text-muted">
          {t(`questions:types.${question.type}`)}
        </span>
      </div>
      <QuestionView
        question={question}
        answer={answer}
        onAnswerChange={quiz.setAnswer}
        disabled={attempt !== null || quiz.isChecking}
        review={attempt ? choiceReview(question, item.correctAnswer) : undefined}
      />
      {quiz.showRequired ? (
        <p role="alert" className="text-caption text-danger">
          {t('session.answerRequired')}
        </p>
      ) : null}
      {attempt ? (
        <FeedbackPanel item={item} attempt={attempt} question={question}>
          <AskTeacherLink attemptId={attempt.id} />
        </FeedbackPanel>
      ) : null}
      <QuizQuestionActions
        sessionId={sessionId}
        answered={attempt !== null}
        isLast={isLast}
        isChecking={quiz.isChecking}
        onCheck={quiz.check}
        onNext={onNext}
      />
      <PaywallDialog reason={quiz.paywall} onClose={quiz.closePaywall} />
    </article>
  );
}
