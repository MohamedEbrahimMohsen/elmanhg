import { useState } from 'react';
import { getRouteApi, Link } from '@tanstack/react-router';
import { ChevronLeft } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetStudentProfile } from '@/shared/api/generated/students/students';
import { GrantPlanDialog, type GrantPlanTarget } from '../components/GrantPlanDialog';
import { StudentHistorySection } from '../components/StudentHistorySection';
import { StudentProfileCard } from '../components/StudentProfileCard';
import { StudentProgressSection } from '../components/StudentProgressSection';
import { StudentSubscriptionsCard } from '../components/StudentSubscriptionsCard';
import { UserStatusDialog, type UserStatusTarget } from '../components/UserStatusDialog';
import { registerUsersLocales } from '../locales';

registerUsersLocales();

const routeApi = getRouteApi('/admin/student/$studentId');

export function StudentDetailPage() {
  const { t } = useTranslation('users');
  const { studentId } = routeApi.useParams();
  const { data: profile, error, isPending, isError, refetch } = useGetStudentProfile(studentId);
  const [statusTarget, setStatusTarget] = useState<UserStatusTarget | null>(null);
  const [grantTarget, setGrantTarget] = useState<GrantPlanTarget | null>(null);

  const renderProfile = () => {
    if (isPending) {
      return <ContentListSkeleton label={t('student.loading')} />;
    }
    if (isError) {
      return (
        <ContentErrorState
          title={t('student.errorTitle')}
          error={error}
          onRetry={() => {
            void refetch();
          }}
        />
      );
    }
    const statusTargetOf = {
      id: profile.id,
      displayName: profile.displayName,
      role: 'Student',
      status: profile.status,
    } as const;
    return (
      <>
        <StudentProfileCard
          profile={profile}
          onSuspend={() => {
            setStatusTarget(statusTargetOf);
          }}
          onReactivate={() => {
            setStatusTarget(statusTargetOf);
          }}
          onGrant={(plan) => {
            setGrantTarget({ studentId: profile.id, displayName: profile.displayName, plan });
          }}
        />
        <StudentSubscriptionsCard subscriptions={profile.subscriptions} studentName={profile.displayName} />
        <StudentProgressSection studentId={studentId} />
        <StudentHistorySection studentId={studentId} />
      </>
    );
  };

  return (
    <section className="flex flex-col gap-4">
      <Link
        to="/admin/users"
        className="inline-flex min-h-11 items-center gap-1 self-start rounded-sm text-caption font-bold text-accent-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
      >
        <ChevronLeft aria-hidden className="size-4 rtl:rotate-180" />
        {t('student.back')}
      </Link>
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">
        {profile?.displayName ?? t('student.title')}
      </h1>
      {renderProfile()}
      <UserStatusDialog
        target={statusTarget}
        onOpenChange={(open) => {
          if (!open) {
            setStatusTarget(null);
          }
        }}
      />
      <GrantPlanDialog
        target={grantTarget}
        onOpenChange={(open) => {
          if (!open) {
            setGrantTarget(null);
          }
        }}
      />
    </section>
  );
}
