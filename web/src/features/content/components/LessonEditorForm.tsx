import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import type { LessonDetailResult } from '@/shared/api/generated/model';
import { Form } from '@/shared/form/Form';
import { FormRootError } from '@/shared/form/FormRootError';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextField } from '@/shared/form/TextField';
import type { ServerErrorFields } from '@/shared/form/applyServerErrors';
import { toLessonValues } from '../api/lessonValues';
import { useLessonEditor } from '../hooks/useLessonEditor';
import { lessonSchema, type LessonValues } from '../schemas/lessonSchema';
import { LessonPreview } from './LessonPreview';
import { ObjectivesField } from './ObjectivesField';
import { RichTextField } from './RichTextField';

export interface LessonEditorFormProps {
  lesson: LessonDetailResult;
}

const lessonErrorFields: ServerErrorFields<LessonValues> = {
  LESSON_NAME_REQUIRED: 'name',
  LESSON_NAME_TOO_LONG: 'name',
  LESSON_VIDEO_URL_INVALID: 'videoUrl',
  LESSON_VIDEO_URL_TOO_LONG: 'videoUrl',
  LESSON_EXPLANATION_TOO_LONG: 'explanation',
  LESSON_SUMMARY_TOO_LONG: 'summary',
  LESSON_OBJECTIVES_TOO_MANY: 'objectives',
  LESSON_OBJECTIVE_TEXT_REQUIRED: 'objectives',
  LESSON_OBJECTIVE_TEXT_TOO_LONG: 'objectives',
  LESSON_OBJECTIVE_DUPLICATE: 'objectives',
};

export function LessonEditorForm({ lesson }: LessonEditorFormProps) {
  const { t } = useTranslation('content');
  const form = useForm<LessonValues>({ resolver: zodResolver(lessonSchema), defaultValues: toLessonValues(lesson) });
  const { save, uploadImage } = useLessonEditor(lesson.id);

  return (
    <Form
      form={form}
      onSubmit={save}
      serverErrorFields={lessonErrorFields}
      className="grid gap-4 lg:grid-cols-2 lg:items-start"
    >
      <div className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
        <FormRootError />
        <TextField<LessonValues> name="name" label={t('lessonEditor.fields.name')} />
        <TextField<LessonValues> name="videoUrl" label={t('lessonEditor.fields.videoUrl')} dir="ltr" />
        <RichTextField name="explanation" label={t('lessonEditor.fields.explanation')} onUploadImage={uploadImage} />
        <ObjectivesField />
        <RichTextField name="summary" label={t('lessonEditor.fields.summary')} onUploadImage={uploadImage} />
        <SubmitButton>{t('lessonEditor.save')}</SubmitButton>
      </div>
      <LessonPreview />
    </Form>
  );
}
