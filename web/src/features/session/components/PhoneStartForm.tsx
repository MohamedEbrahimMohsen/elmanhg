import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import type { z } from 'zod';
import { useSendOtp } from '@/shared/api/generated/auth/auth';
import type { ServerErrorFields } from '@/shared/form/applyServerErrors';
import { Form } from '@/shared/form/Form';
import { FormRootError } from '@/shared/form/FormRootError';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextField } from '@/shared/form/TextField';
import { phoneSignInSchema, phoneSignUpSchema } from '../schemas/phoneStartSchema';
import type { OtpDelivery } from './OtpForm';

export interface PhoneStart {
  phoneNumber: string;
  displayName: string;
  delivery: OtpDelivery;
}

export interface PhoneStartFormProps {
  withDisplayName: boolean;
  onCodeSent: (start: PhoneStart) => void;
}

interface PhoneStartValues {
  phoneNumber: string;
  displayName?: string;
}

const serverErrorFields: ServerErrorFields<PhoneStartValues> = {
  VALIDATION_PHONE_NUMBER_INVALID_CELLULAR_CODE: 'phoneNumber',
  VALIDATION_PHONE_NUMBER_MUST_BE_X_DIGITS: 'phoneNumber',
  VALIDATION_PHONE_NUMBER_MUST_BE_ONLY_DIGITS: 'phoneNumber',
};

export function PhoneStartForm({ withDisplayName, onCodeSent }: PhoneStartFormProps) {
  const { t } = useTranslation('session');
  const sendOtp = useSendOtp();
  const schema: z.ZodType<PhoneStartValues, PhoneStartValues> = withDisplayName ? phoneSignUpSchema : phoneSignInSchema;
  const form = useForm<PhoneStartValues>({
    resolver: zodResolver(schema),
    defaultValues: { phoneNumber: '', displayName: '' },
  });

  return (
    <Form
      form={form}
      serverErrorFields={serverErrorFields}
      onSubmit={async (values) => {
        const result = await sendOtp.mutateAsync({ data: { phoneNumber: values.phoneNumber } });
        onCodeSent({
          phoneNumber: values.phoneNumber,
          displayName: values.displayName ?? '',
          delivery: { verificationId: result.verificationId, channel: result.channel },
        });
      }}
    >
      <FormRootError />
      {withDisplayName ? (
        <TextField<PhoneStartValues> name="displayName" label={t('fields.displayName')} autoComplete="name" />
      ) : null}
      <TextField<PhoneStartValues>
        name="phoneNumber"
        label={t('fields.phoneNumber')}
        type="tel"
        autoComplete="tel"
        description={t('fields.phoneHint')}
      />
      <SubmitButton>{t('actions.sendCode')}</SubmitButton>
    </Form>
  );
}
