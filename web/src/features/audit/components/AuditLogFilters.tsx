import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Form } from '@/shared/form/Form';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextField } from '@/shared/form/TextField';
import { Button } from '@/shared/ui/button';
import { auditLogFiltersSchema, type AuditLogFiltersValues } from '../schemas/auditLogFiltersSchema';
import type { AuditLogSearch } from '../schemas/auditLogSearchSchema';
import { ResourceTypeField } from './ResourceTypeField';

export interface AuditLogFiltersProps {
  search: AuditLogSearch;
  onApply: (values: AuditLogFiltersValues) => void;
  onClear: () => void;
}

export function AuditLogFilters({ search, onApply, onClear }: AuditLogFiltersProps) {
  const { t } = useTranslation('audit');
  const form = useForm<AuditLogFiltersValues>({
    resolver: zodResolver(auditLogFiltersSchema),
    defaultValues: {
      actor: search.actor ?? '',
      resourceType: search.resourceType ?? '',
      from: search.from ?? '',
      to: search.to ?? '',
    },
  });

  return (
    <Form form={form} onSubmit={onApply}>
      <div className="grid grid-cols-1 gap-3 md:grid-cols-4">
        <TextField<AuditLogFiltersValues>
          name="actor"
          label={t('filters.actor')}
          description={t('filters.actorHint')}
        />
        <ResourceTypeField />
        <TextField<AuditLogFiltersValues> name="from" label={t('filters.from')} type="date" />
        <TextField<AuditLogFiltersValues> name="to" label={t('filters.to')} type="date" />
      </div>
      <div className="flex flex-wrap gap-3">
        <SubmitButton>{t('filters.apply')}</SubmitButton>
        <Button variant="ghost" onClick={onClear}>
          {t('filters.clear')}
        </Button>
      </div>
    </Form>
  );
}
