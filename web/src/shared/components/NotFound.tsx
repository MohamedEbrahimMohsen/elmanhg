import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { cn } from '@/shared/lib/utils';
import { layoutContainerClassName } from '@/shared/ui/layout';

export function NotFound() {
  const { t } = useTranslation();

  return (
    <section className={cn(layoutContainerClassName, 'py-6 in-[main]:px-0 lg:in-[main]:px-0')}>
      <div className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1">
        <h1 className="font-display text-h2 font-bold">{t('notFound.body')}</h1>
        <Link
          to="/"
          className="text-ui font-semibold text-accent focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
        >
          {t('notFound.back')}
        </Link>
      </div>
    </section>
  );
}
