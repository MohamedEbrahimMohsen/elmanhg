import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { AskTeacherLink } from '@/features/askTeacher';
import { QuestionView } from '@/features/questions';
import { useSession } from '@/features/session';
import { LazyPaywallDialog } from '@/features/subscription';
import type { SessionItemResult } from '@/shared/api/generated/model';
import { choiceReview } from '../api/correctAnswer';
import { mathDraftOwnerFor } from '../api/mathDraftOwner';
import { fromAnswerPayload, toQuizQuestion } from '../api/quizItem';
import { useQuizAnswer } from '../hooks/useQuizAnswer';
import { FeedbackPanel } from './FeedbackPanel';
import { QuizQuestionActions } from './QuizQuestionActions';
import { QuizQuestionHeading } from './QuizQuestionHeading';

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
  const draftOwner = mathDraftOwnerFor(useSession()?.userId, sessionId, item.questionId);
  const quiz = useQuizAnswer(sessionId, item, question, draftOwner);
  const attempt = item.attempt;
  const answer = attempt ? fromAnswerPayload(question, attempt.answer) : quiz.answer;

  return (
    <article
      aria-labelledby={headingId}
      className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-4.5 shadow-1"
    >
      <QuizQuestionHeading
        id={headingId}
        position={position}
        total={total}
        type={question.type}
        focusOnMount={focusOnMount}
      />
      <QuestionView
        question={question}
        answer={answer}
        onAnswerChange={quiz.setAnswer}
        disabled={attempt !== null || quiz.isChecking}
        review={attempt ? choiceReview(question, item.correctAnswer) : undefined}
        mathDraftOwner={draftOwner}
      />
      {quiz.showRequired ? (
        <p role="alert" className="text-caption text-danger">
          {t('session.answerRequired')}
        </p>
      ) : null}
      {attempt ? (
        <FeedbackPanel
          item={item}
          attempt={attempt}
          question={question}
          ask={{
            entryPoint: 'QuizQuestion',
            sessionId,
            questionId: item.questionId,
            title: t('avatar.questionTitle', { position }),
          }}
        >
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
      <LazyPaywallDialog reason={quiz.paywall} onClose={quiz.closePaywall} />
    </article>
  );
}
