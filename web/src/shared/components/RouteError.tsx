import { useRouter, type ErrorComponentProps } from '@tanstack/react-router';
import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { clientErrorReporter } from '@/shared/lib/clientErrorReporter';
import { cn } from '@/shared/lib/utils';
import { Button } from '@/shared/ui/button';
import { layoutContainerClassName } from '@/shared/ui/layout';

export function RouteError({ error, reset }: ErrorComponentProps) {
  const { t } = useTranslation();
  const router = useRouter();
  const code = error instanceof ApiError ? error.code : unhandledErrorCode;

  useEffect(() => {
    clientErrorReporter.report(error, 'Route');
  }, [error]);

  return (
    <section role="alert" className={cn(layoutContainerClassName, 'py-6 in-[main]:px-0 lg:in-[main]:px-0')}>
      <div className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1">
        <h1 className="font-display text-h2 font-bold">{t('error.title')}</h1>
        <p className="text-ui text-text-muted">{t([`errors.${code}`, 'errors.UNHANDLED_EXCEPTION'])}</p>
        <Button
          variant="secondary"
          onClick={() => {
            reset();
            void router.invalidate();
          }}
        >
          {t('actions.retry')}
        </Button>
      </div>
    </section>
  );
}
