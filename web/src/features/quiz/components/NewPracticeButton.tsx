import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { nextQuizSize } from '../api/quizSession';
import { useStartQuiz } from '../hooks/useStartQuiz';

export interface NewPracticeButtonProps {
  lessonId: string;
  servedCount: number;
}

export function NewPracticeButton({ lessonId, servedCount }: NewPracticeButtonProps) {
  const { t } = useTranslation('quiz');
  const { start, isPending, errorCode } = useStartQuiz(lessonId);

  return (
    <div className="flex flex-col items-start gap-2">
      <Button
        disabled={isPending}
        onClick={() => {
          start(nextQuizSize(servedCount));
        }}
      >
        {t('result.newPractice')}
      </Button>
      {errorCode ? (
        <p role="alert" className="text-caption text-danger">
          {t([`common:errors.${errorCode}`, 'common:errors.UNHANDLED_EXCEPTION'])}
        </p>
      ) : null}
    </div>
  );
}
