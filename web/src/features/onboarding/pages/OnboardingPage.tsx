import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetSubjectInterests } from '@/shared/api/generated/students/students';
import { Button } from '@/shared/ui/button';
import { SubjectInterestsForm } from '../components/SubjectInterestsForm';
import { useSubjectInterestsSave } from '../hooks/useSubjectInterestsSave';

export function OnboardingPage() {
  const { t } = useTranslation('onboarding');
  const { data, error, isPending, isError, refetch } = useGetSubjectInterests();
  const emptySave = useSubjectInterestsSave(data?.needsOnboarding ?? false);
  const content = () => {
    if (isError) {
      return (
        <ContentErrorState
          title={t('page.errorTitle')}
          error={error}
          onRetry={() => {
            void refetch();
          }}
        />
      );
    }
    if (isPending) {
      return <ContentListSkeleton label={t('page.loading')} />;
    }
    if (data.subjects.length > 0) {
      return <SubjectInterestsForm interests={data} />;
    }
    return (
      <>
        <p className="text-ui text-text-muted">{t('page.empty')}</p>
        <Button className="self-start" disabled={emptySave.isPending} onClick={emptySave.skip}>
          {t('actions.continueHome')}
        </Button>
      </>
    );
  };

  return (
    <main id="main" className="mx-auto flex min-h-dvh max-w-layout flex-col gap-4 px-4 py-6 lg:px-6">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('page.title')}</h1>
      <p className="text-ui text-text-muted">{t('page.intro')}</p>
      {content()}
    </main>
  );
}
