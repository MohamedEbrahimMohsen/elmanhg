import { useId } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useController, useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Form } from '@/shared/form/Form';
import { FormRootError } from '@/shared/form/FormRootError';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextField } from '@/shared/form/TextField';
import { Button } from '@/shared/ui/button';
import { optimizeImage } from '../api/optimizeImage';
import { acceptedImageTypes, imageInsertSchema, type ImageInsertValues } from '../schemas/imageInsertSchema';

export interface ImageInsertFormProps {
  onUpload: (file: File) => Promise<string>;
  onInsert: (src: string, alt: string) => void;
  onCancel: () => void;
}

const imageErrorFields = {
  LESSON_IMAGE_REQUIRED: 'file',
  LESSON_IMAGE_TYPE_INVALID: 'file',
  LESSON_IMAGE_TOO_LARGE: 'file',
} as const;

export function ImageInsertForm({ onUpload, onInsert, onCancel }: ImageInsertFormProps) {
  const { t } = useTranslation('content');
  const form = useForm<ImageInsertValues>({
    resolver: zodResolver(imageInsertSchema),
    defaultValues: { description: '' },
  });
  const {
    field: { ref, onChange, onBlur },
    fieldState,
  } = useController<ImageInsertValues, 'file'>({ name: 'file', control: form.control });
  const id = useId();
  const errorId = `${id}-error`;

  return (
    <Form
      form={form}
      serverErrorFields={imageErrorFields}
      onSubmit={async (values) => {
        const url = await onUpload(await optimizeImage(values.file));
        onInsert(url, values.description);
      }}
    >
      <FormRootError />
      <div className="flex flex-col gap-1.5">
        <label htmlFor={id} className="text-caption text-text-muted">
          {t('lessonEditor.image.file')}
        </label>
        <input
          id={id}
          type="file"
          accept={acceptedImageTypes}
          aria-invalid={fieldState.invalid}
          aria-describedby={fieldState.error ? errorId : undefined}
          onChange={(event) => {
            onChange(event.target.files?.[0]);
          }}
          onBlur={onBlur}
          ref={ref}
          className="text-ui"
        />
        {fieldState.error ? (
          <p id={errorId} className="text-caption text-danger">
            {t([fieldState.error.message ?? '', 'errors.UNHANDLED_EXCEPTION'], { ns: 'common' })}
          </p>
        ) : null}
      </div>
      <TextField<ImageInsertValues> name="description" label={t('lessonEditor.image.description')} />
      <div className="flex flex-wrap gap-2">
        <SubmitButton>{t('lessonEditor.image.insert')}</SubmitButton>
        <Button variant="secondary" onClick={onCancel}>
          {t('actions.cancel')}
        </Button>
      </div>
    </Form>
  );
}
