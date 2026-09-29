import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetMyUsage } from '@/shared/api/generated/subscriptions/subscriptions';
import { formatNumber } from '@/shared/lib/format';
import { Button } from '@/shared/ui/button';
import { AskTeacherUpsell } from '../components/AskTeacherUpsell';
import { ThreadList } from '../components/ThreadList';

function AskTeacherListContent() {
  const { t, i18n } = useTranslation('askTeacher');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const { data, error, isPending, isError, refetch } = useGetMyUsage();

  if (isPending) {
    return <ContentListSkeleton label={t('list.loading')} />;
  }
  if (isError) {
    return (
      <ContentErrorState
        title={t('list.errorTitle')}
        error={error}
        onRetry={() => {
          void refetch();
        }}
      />
    );
  }
  if (!data.hasAskTeacher) {
    return <AskTeacherUpsell />;
  }
  return (
    <>
      <div className="flex flex-wrap items-center gap-3">
        <Button asChild>
          <Link to="/student/ask-new">{t('list.new')}</Link>
        </Button>
        <p className="text-caption text-text-muted">
          {t('list.allowance', {
            used: formatNumber(Number(data.askTeacherQuestionsUsedThisMonth), lng),
            limit: formatNumber(Number(data.monthlyAskTeacherQuestionLimit), lng),
          })}
        </p>
      </div>
      <ThreadList />
    </>
  );
}

export function AskTeacherListPage() {
  const { t } = useTranslation('askTeacher');

  return (
    <section className="flex flex-col gap-4">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('list.title')}</h1>
      <AskTeacherListContent />
    </section>
  );
}
