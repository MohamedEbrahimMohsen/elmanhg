import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { AskAvatarButton, type AvatarContextInput } from '@/features/avatar';
import { RichTextViewer } from '@/features/content';
import { emptyAnswer, QuestionView } from '@/features/questions';
import { choiceReview, CorrectAnswer, describeCorrectAnswer, QuizReviewItem, toQuizQuestion } from '@/features/quiz';
import type { ExamItemResult } from '@/shared/api/generated/model';

export interface ExamReviewItemProps {
  item: ExamItemResult;
  sessionId: string;
}

export function ExamReviewItem({ item, sessionId }: ExamReviewItemProps) {
  const { t } = useTranslation('exam');
  const headingId = useId();
  const ask: AvatarContextInput = {
    entryPoint: 'ExamReview',
    sessionId,
    questionId: item.questionId,
    title: t('result.avatarTitle', { position: Number(item.position) }),
  };

  if (item.attempt) {
    return <QuizReviewItem item={item} attempt={item.attempt} ask={ask} />;
  }
  const question = toQuizQuestion(item);
  const correct = describeCorrectAnswer(question, item.correctAnswer);
  return (
    <article
      aria-labelledby={headingId}
      className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-4.5 shadow-1"
    >
      <h3 id={headingId} className="font-display text-h3 font-semibold">
        {t('result.reviewItem', { position: Number(item.position) })}
      </h3>
      <QuestionView
        question={question}
        answer={emptyAnswer()}
        onAnswerChange={() => undefined}
        disabled
        review={choiceReview(question, item.correctAnswer)}
      />
      <p className="text-ui font-semibold text-text-muted">{t('result.unanswered')}</p>
      {correct ? (
        <div className="flex flex-col gap-1">
          <p className="text-caption font-semibold text-text-muted">{t('result.correctAnswer')}</p>
          <CorrectAnswer view={correct} />
        </div>
      ) : null}
      {item.explanation ? <RichTextViewer html={item.explanation} /> : null}
      <AskAvatarButton context={ask} />
    </article>
  );
}
