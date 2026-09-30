import { lazy, Suspense } from 'react';
import { preload } from 'react-dom';
import { useTranslation } from 'react-i18next';
import { ContentListSkeleton } from '@/features/content';
import { DailyQuizCounter } from '@/features/subscription';
import type { SessionResult } from '@/shared/api/generated/model';
import { questionImageSources } from '../api/quizItem';
import { useQuizNavigation } from '../hooks/useQuizNavigation';
import { QuizQuestionCard } from './QuizQuestionCard';

const QuizEssayCard = lazy(() => import('./QuizEssayCard').then((module) => ({ default: module.QuizEssayCard })));

export interface QuizRunnerProps {
  session: SessionResult;
}

export function QuizRunner({ session }: QuizRunnerProps) {
  const { t } = useTranslation('quiz');
  const nav = useQuizNavigation(session);
  for (const src of nav.nextItem ? questionImageSources(nav.nextItem) : []) {
    preload(src, { as: 'image' });
  }

  return (
    <section className="flex flex-col gap-4">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('session.title')}</h1>
      {session.isTestMode ? null : <DailyQuizCounter variant="quiz" />}
      {nav.item.type === 'Essay' ? (
        <Suspense fallback={<ContentListSkeleton label={t('session.loading')} />}>
          <QuizEssayCard
            key={nav.position}
            sessionId={session.id}
            item={nav.item}
            position={nav.position}
            total={nav.total}
            isLast={nav.isLast}
            focusOnMount={nav.focusOnMount}
            onNext={nav.next}
          />
        </Suspense>
      ) : (
        <QuizQuestionCard
          key={nav.position}
          sessionId={session.id}
          item={nav.item}
          position={nav.position}
          total={nav.total}
          isLast={nav.isLast}
          focusOnMount={nav.focusOnMount}
          onNext={nav.next}
        />
      )}
    </section>
  );
}
