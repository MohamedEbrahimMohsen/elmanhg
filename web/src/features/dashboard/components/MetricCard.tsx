import { useId, type ReactNode } from 'react';
import type { UseQueryResult } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { ContentErrorState } from '@/features/content';

export interface MetricCardProps<T> {
  title: string;
  query: UseQueryResult<T, unknown>;
  variant?: 'kpi' | 'panel';
  ns?: 'dashboard' | 'teacherStats';
  children: (data: T) => ReactNode;
}

export function MetricCard<T>({ title, query, variant = 'kpi', ns = 'dashboard', children }: MetricCardProps<T>) {
  const { t } = useTranslation(ns);
  const id = useId();

  return (
    <section
      aria-labelledby={id}
      className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5"
    >
      <h2 id={id} className={variant === 'kpi' ? 'text-caption text-text-muted' : 'text-ui font-semibold text-text'}>
        {title}
      </h2>
      {query.isPending ? (
        <div role="status" aria-busy="true" aria-label={t('card.loading', { title })} className="flex flex-col gap-2">
          <div className="h-8 w-24 rounded-sm bg-soft" />
          <div className="h-4 rounded-sm bg-soft" />
          <div className="h-4 rounded-sm bg-soft" />
        </div>
      ) : query.isError ? (
        <ContentErrorState
          title={t('card.error', { title })}
          error={query.error}
          onRetry={() => {
            void query.refetch();
          }}
        />
      ) : (
        children(query.data)
      )}
    </section>
  );
}
