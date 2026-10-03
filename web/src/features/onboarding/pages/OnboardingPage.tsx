import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetSubjectInterests } from '@/shared/api/generated/students/students';
import { BrandBar } from '@/shared/components/BrandBar';
import { cn } from '@/shared/lib/utils';
import { Button } from '@/shared/ui/button';
import { layoutContainerClassName } from '@/shared/ui/layout';
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
    <>
      <BrandBar />
      <main id="main" className={cn(layoutContainerClassName, 'flex flex-col gap-4 py-6')}>
        <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('page.title')}</h1>
        <p className="text-ui text-text-muted">{t('page.intro')}</p>
        {content()}
      </main>
    </>
  );
}
