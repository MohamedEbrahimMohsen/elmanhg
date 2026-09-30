import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { AskTeacherLink } from '@/features/askTeacher';
import { RichTextViewer } from '@/features/content';
import { emptyAnswer, QuestionView } from '@/features/questions';
import { PaywallDialog } from '@/features/subscription';
import { essayAnswerText, isWrittenEssay } from '../api/essayItem';
import { toQuizQuestion } from '../api/quizItem';
import { useQuizEssay } from '../hooks/useQuizEssay';
import { EssayAnswerText } from './EssayAnswerText';
import { EssayDraftStatus } from './EssayDraftStatus';
import { EssayGradeStatus } from './EssayGradeStatus';
import { FeedbackPanel } from './FeedbackPanel';
import { QuizQuestionActions } from './QuizQuestionActions';
import type { QuizQuestionCardProps } from './QuizQuestionCard';
import { QuizQuestionHeading } from './QuizQuestionHeading';

export function QuizEssayCard({
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
  const essay = useQuizEssay(sessionId, item, question);
  const attempt = item.attempt;
  const submitted = attempt !== null || item.pendingAnswer !== null;

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
      {submitted ? (
        <div className="text-body font-semibold">
          <RichTextViewer html={item.stem} />
        </div>
      ) : (
        <>
          <QuestionView
            question={question}
            answer={{ ...emptyAnswer(), text: essay.text }}
            onAnswerChange={(answer) => {
              essay.change(answer.text);
            }}
            disabled={essay.isSubmitting}
          />
          <EssayDraftStatus status={essay.draftStatus} />
          {essay.problem ? (
            <p role="alert" className="text-caption text-danger">
              {essay.problem === 'required'
                ? t('session.answerRequired')
                : t('session.essayOverLimit', { max: question.maxWords })}
            </p>
          ) : null}
        </>
      )}
      {submitted && isWrittenEssay(item) ? (
        <>
          <EssayAnswerText text={essayAnswerText(item)} />
          <EssayGradeStatus sessionId={sessionId} questionId={item.questionId} onGraded={essay.refreshSession} />
          {item.explanation ? (
            <div className="flex flex-col gap-1">
              <p className="text-caption font-semibold text-text">{t('feedback.explanation')}</p>
              <div className="text-ui text-text-muted">
                <RichTextViewer html={item.explanation} />
              </div>
            </div>
          ) : null}
        </>
      ) : null}
      {attempt && !isWrittenEssay(item) ? (
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
        answered={submitted}
        isLast={isLast}
        isChecking={essay.isSubmitting}
        onCheck={essay.submit}
        onNext={onNext}
        isEssay
      />
      <PaywallDialog reason={essay.paywall} onClose={essay.closePaywall} />
    </article>
  );
}
