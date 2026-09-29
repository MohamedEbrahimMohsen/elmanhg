import { useEffect } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import type { AvatarStatusResult } from '@/shared/api/generated/model';
import { Form } from '@/shared/form/Form';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextField } from '@/shared/form/TextField';
import { useAvatarChat } from '../hooks/useAvatarChat';
import { avatarMessageSchema, type AvatarMessageValues } from '../schemas/avatarMessageSchema';

export interface AvatarComposerProps {
  status: AvatarStatusResult;
}

export function AvatarComposer({ status }: AvatarComposerProps) {
  const { t } = useTranslation('avatar');
  const chat = useAvatarChat();
  const blocked = status.examInProgress || Number(status.messagesRemainingToday) === 0 || chat.isPending;
  const form = useForm<AvatarMessageValues>({
    resolver: zodResolver(avatarMessageSchema(Number(status.messageMaxLength))),
    defaultValues: { message: '' },
    disabled: blocked,
  });

  useEffect(() => {
    form.setFocus('message');
  }, [form]);

  return (
    <Form
      form={form}
      onSubmit={async (values) => {
        await chat.send(values.message, Number(status.maxHistoryMessages));
        form.reset();
      }}
    >
      <TextField<AvatarMessageValues>
        name="message"
        label={t('composer.label')}
        placeholder={t('composer.placeholder')}
      />
      <SubmitButton disabled={blocked}>{t('composer.send')}</SubmitButton>
    </Form>
  );
}
