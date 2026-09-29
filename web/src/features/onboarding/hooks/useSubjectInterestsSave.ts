import { useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { trackFunnelEvent } from '@/features/analytics';
import { invalidateMastery } from '@/features/mastery';
import { useSessionStore } from '@/features/session';
import { getGetSubjectInterestsQueryKey, useSaveSubjectInterests } from '@/shared/api/generated/students/students';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';

export interface SubjectInterestsSave {
  save: (subjectIds: string[]) => Promise<void>;
  skip: () => void;
  isPending: boolean;
}

export function useSubjectInterestsSave(needsOnboarding: boolean): SubjectInterestsSave {
  const { t } = useTranslation('onboarding');
  const queryClient = useQueryClient();
  const store = useSessionStore();
  const navigate = useNavigate();
  const mutation = useSaveSubjectInterests({
    mutation: {
      onSuccess: async () => {
        if (needsOnboarding) {
          trackFunnelEvent('OnboardingCompleted');
        }
        const session = store.get();
        if (session) {
          store.set({ ...session, needsOnboarding: false });
        }
        await queryClient.invalidateQueries({ queryKey: getGetSubjectInterestsQueryKey() });
        void invalidateMastery(queryClient);
        toast.success(t('toast.saved'));
        await navigate({ to: '/student' });
      },
    },
  });
  const save = (subjectIds: string[]) => mutation.mutateAsync({ data: { subjectIds } });

  return {
    save,
    skip: () => {
      save([]).catch((error: unknown) => {
        const code = error instanceof ApiError ? error.code : unhandledErrorCode;
        toast.error(t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION']));
      });
    },
    isPending: mutation.isPending,
  };
}
