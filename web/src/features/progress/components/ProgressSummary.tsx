import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { HeadlineCounterCard } from '@/features/mastery';
import { useGetMasteryOverview } from '@/shared/api/generated/mastery/mastery';

export function ProgressSummary() {
  const { t } = useTranslation('progress');
  const { data, error, isPending, isError, refetch } = useGetMasteryOverview();

  if (isPending) {
    return <ContentListSkeleton label={t('summary.loading')} />;
  }
  if (isError) {
    return (
      <ContentErrorState
        title={t('summary.errorTitle')}
        error={error}
        onRetry={() => {
          void refetch();
        }}
      />
    );
  }
  return <HeadlineCounterCard headline={data.headline} streakDays={Number(data.streakDays)} />;
}
