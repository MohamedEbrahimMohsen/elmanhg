import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { useGetSubjects } from '@/shared/api/generated/subjects/subjects';
import { Label } from '@/shared/ui/label';
import { Select } from '@/shared/ui/select';
import { dashboardPeriods, type DashboardPeriod, type DashboardRange } from '../api/dashboardRange';
import { formatDay } from '../api/metricFormat';

export interface DashboardFiltersProps {
  days: DashboardPeriod;
  subjectId: string | undefined;
  range: DashboardRange;
  onDaysChange: (days: DashboardPeriod) => void;
  onSubjectChange: (subjectId: string) => void;
}

export function DashboardFilters({ days, subjectId, range, onDaysChange, onSubjectChange }: DashboardFiltersProps) {
  const { t, i18n } = useTranslation('dashboard');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const query = useGetSubjects();
  const subjects = query.data ?? [];
  const subjectFieldId = useId();
  const periodFieldId = useId();

  return (
    <div
      role="group"
      aria-label={t('filters.label')}
      className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1"
    >
      <div className="flex flex-col gap-3 md:flex-row md:items-end">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor={subjectFieldId}>{t('filters.subject')}</Label>
          <Select
            id={subjectFieldId}
            value={subjectId ?? ''}
            onChange={(event) => {
              onSubjectChange(event.target.value);
            }}
            className="md:w-60"
          >
            <option value="">{t('filters.allSubjects')}</option>
            {subjects.map((subject) => (
              <option key={subject.id} value={subject.id}>
                {subject.name}
              </option>
            ))}
          </Select>
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor={periodFieldId}>{t('filters.period')}</Label>
          <Select
            id={periodFieldId}
            value={String(days)}
            onChange={(event) => {
              const next = dashboardPeriods.find((period) => String(period) === event.target.value);
              if (next !== undefined) {
                onDaysChange(next);
              }
            }}
            className="md:w-60"
          >
            {dashboardPeriods.map((period) => (
              <option key={period} value={String(period)}>
                {t(`filters.periods.${String(period)}`)}
              </option>
            ))}
          </Select>
        </div>
      </div>
      <p className="text-caption text-text-muted">
        {t('filters.range', { from: formatDay(range.from, lng), to: formatDay(range.to, lng) })}
      </p>
    </div>
  );
}
