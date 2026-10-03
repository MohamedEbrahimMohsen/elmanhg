import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import type { AdminPaymentResult } from '@/shared/api/generated/model';
import { Form } from '@/shared/form/Form';
import { FormRootError } from '@/shared/form/FormRootError';
import { TextAreaField } from '@/shared/form/TextAreaField';
import { formatMoney } from '@/shared/lib/money';
import { Button } from '@/shared/ui/button';
import { Dialog, DialogContent } from '@/shared/ui/dialog';
import { useRefundPayment } from '../hooks/useRefundPayment';
import { refundPaymentSchema, type RefundPaymentValues } from '../schemas/refundPaymentSchema';

export interface RefundPaymentDialogProps {
  payment: AdminPaymentResult | null;
  onOpenChange: (open: boolean) => void;
}

const serverErrorFields = {
  PAYMENT_REFUND_REASON_REQUIRED: 'reason',
  PAYMENT_REFUND_REASON_TOO_LONG: 'reason',
} as const;

export function RefundPaymentDialog({ payment, onOpenChange }: RefundPaymentDialogProps) {
  const { t, i18n } = useTranslation('payments');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const [idempotencyKey] = useState(() => crypto.randomUUID());
  const { refund, isPending } = useRefundPayment();
  const form = useForm<RefundPaymentValues>({
    resolver: zodResolver(refundPaymentSchema),
    defaultValues: { reason: '' },
  });
  const amount = payment ? formatMoney(Number(payment.amount.amountMinor), payment.amount.currency, lng) : '';

  const submit = async (values: RefundPaymentValues) => {
    if (!payment) {
      return;
    }
    await refund(payment.id, values.reason, idempotencyKey);
    onOpenChange(false);
  };

  return (
    <Dialog open={payment !== null} onOpenChange={onOpenChange}>
      <DialogContent title={t('refund.title', { amount })}>
        <p className="text-ui text-text">{t('refund.body', { name: payment?.studentName ?? '' })}</p>
        <Form form={form} onSubmit={submit} serverErrorFields={serverErrorFields}>
          <TextAreaField<RefundPaymentValues> name="reason" label={t('refund.reason')} />
          <FormRootError />
          <div className="flex flex-wrap justify-end gap-3">
            <Button
              variant="secondary"
              onClick={() => {
                onOpenChange(false);
              }}
            >
              {t('refund.cancel')}
            </Button>
            <Button type="submit" variant="danger" disabled={isPending} aria-busy={isPending}>
              {t('refund.confirm')}
            </Button>
          </div>
        </Form>
      </DialogContent>
    </Dialog>
  );
}
