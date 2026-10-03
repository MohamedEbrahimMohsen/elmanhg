import { useTranslation } from 'react-i18next';
import { useGetDashboardSuccessRate } from '@/shared/api/generated/dashboard/dashboard';
import { AskTeacherCard } from '../components/AskTeacherCard';
import { ContentCard } from '../components/ContentCard';
import { DashboardCharts } from '../components/DashboardCharts';
import { DashboardFilters } from '../components/DashboardFilters';
import { FunnelCard } from '../components/FunnelCard';
import { GrowthTiles } from '../components/GrowthTiles';
import { LearningTiles } from '../components/LearningTiles';
import { MetricCard } from '../components/MetricCard';
import { PaymentsCard } from '../components/PaymentsCard';
import { QuestionTypesCard } from '../components/QuestionTypesCard';
import { StudentsCard } from '../components/StudentsCard';
import { SubscribersCard } from '../components/SubscribersCard';
import { SuccessRateBreakdown } from '../components/SuccessRateBreakdown';
import { ValidationCard } from '../components/ValidationCard';
import { useDashboardFilters } from '../hooks/useDashboardFilters';
import { registerDashboardLocales } from '../locales';

registerDashboardLocales();

export function DashboardPage() {
  const { t } = useTranslation('dashboard');
  const f = useDashboardFilters();
  const subjectSelected = f.subjectId !== undefined;
  const successRate = useGetDashboardSuccessRate(f.subjectRangeParams);

  return (
    <section className="flex flex-col gap-4">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('page.title')}</h1>
      <p className="text-caption text-text-muted">{t('page.intro')}</p>
      <DashboardFilters
        days={f.days}
        subjectId={f.subjectId}
        range={f.range}
        onDaysChange={f.setDays}
        onSubjectChange={f.setSubject}
      />
      <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
        <GrowthTiles range={f.range} contentParams={f.contentParams} subjectSelected={subjectSelected} />
        <LearningTiles params={f.subjectRangeParams} />
      </div>
      <div className="grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3">
        <StudentsCard params={f.range} subjectSelected={subjectSelected} />
        <SubscribersCard params={f.range} subjectSelected={subjectSelected} />
        <PaymentsCard params={f.range} subjectSelected={subjectSelected} />
        <AskTeacherCard params={f.subjectRangeParams} />
        <ValidationCard params={f.subjectRangeParams} />
        <ContentCard params={f.contentParams} />
        <QuestionTypesCard params={f.contentParams} />
        <FunnelCard params={f.range} subjectSelected={subjectSelected} />
      </div>
      <DashboardCharts
        subjectRangeParams={f.subjectRangeParams}
        rangeParams={f.range}
        subjectSelected={subjectSelected}
      />
      <MetricCard title={t('breakdown.title')} query={successRate} variant="panel">
        {(data) => <SuccessRateBreakdown data={data} subjectSelected={subjectSelected} />}
      </MetricCard>
    </section>
  );
}
