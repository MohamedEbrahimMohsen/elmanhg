import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { useSendOtp, useVerifyOtp } from '@/shared/api/generated/auth/auth';
import type { GenerateOTPCommand, OtpChannel } from '@/shared/api/generated/model';
import type { ServerErrorFields } from '@/shared/form/applyServerErrors';
import { Form } from '@/shared/form/Form';
import { FormRootError } from '@/shared/form/FormRootError';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextField } from '@/shared/form/TextField';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { Button } from '@/shared/ui/button';
import { otpSchema, type OtpValues } from '../schemas/otpSchema';

export interface OtpDelivery {
  verificationId: string;
  channel: OtpChannel;
}

export interface OtpFormProps {
  recipient: string;
  request: GenerateOTPCommand;
  delivery: OtpDelivery;
  changeLabel: string;
  onVerified: (verificationId: string) => Promise<void>;
  onResent: (delivery: OtpDelivery) => void;
  onChange: () => void;
}

const serverErrorFields: ServerErrorFields<OtpValues> = {
  OTP_NOT_MATCHED: 'code',
  OTP_EXPIRED: 'code',
  OTP_INVALID_FORMAT: 'code',
};

const sentMessageKey: Record<OtpChannel, string> = {
  WhatsApp: 'otp.sentViaWhatsApp',
  Sms: 'otp.sentViaSms',
  Email: 'otp.sentToEmail',
};

export function OtpForm({ recipient, request, delivery, changeLabel, onVerified, onResent, onChange }: OtpFormProps) {
  const { t } = useTranslation('session');
  const [verified, setVerified] = useState(false);
  const verifyOtp = useVerifyOtp();
  const sendOtp = useSendOtp();
  const form = useForm<OtpValues>({ resolver: zodResolver(otpSchema), defaultValues: { code: '' } });

  const resend = async () => {
    try {
      const result = await sendOtp.mutateAsync({ data: request });
      setVerified(false);
      onResent({ verificationId: result.verificationId, channel: result.channel });
      toast(t('otp.resent'));
    } catch (error) {
      form.setError('root.server', {
        message: `errors.${error instanceof ApiError ? error.code : unhandledErrorCode}`,
      });
    }
  };

  return (
    <div className="flex flex-col gap-4">
      <p className="text-ui text-text">{t(sentMessageKey[delivery.channel], { recipient })}</p>
      <Form
        form={form}
        serverErrorFields={serverErrorFields}
        onSubmit={async ({ code }) => {
          if (!verified) {
            await verifyOtp.mutateAsync({ data: { verificationId: delivery.verificationId, code } });
            setVerified(true);
          }
          await onVerified(delivery.verificationId);
        }}
      >
        <FormRootError />
        <TextField<OtpValues> name="code" label={t('fields.code')} autoComplete="one-time-code" />
        <SubmitButton>{t('actions.verify')}</SubmitButton>
      </Form>
      <div className="flex flex-wrap gap-2">
        <Button
          type="button"
          variant="secondary"
          onClick={() => {
            void resend();
          }}
        >
          {t('actions.resendCode')}
        </Button>
        <Button type="button" variant="secondary" onClick={onChange}>
          {changeLabel}
        </Button>
      </div>
    </div>
  );
}
