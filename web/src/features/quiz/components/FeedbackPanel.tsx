import type { ReactNode } from 'react';
import { CircleAlert, CircleCheck, CircleX, Clock } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { AskAvatarButton, type AvatarContextInput } from '@/features/avatar';
import { RichTextViewer } from '@/features/content';
import type { StudentQuestion } from '@/features/questions';
import type { AttemptResult } from '@/shared/api/generated/model';
import type { QuizItemContent } from '../api/quizItem';
import { formatNumber } from '@/shared/lib/format';
import { cn } from '@/shared/lib/utils';
import { describeCorrectAnswer } from '../api/correctAnswer';
import { CorrectAnswer } from './CorrectAnswer';

export interface FeedbackPanelProps {
  item: QuizItemContent;
  attempt: AttemptResult;
  question: StudentQuestion;
  ask: AvatarContextInput;
  children?: ReactNode;
}

const verdicts = {
  Correct: {
    icon: CircleCheck,
    panel: 'border-success bg-success-soft',
    color: 'text-success',
    key: 'feedback.correct',
  },
  Partial: {
    icon: CircleAlert,
    panel: 'border-warning bg-warning-soft',
    color: 'text-warning',
    key: 'feedback.partial',
  },
  Incorrect: { icon: CircleX, panel: 'border-danger bg-danger-soft', color: 'text-danger', key: 'feedback.incorrect' },
  InReview: { icon: Clock, panel: 'border-warning bg-warning-soft', color: 'text-warning', key: 'feedback.inReview' },
} as const;

function toVerdict(attempt: AttemptResult): keyof typeof verdicts {
  if (attempt.awaitsReview) {
    return 'InReview';
  }
  return attempt.outcome === 'Correct' || attempt.outcome === 'Partial' ? attempt.outcome : 'Incorrect';
}

export function FeedbackPanel({ item, attempt, question, ask, children }: FeedbackPanelProps) {
  const { t, i18n } = useTranslation('quiz');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const inReview = attempt.awaitsReview;
  const verdict = verdicts[toVerdict(attempt)];
  const Icon = verdict.icon;
  const view = inReview ? null : describeCorrectAnswer(question, item.correctAnswer);
  const explanation = inReview ? null : item.explanation;

  return (
    <div
      role="group"
      aria-label={t('feedback.label')}
      className={cn(
        'flex flex-col gap-3 rounded-md border px-3.5 py-3 transition-opacity duration-(--ds-motion-base-duration) ease-(--ds-motion-base-easing) starting:opacity-0',
        verdict.panel,
      )}
    >
      <div role="status" className="flex items-center gap-3">
        <Icon aria-hidden className={cn('size-6.5 shrink-0', verdict.color)} />
        <div>
          <p className="text-ui font-semibold text-text">{t(verdict.key)}</p>
          <p className="text-caption text-text">
            {inReview
              ? t('feedback.inReviewHint')
              : t('feedback.score', {
                  score: formatNumber(Number(attempt.score), lng),
                  maxScore: formatNumber(Number(item.maxScore), lng),
                })}
          </p>
        </div>
      </div>
      {attempt.feedback ? <p className="text-caption text-text-muted">{attempt.feedback}</p> : null}
      {view ? (
        <div className="flex flex-col gap-1">
          <p className="text-caption font-semibold text-text">{t('feedback.correctAnswer')}</p>
          <CorrectAnswer view={view} />
        </div>
      ) : null}
      {explanation ? (
        <div className="flex flex-col gap-1">
          <p className="text-caption font-semibold text-text">{t('feedback.explanation')}</p>
          <div className="text-ui text-text-muted">
            <RichTextViewer html={explanation} />
          </div>
        </div>
      ) : null}
      <div className="flex flex-wrap items-start gap-2">
        <AskAvatarButton context={ask} />
        {children}
      </div>
    </div>
  );
}
