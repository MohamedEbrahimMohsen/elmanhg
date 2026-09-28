import { ChevronLeft, ChevronRight } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';

export interface PaginationProps {
  page: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}

export function Pagination({ page, totalPages, onPageChange }: PaginationProps) {
  const { t } = useTranslation();

  return (
    <nav aria-label={t('pagination.label')} className="flex items-center justify-between gap-3">
      <Button
        variant="secondary"
        size="sm"
        disabled={page <= 1}
        onClick={() => {
          onPageChange(page - 1);
        }}
      >
        <ChevronLeft aria-hidden className="size-4 rtl:rotate-180" />
        {t('pagination.previous')}
      </Button>
      <p aria-live="polite" className="text-caption text-text-muted">
        {t('pagination.status', { page, total: totalPages })}
      </p>
      <Button
        variant="secondary"
        size="sm"
        disabled={page >= totalPages}
        onClick={() => {
          onPageChange(page + 1);
        }}
      >
        {t('pagination.next')}
        <ChevronRight aria-hidden className="size-4 rtl:rotate-180" />
      </Button>
    </nav>
  );
}
