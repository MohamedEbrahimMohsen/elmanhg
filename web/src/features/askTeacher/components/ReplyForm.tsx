import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Form } from '@/shared/form/Form';
import { FormRootError } from '@/shared/form/FormRootError';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextAreaField } from '@/shared/form/TextAreaField';
import { useReplyToThread } from '../hooks/useReplyToThread';
import { replyFormSchema, type ReplyFormValues } from '../schemas/replyFormSchema';

export interface ReplyFormProps {
  threadId: string;
}

export function ReplyForm({ threadId }: ReplyFormProps) {
  const { t } = useTranslation('askTeacher');
  const { submit } = useReplyToThread(threadId);
  const form = useForm<ReplyFormValues>({
    resolver: zodResolver(replyFormSchema),
    defaultValues: { text: '' },
  });

  return (
    <Form
      form={form}
      onSubmit={async (values) => {
        await submit(values);
      }}
      serverErrorFields={{
        TEACHER_THREAD_REPLY_TEXT_REQUIRED: 'text',
        TEACHER_THREAD_REPLY_TEXT_TOO_LONG: 'text',
      }}
    >
      <TextAreaField<ReplyFormValues>
        name="text"
        label={t('inboxThread.replyLabel')}
        description={t('inboxThread.replyHint')}
      />
      <FormRootError />
      <SubmitButton>{t('inboxThread.send')}</SubmitButton>
    </Form>
  );
}
