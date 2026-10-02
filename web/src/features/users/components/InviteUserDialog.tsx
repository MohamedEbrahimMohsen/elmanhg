import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useRouter } from '@tanstack/react-router';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import type { ServerErrorFields } from '@/shared/form/applyServerErrors';
import { Form } from '@/shared/form/Form';
import { FormRootError } from '@/shared/form/FormRootError';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextField } from '@/shared/form/TextField';
import { Button } from '@/shared/ui/button';
import { Dialog, DialogContent } from '@/shared/ui/dialog';
import { useInviteUser } from '../hooks/useInviteUser';
import { inviteUserSchema, type InviteUserValues } from '../schemas/inviteUserSchema';
import { phoneNumberServerErrorFields } from '../schemas/teacherPhoneSchema';

export interface InviteUserDialogProps {
  role: 'Teacher' | 'Admin' | null;
  onOpenChange: (open: boolean) => void;
}

interface InviteDone {
  name: string;
  email: string;
  emailSent: boolean;
}

const serverErrorFields: ServerErrorFields<InviteUserValues> = {
  ...phoneNumberServerErrorFields,
  EMAIL_ALREADY_REGISTERED: 'email',
  EMAIL_INVALID: 'email',
  EMAIL_TOO_LONG: 'email',
  DISPLAY_NAME_REQUIRED: 'displayName',
  DISPLAY_NAME_TOO_LONG: 'displayName',
};

export function InviteUserDialog({ role, onOpenChange }: InviteUserDialogProps) {
  const { t } = useTranslation('users');
  const router = useRouter();
  const { invite } = useInviteUser();
  const [done, setDone] = useState<InviteDone | null>(null);
  const form = useForm<InviteUserValues>({
    resolver: zodResolver(inviteUserSchema),
    defaultValues: { displayName: '', email: '', phoneNumber: '' },
  });
  const link = new URL(router.buildLocation({ to: '/accept-invite' }).href, document.baseURI).href;

  const copy = async () => {
    await navigator.clipboard.writeText(link);
    toast(t('invite.copied'));
  };

  return (
    <Dialog open={role !== null} onOpenChange={onOpenChange}>
      <DialogContent
        title={t(done ? 'invite.doneTitle' : role === 'Admin' ? 'invite.titleAdmin' : 'invite.titleTeacher')}
      >
        {done ? (
          <>
            <p className="text-ui text-text">{t('invite.doneBody', { name: done.name, email: done.email })}</p>
            <p className="text-caption text-text-muted">
              {done.emailSent ? t('invite.emailSent', { email: done.email }) : t('invite.emailNotSent')}
            </p>
            <label className="flex flex-col gap-1.5 text-caption text-text-muted">
              {t('invite.link')}
              <input
                readOnly
                dir="ltr"
                value={link}
                className="h-11 w-full rounded-sm border border-border-strong bg-soft px-3 font-mono text-mono text-text"
              />
            </label>
            <div className="flex flex-wrap justify-end gap-3">
              <Button variant="secondary" onClick={() => void copy()}>
                {t('invite.copy')}
              </Button>
              <Button
                variant="primary"
                onClick={() => {
                  onOpenChange(false);
                }}
              >
                {t('invite.close')}
              </Button>
            </div>
          </>
        ) : (
          <Form
            form={form}
            serverErrorFields={serverErrorFields}
            onSubmit={async (values) => {
              const phoneNumber = role === 'Teacher' && values.phoneNumber !== '' ? values.phoneNumber : null;
              const result = await invite({ ...values, phoneNumber, role: role ?? 'Teacher' });
              setDone({ name: values.displayName, email: values.email, emailSent: result.emailSent });
            }}
          >
            <FormRootError />
            <TextField<InviteUserValues> name="displayName" label={t('invite.name')} autoComplete="off" />
            <TextField<InviteUserValues>
              name="email"
              label={t('invite.email')}
              type="email"
              autoComplete="off"
              dir="ltr"
            />
            {role === 'Teacher' ? (
              <TextField<InviteUserValues>
                name="phoneNumber"
                label={t('invite.phone')}
                description={t('invite.phoneHint')}
                type="tel"
                autoComplete="off"
                dir="ltr"
              />
            ) : null}
            <SubmitButton>{t('invite.submit')}</SubmitButton>
          </Form>
        )}
      </DialogContent>
    </Dialog>
  );
}
