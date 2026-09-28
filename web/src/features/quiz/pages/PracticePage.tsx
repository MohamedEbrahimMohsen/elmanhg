import { useTranslation } from 'react-i18next';
import { PracticeStart } from '../components/PracticeStart';

export interface PracticePageProps {
  lessonId: string;
}

export function PracticePage({ lessonId }: PracticePageProps) {
  const { t } = useTranslation('quiz');

  return (
    <section className="flex flex-col gap-4">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('practice.title')}</h1>
      <PracticeStart lessonId={lessonId} />
    </section>
  );
}
