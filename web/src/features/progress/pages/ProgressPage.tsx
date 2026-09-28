import { useTranslation } from 'react-i18next';
import { ProgressSummary } from '../components/ProgressSummary';
import { SessionHistorySection } from '../components/SessionHistorySection';
import { SubjectProgressSection } from '../components/SubjectProgressSection';
import { WeakSpotsSection } from '../components/WeakSpotsSection';

export function ProgressPage() {
  const { t } = useTranslation('progress');

  return (
    <section className="flex flex-col gap-4">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('page.title')}</h1>
      <ProgressSummary />
      <SubjectProgressSection />
      <WeakSpotsSection />
      <SessionHistorySection />
    </section>
  );
}
