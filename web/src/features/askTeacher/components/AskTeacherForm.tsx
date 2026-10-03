import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import type { TeacherReplyDeadlineResult } from '@/shared/api/generated/model';
import { Form } from '@/shared/form/Form';
import { FormRootError } from '@/shared/form/FormRootError';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextAreaField } from '@/shared/form/TextAreaField';
import { formatDateTime } from '@/shared/lib/dateTime';
import { formatNumber } from '@/shared/lib/format';
import { useCreateThread } from '../hooks/useCreateThread';
import { askTeacherFormSchema, type AskTeacherFormValues } from '../schemas/askTeacherFormSchema';
import type { AskTeacherNewSearch } from '../schemas/askTeacherNewSearchSchema';
import { AttachedContext } from './AttachedContext';
import { ImageField } from './ImageField';
import { LessonPicker } from './LessonPicker';

export interface AskTeacherFormProps {
  search: AskTeacherNewSearch;
  replyDeadline: TeacherReplyDeadlineResult | undefined;
}

export function AskTeacherForm({ search, replyDeadline }: AskTeacherFormProps) {
  const { t, i18n } = useTranslation('askTeacher');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const hasContext = Boolean(search.lessonId ?? search.questionId ?? search.attemptId);
  const { submit } = useCreateThread(search);
  const form = useForm<AskTeacherFormValues>({
    resolver: zodResolver(askTeacherFormSchema(!hasContext)),
    defaultValues: { text: '', lessonId: '', image: null },
  });

  return (
    <Form
      form={form}
      onSubmit={async (values) => {
        await submit(values);
      }}
      serverErrorFields={{
        TEACHER_THREAD_TEXT_REQUIRED: 'text',
        TEACHER_THREAD_TEXT_TOO_LONG: 'text',
        TEACHER_THREAD_IMAGE_TYPE_INVALID: 'image',
        TEACHER_THREAD_IMAGE_TOO_LARGE: 'image',
      }}
    >
      {hasContext ? <AttachedContext search={search} /> : <LessonPicker />}
      <TextAreaField<AskTeacherFormValues> name="text" label={t('form.text')} description={t('form.textHint')} />
      <ImageField />
      {replyDeadline === undefined ? null : (
        <p className="text-caption text-text-muted">
          {t('form.slaNote', {
            hours: formatNumber(Number(replyDeadline.replySlaHours), lng),
            date: formatDateTime(replyDeadline.slaDueAt, lng, 'fullDateTime'),
          })}
        </p>
      )}
      {replyDeadline?.skipsUncountedDays ? (
        <p className="text-caption text-text-muted">{t('form.slaCalendarNote')}</p>
      ) : null}
      <FormRootError />
      <SubmitButton>{t('form.send')}</SubmitButton>
    </Form>
  );
}
