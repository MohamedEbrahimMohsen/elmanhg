import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { defaultQuizSize, quizSizes } from '../api/quizSession';
import { useStartQuiz } from '../hooks/useStartQuiz';

export interface PracticeStartProps {
  lessonId: string;
}

export function PracticeStart({ lessonId }: PracticeStartProps) {
  const { t } = useTranslation('quiz');
  const headingId = useId();
  const { start, isPending, errorCode } = useStartQuiz(lessonId);

  return (
    <div className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
      <h2 id={headingId} className="font-display text-h2 font-bold lg:text-h2-desktop">
        {t('practice.choose')}
      </h2>
      <p className="text-caption text-text-muted">{t('practice.resumeHint')}</p>
      <div role="group" aria-labelledby={headingId} aria-busy={isPending} className="flex flex-wrap gap-3">
        {quizSizes.map((size) => (
          <Button
            key={size}
            variant={size === defaultQuizSize ? 'primary' : 'secondary'}
            disabled={isPending}
            onClick={() => {
              start(size);
            }}
          >
            {t('practice.size', { count: size })}
          </Button>
        ))}
      </div>
      {errorCode === 'SESSION_NO_SERVABLE_QUESTIONS' ? (
        <p role="status" className="text-ui text-text-muted">
          {t('practice.empty')}
        </p>
      ) : errorCode ? (
        <p role="alert" className="text-caption text-danger">
          {t([`common:errors.${errorCode}`, 'common:errors.UNHANDLED_EXCEPTION'])}
        </p>
      ) : null}
    </div>
  );
}
