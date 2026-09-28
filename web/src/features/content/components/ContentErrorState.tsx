import { useTranslation } from 'react-i18next';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { Button } from '@/shared/ui/button';

export interface ContentErrorStateProps {
  title: string;
  error: unknown;
  onRetry: () => void;
}

export function ContentErrorState({ title, error, onRetry }: ContentErrorStateProps) {
  const { t } = useTranslation('content');
  const errorCode = error instanceof ApiError ? error.code : unhandledErrorCode;

  return (
    <div role="alert" className="flex flex-col items-start gap-3 rounded-lg border border-danger bg-danger-soft p-4">
      <p className="text-ui font-semibold text-danger">{title}</p>
      <p className="text-caption text-text">{t([`common:errors.${errorCode}`, 'common:errors.UNHANDLED_EXCEPTION'])}</p>
      <Button variant="secondary" onClick={onRetry}>
        {t('common:actions.retry')}
      </Button>
    </div>
  );
}
