import { getRouteApi, Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetMyUsage } from '@/shared/api/generated/subscriptions/subscriptions';
import { useGetTeacherReplyDeadline } from '@/shared/api/generated/teacher-threads/teacher-threads';
import { formatNumber } from '@/shared/lib/format';
import { AskTeacherForm } from '../components/AskTeacherForm';
import { AskTeacherUpsell } from '../components/AskTeacherUpsell';

const routeApi = getRouteApi('/student/ask-new');

function AskTeacherNewContent() {
  const { t, i18n } = useTranslation('askTeacher');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const search = routeApi.useSearch();
  const { data, error, isPending, isError, refetch } = useGetMyUsage();
  const deadline = useGetTeacherReplyDeadline();

  if (isPending) {
    return <ContentListSkeleton label={t('context.loading')} />;
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
  if (Number(data.askTeacherQuestionsRemainingThisMonth) === 0) {
    return (
      <div className="flex flex-col items-start gap-3 rounded-lg border border-border bg-warning-soft p-4">
        <p className="text-ui text-text">
          {t('new.quotaUsed', { limit: formatNumber(Number(data.monthlyAskTeacherQuestionLimit), lng) })}
        </p>
        <Link
          to="/student/ask"
          className="rounded-sm text-ui text-accent underline focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
        >
          {t('new.backToList')}
        </Link>
      </div>
    );
  }
  return <AskTeacherForm search={search} replyDeadline={deadline.data} />;
}

export function AskTeacherNewPage() {
  const { t } = useTranslation('askTeacher');

  return (
    <section className="flex flex-col gap-4">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('new.title')}</h1>
      <AskTeacherNewContent />
    </section>
  );
}
