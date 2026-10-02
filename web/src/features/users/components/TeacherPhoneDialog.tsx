import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import type { UserSummaryResult } from '@/shared/api/generated/model';
import { applyServerErrors, type ServerErrorFields } from '@/shared/form/applyServerErrors';
import { Form } from '@/shared/form/Form';
import { FormRootError } from '@/shared/form/FormRootError';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextField } from '@/shared/form/TextField';
import { Button } from '@/shared/ui/button';
import { Dialog, DialogContent } from '@/shared/ui/dialog';
import { useTeacherPhoneNumber } from '../hooks/useTeacherPhoneNumber';
import {
  phoneNumberServerErrorFields,
  teacherPhoneSchema,
  type TeacherPhoneValues,
} from '../schemas/teacherPhoneSchema';

export interface TeacherPhoneDialogProps {
  target: Pick<UserSummaryResult, 'id' | 'displayName' | 'maskedPhone'> | null;
  onOpenChange: (open: boolean) => void;
}

const serverErrorFields: ServerErrorFields<TeacherPhoneValues> = phoneNumberServerErrorFields;
// Keeps the masked number left-to-right inside the Arabic sentence.
const leftToRightIsolate = '\u2066';
const popDirectionalIsolate = '\u2069';

function TeacherPhoneForm({
  target,
  onDone,
}: {
  target: NonNullable<TeacherPhoneDialogProps['target']>;
  onDone: () => void;
}) {
  const { t } = useTranslation('users');
  const { save, isPending } = useTeacherPhoneNumber();
  const form = useForm<TeacherPhoneValues>({
    resolver: zodResolver(teacherPhoneSchema),
    defaultValues: { phoneNumber: '' },
  });
  const remove = () => {
    save(target.id, null).then(onDone, (error: unknown) => {
      applyServerErrors(form, error, serverErrorFields);
    });
  };

  return (
    <Form
      form={form}
      serverErrorFields={serverErrorFields}
      onSubmit={async (values) => {
        await save(target.id, values.phoneNumber);
        onDone();
      }}
    >
      <p className="text-ui text-text">
        {target.maskedPhone
          ? t('teacherPhone.current', { phone: `${leftToRightIsolate}${target.maskedPhone}${popDirectionalIsolate}` })
          : t('teacherPhone.none')}
      </p>
      <FormRootError />
      <TextField<TeacherPhoneValues>
        name="phoneNumber"
        label={t('teacherPhone.label')}
        type="tel"
        autoComplete="off"
        dir="ltr"
      />
      <div className="flex flex-wrap justify-end gap-3">
        {target.maskedPhone ? (
          <Button variant="secondary" disabled={isPending} onClick={remove}>
            {t('teacherPhone.remove')}
          </Button>
        ) : null}
        <SubmitButton>{t('teacherPhone.save')}</SubmitButton>
      </div>
    </Form>
  );
}

export function TeacherPhoneDialog({ target, onOpenChange }: TeacherPhoneDialogProps) {
  const { t } = useTranslation('users');

  return (
    <Dialog open={target !== null} onOpenChange={onOpenChange}>
      <DialogContent title={t('teacherPhone.title', { name: target?.displayName ?? '' })}>
        {target ? (
          <TeacherPhoneForm
            key={target.id}
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
