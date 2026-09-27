import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { useSendOtp, useVerifyOtp } from '@/shared/api/generated/auth/auth';
import type { ServerErrorFields } from '@/shared/form/applyServerErrors';
import { Form } from '@/shared/form/Form';
import { FormRootError } from '@/shared/form/FormRootError';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextField } from '@/shared/form/TextField';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { Button } from '@/shared/ui/button';
import { otpSchema, type OtpValues } from '../schemas/otpSchema';
import type { PhoneStart } from './PhoneStartForm';

export interface OtpFormProps {
  start: PhoneStart;
  onVerified: (verificationId: string) => Promise<void>;
  onResent: (verificationId: string) => void;
  onChangeNumber: () => void;
}

const serverErrorFields: ServerErrorFields<OtpValues> = {
  OTP_NOT_MATCHED: 'code',
  OTP_EXPIRED: 'code',
  OTP_INVALID_FORMAT: 'code',
};

export function OtpForm({ start, onVerified, onResent, onChangeNumber }: OtpFormProps) {
  const { t } = useTranslation('session');
  const [verified, setVerified] = useState(false);
  const verifyOtp = useVerifyOtp();
  const sendOtp = useSendOtp();
  const form = useForm<OtpValues>({ resolver: zodResolver(otpSchema), defaultValues: { code: '' } });

  const resend = async () => {
    try {
      const result = await sendOtp.mutateAsync({ data: { phoneNumber: start.phoneNumber } });
      setVerified(false);
      onResent(result.verificationId);
      toast(t('otp.resent'));
    } catch (error) {
      form.setError('root.server', {
        message: `errors.${error instanceof ApiError ? error.code : unhandledErrorCode}`,
      });
    }
  };

  return (
    <div className="flex flex-col gap-4">
      <p className="text-ui text-text">{t('otp.sentTo', { phoneNumber: start.phoneNumber })}</p>
      <Form
        form={form}
        serverErrorFields={serverErrorFields}
        onSubmit={async ({ code }) => {
          if (!verified) {
            await verifyOtp.mutateAsync({ data: { verificationId: start.verificationId, code } });
            setVerified(true);
          }
          await onVerified(start.verificationId);
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
        <Button type="button" variant="secondary" onClick={onChangeNumber}>
          {t('actions.changeNumber')}
        </Button>
      </div>
    </div>
  );
}
