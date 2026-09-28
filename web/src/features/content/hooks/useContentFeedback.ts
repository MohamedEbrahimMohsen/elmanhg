import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';

export function useContentFeedback(): { success: (key: string) => void; failure: (error: unknown) => void } {
  const { t } = useTranslation('content');

  return {
    success: (key) => {
      toast(t(key));
    },
    failure: (error) => {
      const code = error instanceof ApiError ? error.code : unhandledErrorCode;
      toast.error(t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION']));
    },
  };
}
