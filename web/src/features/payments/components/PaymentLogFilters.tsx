import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Form } from '@/shared/form/Form';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextField } from '@/shared/form/TextField';
import { Button } from '@/shared/ui/button';
import { paymentLogFiltersSchema, type PaymentLogFiltersValues } from '../schemas/paymentLogFiltersSchema';
import type { PaymentLogSearch } from '../schemas/paymentLogSearchSchema';
import { PaymentLogSelectField } from './PaymentLogSelectField';

export interface PaymentLogFiltersProps {
  search: PaymentLogSearch;
  onApply: (values: PaymentLogFiltersValues) => void;
  onClear: () => void;
}

const statuses = ['Pending', 'Succeeded', 'Failed', 'Refunded'] as const;
const plans = ['Base', 'AskTeacher'] as const;

export function PaymentLogFilters({ search, onApply, onClear }: PaymentLogFiltersProps) {
  const { t } = useTranslation('payments');
  const form = useForm<PaymentLogFiltersValues>({
    resolver: zodResolver(paymentLogFiltersSchema),
    defaultValues: {
      status: search.status ?? '',
      plan: search.plan ?? '',
      reference: search.reference ?? '',
      from: search.from ?? '',
      to: search.to ?? '',
    },
  });

  return (
    <Form form={form} onSubmit={onApply}>
      <div className="grid grid-cols-1 gap-3 md:grid-cols-5">
        <PaymentLogSelectField
          name="status"
          label={t('filters.status')}
          allLabel={t('filters.allStatuses')}
          options={statuses.map((status) => ({ value: status, label: t(`status.${status}`) }))}
        />
        <PaymentLogSelectField
          name="plan"
          label={t('filters.plan')}
          allLabel={t('filters.allPlans')}
          options={plans.map((plan) => ({ value: plan, label: t(`plan.${plan}`) }))}
        />
        <TextField<PaymentLogFiltersValues>
          name="reference"
          label={t('filters.reference')}
          description={t('filters.referenceHint')}
          dir="ltr"
        />
        <TextField<PaymentLogFiltersValues> name="from" label={t('filters.from')} type="date" />
        <TextField<PaymentLogFiltersValues> name="to" label={t('filters.to')} type="date" />
      </div>
      {search.studentId ? <p className="text-caption text-text-muted">{t('filters.studentActive')}</p> : null}
      <div className="flex flex-wrap gap-3">
        <SubmitButton>{t('filters.apply')}</SubmitButton>
        <Button variant="ghost" onClick={onClear}>
          {t('filters.clear')}
        </Button>
      </div>
    </Form>
  );
}
