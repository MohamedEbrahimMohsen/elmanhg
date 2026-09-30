import { useTranslation } from 'react-i18next';
import { TeacherStatsCard } from '../components/TeacherStatsCard';
import { registerTeacherStatsLocales } from '../teacherStatsLocales';

registerTeacherStatsLocales();

export function TeacherStatsPage() {
  const { t } = useTranslation('teacherStats');

  return (
    <section className="flex flex-col gap-4">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('page.title')}</h1>
      <p className="text-caption text-text-muted">{t('page.intro')}</p>
      <TeacherStatsCard />
    </section>
  );
}
