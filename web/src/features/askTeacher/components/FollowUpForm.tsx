import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Form } from '@/shared/form/Form';
import { FormRootError } from '@/shared/form/FormRootError';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextAreaField } from '@/shared/form/TextAreaField';
import { useFollowUpThread } from '../hooks/useFollowUpThread';
import { followUpFormSchema, type FollowUpFormValues } from '../schemas/followUpFormSchema';

export interface FollowUpFormProps {
  threadId: string;
}

export function FollowUpForm({ threadId }: FollowUpFormProps) {
  const { t } = useTranslation('askTeacher');
  const { submit } = useFollowUpThread(threadId);
  const form = useForm<FollowUpFormValues>({
    resolver: zodResolver(followUpFormSchema),
    defaultValues: { text: '' },
  });

  return (
    <div className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-4 shadow-1">
      <h2 className="font-display text-h2 font-bold lg:text-h2-desktop">{t('followUp.title')}</h2>
      <Form
        form={form}
        onSubmit={async (values) => {
          await submit(values);
        }}
        serverErrorFields={{
          TEACHER_THREAD_TEXT_REQUIRED: 'text',
          TEACHER_THREAD_TEXT_TOO_LONG: 'text',
        }}
      >
        <TextAreaField<FollowUpFormValues> name="text" label={t('followUp.label')} description={t('followUp.hint')} />
        <FormRootError />
        <SubmitButton>{t('followUp.send')}</SubmitButton>
      </Form>
    </div>
  );
}
