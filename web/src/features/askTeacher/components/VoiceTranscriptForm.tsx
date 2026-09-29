import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Form } from '@/shared/form/Form';
import { FormRootError } from '@/shared/form/FormRootError';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextAreaField } from '@/shared/form/TextAreaField';
import { useSendVoiceReply } from '../hooks/useSendVoiceReply';
import { voiceTranscriptFormSchema, type VoiceTranscriptFormValues } from '../schemas/voiceTranscriptFormSchema';

export interface VoiceTranscriptFormProps {
  threadId: string;
  draftId: string;
  defaultText: string;
}

export function VoiceTranscriptForm({ threadId, draftId, defaultText }: VoiceTranscriptFormProps) {
  const { t } = useTranslation('askTeacher');
  const { submit } = useSendVoiceReply(threadId);
  const form = useForm<VoiceTranscriptFormValues>({
    resolver: zodResolver(voiceTranscriptFormSchema),
    defaultValues: { text: defaultText },
  });

  return (
    <Form
      form={form}
      onSubmit={async (values) => {
        await submit(draftId, values);
      }}
      serverErrorFields={{
        TEACHER_THREAD_REPLY_TEXT_REQUIRED: 'text',
        TEACHER_THREAD_REPLY_TEXT_TOO_LONG: 'text',
      }}
    >
      <TextAreaField<VoiceTranscriptFormValues>
        name="text"
        label={t('voice.transcriptLabel')}
        description={t('voice.transcriptHint')}
      />
      <FormRootError />
      <SubmitButton>{t('inboxThread.send')}</SubmitButton>
    </Form>
  );
}
