import { useId } from 'react';
import { useController } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { useGetAuditLogResourceTypes } from '@/shared/api/generated/audit-logs/audit-logs';
import { Label } from '@/shared/ui/label';
import type { AuditLogFiltersValues } from '../schemas/auditLogFiltersSchema';

export function ResourceTypeField() {
  const { t } = useTranslation('audit');
  const id = useId();
  const { data: resourceTypes = [] } = useGetAuditLogResourceTypes();
  const {
    field: { ref, name, value, onChange, onBlur },
  } = useController<AuditLogFiltersValues, 'resourceType'>({ name: 'resourceType' });

  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{t('filters.resourceType')}</Label>
      <select
        id={id}
        ref={ref}
        name={name}
        value={value}
        onChange={onChange}
        onBlur={onBlur}
        className="h-11 w-full rounded-sm border border-border-strong bg-surface px-3 text-ui text-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
      >
        <option value="">{t('filters.allResourceTypes')}</option>
        {resourceTypes.map((resourceType) => (
          <option key={resourceType} value={resourceType}>
            {resourceType}
          </option>
        ))}
      </select>
    </div>
  );
}
