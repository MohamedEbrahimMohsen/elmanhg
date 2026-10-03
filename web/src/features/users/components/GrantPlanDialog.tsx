import { useId } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useController, useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import type { PlanPriceResult } from '@/shared/api/generated/model';
import { useGetPlanCatalogue } from '@/shared/api/generated/plans/plans';
import type { ServerErrorFields } from '@/shared/form/applyServerErrors';
import { Form } from '@/shared/form/Form';
import { FormRootError } from '@/shared/form/FormRootError';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { Dialog, DialogContent } from '@/shared/ui/dialog';
import { Label } from '@/shared/ui/label';
import { Select } from '@/shared/ui/select';
import { useGrantPlan } from '../hooks/useGrantPlan';
import { grantPlanSchema, type GrantPlanValues } from '../schemas/grantPlanSchema';

export interface GrantPlanTarget {
  studentId: string;
  displayName: string;
  plan: 'Base' | 'AskTeacher';
}

export interface GrantPlanDialogProps {
  target: GrantPlanTarget | null;
  onOpenChange: (open: boolean) => void;
}

const serverErrorFields: ServerErrorFields<GrantPlanValues> = {
  COMPLIMENTARY_PERIOD_UNAVAILABLE: 'period',
  COMPLIMENTARY_PERIOD_INVALID: 'period',
};

function PeriodSelect({ prices }: { prices: PlanPriceResult[] }) {
  const { t } = useTranslation('users');
  const { t: tCommon } = useTranslation();
  const id = useId();
  const {
    field: { ref, name, value, onChange, onBlur },
    fieldState,
  } = useController<GrantPlanValues, 'period'>({ name: 'period' });

  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{t('grant.period')}</Label>
      <Select
        id={id}
        ref={ref}
        name={name}
        value={value}
        onChange={onChange}
        onBlur={onBlur}
        aria-invalid={fieldState.invalid}
      >
        {prices.map((price) => (
          <option key={price.period} value={price.period}>
            {t(`grant.${price.period}`, { months: Number(price.months) })}
          </option>
        ))}
      </Select>
      {fieldState.error ? (
        <p className="text-caption text-danger">
          {tCommon([fieldState.error.message ?? '', 'errors.UNHANDLED_EXCEPTION'])}
        </p>
      ) : null}
    </div>
  );
}

function GrantPlanForm({
  target,
  prices,
  onDone,
}: {
  target: GrantPlanTarget;
  prices: PlanPriceResult[];
  onDone: () => void;
}) {
  const { t } = useTranslation('users');
  const { grant } = useGrantPlan();
  const form = useForm<GrantPlanValues>({
    resolver: zodResolver(grantPlanSchema),
    defaultValues: { plan: target.plan, period: prices[0]?.period ?? 'Monthly' },
  });

  return (
    <Form
      form={form}
      serverErrorFields={serverErrorFields}
      onSubmit={async (values) => {
        await grant(target.studentId, values);
        onDone();
      }}
    >
      <p className="text-ui text-text">{t('grant.body')}</p>
      <PeriodSelect prices={prices} />
      <FormRootError />
      <SubmitButton>{t('grant.confirm')}</SubmitButton>
    </Form>
  );
}

function GrantPlanContent({ target, onDone }: { target: GrantPlanTarget; onDone: () => void }) {
  const { t } = useTranslation('users');
  const catalogue = useGetPlanCatalogue();

  if (!catalogue.data) {
    return <p className="text-caption text-text-muted">{t('grant.loading')}</p>;
  }

  const prices = target.plan === 'Base' ? catalogue.data.base.prices : catalogue.data.askTeacher.prices;
  return <GrantPlanForm target={target} prices={prices} onDone={onDone} />;
}

export function GrantPlanDialog({ target, onOpenChange }: GrantPlanDialogProps) {
  const { t } = useTranslation('users');
  const planName = target ? t(target.plan === 'Base' ? 'plan.Base' : 'plan.askTeacher') : '';

  return (
    <Dialog open={target !== null} onOpenChange={onOpenChange}>
      <DialogContent title={t('grant.title', { plan: planName, name: target?.displayName ?? '' })}>
        {target ? (
          <GrantPlanContent
            target={target}
            onDone={() => {
              onOpenChange(false);
            }}
          />
        ) : null}
      </DialogContent>
    </Dialog>
  );
}
