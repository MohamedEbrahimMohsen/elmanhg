import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { useFinishQuiz } from '../hooks/useFinishQuiz';

export interface QuizQuestionActionsProps {
  sessionId: string;
  answered: boolean;
  isLast: boolean;
  isChecking: boolean;
  onCheck: () => void;
  onNext: () => void;
}

export function QuizQuestionActions({
  sessionId,
  answered,
  isLast,
  isChecking,
  onCheck,
  onNext,
}: QuizQuestionActionsProps) {
  const { t } = useTranslation('quiz');
  const { finish, isPending } = useFinishQuiz(sessionId);

  return (
    <div className="flex flex-col gap-3">
      {answered ? (
        <Button className="min-h-12 w-full" disabled={isPending} onClick={isLast ? finish : onNext}>
          {t(isLast ? 'session.showResult' : 'session.next')}
        </Button>
      ) : (
        <>
          <Button className="min-h-12 w-full" disabled={isChecking} onClick={onCheck}>
            {t(isChecking ? 'session.checking' : 'session.check')}
          </Button>
          <Button variant="secondary" disabled={isPending || isChecking} onClick={finish}>
            {t('session.finish')}
          </Button>
        </>
      )}
    </div>
  );
}
