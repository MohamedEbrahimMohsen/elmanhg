import { useEffect, useRef } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { getGetMyAvatarConversationsQueryKey } from '@/shared/api/generated/avatar/avatar';
import type { AvatarStatusResult } from '@/shared/api/generated/model';
import { Form } from '@/shared/form/Form';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextField } from '@/shared/form/TextField';
import { useAvatarChat } from '../hooks/useAvatarChat';
import { avatarMessageSchema, type AvatarMessageValues } from '../schemas/avatarMessageSchema';

export interface AssistantComposerProps {
  status: AvatarStatusResult;
}

export function AssistantComposer({ status }: AssistantComposerProps) {
  const { t } = useTranslation('avatar');
  const chat = useAvatarChat();
  const queryClient = useQueryClient();
  const sentRef = useRef(false);
  const blocked = status.examInProgress || Number(status.messagesRemainingToday) === 0 || chat.isPending;
  const form = useForm<AvatarMessageValues>({
    resolver: zodResolver(avatarMessageSchema(Number(status.messageMaxLength))),
    defaultValues: { message: '' },
    disabled: blocked,
  });

  const { isSubmitting } = form.formState;

  useEffect(() => {
    if (!blocked && !isSubmitting && sentRef.current) {
      sentRef.current = false;
      form.setFocus('message');
    }
  }, [blocked, isSubmitting, form]);

  return (
    <Form
      form={form}
      className="flex-row items-start gap-2"
      onSubmit={async (values) => {
        sentRef.current = true;
        await chat.send(values.message);
        form.reset();
        await queryClient.invalidateQueries({ queryKey: getGetMyAvatarConversationsQueryKey() });
      }}
    >
      <div className="min-w-0 flex-1">
        <TextField<AvatarMessageValues>
          name="message"
          label={t('composer.label')}
          placeholder={t('composer.placeholder')}
        />
      </div>
      <div className="mt-6.5 shrink-0">
        <SubmitButton disabled={blocked}>{t('composer.send')}</SubmitButton>
      </div>
    </Form>
  );
}
