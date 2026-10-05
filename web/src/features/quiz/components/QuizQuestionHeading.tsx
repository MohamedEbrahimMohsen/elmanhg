import { useTranslation } from 'react-i18next';
import type { StudentQuestion } from '@/features/questions';

export interface QuizQuestionHeadingProps {
  id: string;
  position: number;
  total: number;
  type: StudentQuestion['type'];
  focusOnMount: boolean;
}

export function QuizQuestionHeading({ id, position, total, type, focusOnMount }: QuizQuestionHeadingProps) {
  const { t } = useTranslation('quiz');

  return (
    <div className="flex flex-wrap items-center gap-2">
      <h2
        id={id}
        tabIndex={-1}
        ref={
          focusOnMount
            ? (node) => {
                node?.focus();
              }
            : undefined
        }
        className="font-display text-h3 font-bold focus-visible:outline-hidden"
      >
        {t('session.counter', { position, total })}
      </h2>
      <span className="rounded-full bg-soft px-2.5 py-0.5 text-micro font-bold text-text-muted">
        {t(`questions:types.${type}`)}
      </span>
    </div>
  );
}
