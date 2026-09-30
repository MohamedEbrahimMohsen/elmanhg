import { zodResolver } from '@hookform/resolvers/zod';
import { useForm, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import type { LessonDetailResult, QuestionDetailResult } from '@/shared/api/generated/model';
import { Form } from '@/shared/form/Form';
import { FormRootError } from '@/shared/form/FormRootError';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { questionErrorFields } from '../api/questionErrorFields';
import { questionTypes } from '../api/questionOptions';
import { emptyQuestionValues, toQuestionValues } from '../api/questionValues';
import { useQuestionImageUpload } from '../hooks/useQuestionImageUpload';
import { useQuestionSave } from '../hooks/useQuestionSave';
import { questionEditorSchema, type QuestionValues } from '../schemas/questionEditorSchema';
import { QuestionMetadataFields } from './QuestionMetadataFields';
import { QuestionPreviewPanel } from './QuestionPreviewPanel';
import { QuestionRichTextField } from './QuestionRichTextField';
import { SelectField } from './SelectField';
import { TypeSpecificFields } from './TypeSpecificFields';

export interface QuestionEditorFormProps {
  lesson: LessonDetailResult;
  question?: QuestionDetailResult | undefined;
}

export function QuestionEditorForm({ lesson, question }: QuestionEditorFormProps) {
  const { t } = useTranslation('questions');
  const form = useForm<QuestionValues>({
    resolver: zodResolver(questionEditorSchema),
    defaultValues: question ? toQuestionValues(question) : emptyQuestionValues('Mcq'),
  });
  const type = useWatch({ control: form.control, name: 'type' });
  const save = useQuestionSave(lesson.id, question);
  const upload = useQuestionImageUpload(lesson.id);
  const locked = question !== undefined;
  const submitLabel = !question
    ? t('editor.create')
    : question.validationStatus === 'Rejected'
      ? t('editor.resubmit')
      : t('editor.save');

  return (
    <Form
      form={form}
      onSubmit={save}
      serverErrorFields={questionErrorFields}
      className="grid gap-4 lg:grid-cols-2 lg:items-start"
    >
      <div className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
        <FormRootError />
        <SelectField<QuestionValues>
          name="type"
          label={t('editor.fields.type')}
          options={questionTypes.map((value) => ({
            value,
            label:
              value === 'Essay' || value === 'MathSteps' || value === 'DragDrop'
                ? t('editor.fields.typeV2', { type: t(`types.${value}`) })
                : t(`types.${value}`),
          }))}
          disabled={locked}
          description={locked ? t('editor.fields.typeLocked') : undefined}
        />
        <QuestionRichTextField
          name="stem"
          label={t('editor.fields.stem')}
          description={type === 'Fill' ? t('editor.fields.stemFillHint') : undefined}
          onUploadImage={upload}
        />
        <TypeSpecificFields lessonId={lesson.id} />
        <QuestionMetadataFields lesson={lesson} />
        <QuestionRichTextField name="explanation" label={t('editor.fields.explanation')} onUploadImage={upload} />
        <SubmitButton>{submitLabel}</SubmitButton>
      </div>
      <QuestionPreviewPanel lessonId={lesson.id} />
    </Form>
  );
}
