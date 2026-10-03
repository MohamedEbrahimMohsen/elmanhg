import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetMultiUnitExamOverview } from '@/shared/api/generated/exams/exams';
import { useMultiExamSearch } from '../hooks/useMultiExamSearch';
import { MultiExamPreview } from './MultiExamPreview';
import { MultiExamSizePicker } from './MultiExamSizePicker';
import { MultiExamUnitPicker } from './MultiExamUnitPicker';

export interface MultiExamSubjectSectionProps {
  subjectId: string;
}

// PRD §7.5: a multi-unit exam covers two or more units.
const minimumUnits = 2;

export function MultiExamSubjectSection({ subjectId }: MultiExamSubjectSectionProps) {
  const { t } = useTranslation('exam');
  const { data, error, isPending, isError, refetch } = useGetMultiUnitExamOverview(subjectId);
  const search = useMultiExamSearch();

  if (isPending) {
    return <ContentListSkeleton label={t('multi.loading')} />;
  }
  if (isError) {
    return (
      <ContentErrorState
        title={t('multi.errorTitle')}
        error={error}
        onRetry={() => {
          void refetch();
        }}
      />
    );
  }
  if (data.units.length === 0) {
    return <p className="text-ui text-text-muted">{t('multi.noUnits')}</p>;
  }

  const sizes = data.sizes.map(Number);
  const selectedIds = data.units
    .filter((unit) => unit.hasBlueprint && search.unitIds.includes(unit.unitId))
    .map((unit) => unit.unitId);
  const size = search.size !== undefined && sizes.includes(search.size) ? search.size : (sizes[0] ?? 0);
  const inProgress = data.inProgressExam;

  return (
    <div className="flex flex-col gap-4">
      {inProgress ? (
        <div className="flex flex-col items-start gap-2 rounded-lg border border-warning bg-warning-soft p-4">
          <p className="text-ui text-text">{t('start.otherInProgress')}</p>
          <Link
            to="/student/exam/$sessionId"
            params={{ sessionId: inProgress.sessionId }}
            className="inline-flex min-h-11 items-center rounded-sm text-ui text-accent focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
          >
            {t('start.openOther')}
          </Link>
        </div>
      ) : null}
      <MultiExamUnitPicker units={data.units} selected={selectedIds} onToggle={search.toggleUnit} />
      <MultiExamSizePicker sizes={sizes} value={size} onChange={search.selectSize} />
      {selectedIds.length < minimumUnits ? (
        <p className="text-ui text-text-muted">{t('multi.chooseTwo')}</p>
      ) : (
        <MultiExamPreview subjectId={subjectId} unitIds={selectedIds} size={size} canStart={!inProgress} />
      )}
    </div>
  );
}
