import { useId, useState, type ChangeEvent } from 'react';
import { useFormContext, useFormState, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { optimizeImage } from '@/features/content';
import { TextField } from '@/shared/form/TextField';
import { Label } from '@/shared/ui/label';
import { readImageSize } from '../api/imageSize';
import { acceptedDiagramImageTypes } from '../api/questionOptions';
import { useDiagramImageUpload } from '../hooks/useDiagramImageUpload';
import type { QuestionValues } from '../schemas/questionEditorSchema';

export interface DiagramImageFieldProps {
  lessonId: string;
}

const fileClassName =
  'text-ui text-text file:me-3 file:min-h-9 file:rounded-sm file:border file:border-border-strong file:bg-surface file:px-3 file:text-ui file:text-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden';

export function DiagramImageField({ lessonId }: DiagramImageFieldProps) {
  const { t } = useTranslation('questionsDiagram');
  const id = useId();
  const { getValues, setValue, clearErrors } = useFormContext<QuestionValues>();
  const { errors } = useFormState<QuestionValues>({ name: 'diagramImage' });
  const { upload, isPending, errorCode } = useDiagramImageUpload(lessonId);
  const [readError, setReadError] = useState(false);
  const image = useWatch<QuestionValues, 'diagramImage'>({ name: 'diagramImage' });
  const message = errors.diagramImage?.message ?? errors.diagramImage?.root?.message;

  const onChange = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }
    setReadError(false);
    const optimized = await optimizeImage(file);
    const size = await readImageSize(optimized).catch(() => null);
    if (!size) {
      setReadError(true);
      return;
    }
    const uploaded = await upload(optimized);
    if (uploaded) {
      setValue(
        'diagramImage',
        { ...getValues('diagramImage'), key: uploaded.key, url: uploaded.url, ...size },
        { shouldDirty: true },
      );
      clearErrors('diagramImage');
    }
  };

  return (
    <fieldset className="flex flex-col gap-3">
      <legend className="mb-2 text-caption text-text-muted">{t('editor.dragDrop.imageLegend')}</legend>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor={id}>{t('editor.dragDrop.imageFile')}</Label>
        <input
          id={id}
          type="file"
          accept={acceptedDiagramImageTypes}
          onChange={(event) => void onChange(event)}
          className={fileClassName}
        />
      </div>
      {isPending ? <p role="status">{t('editor.dragDrop.imageUploading')}</p> : null}
      {image.key !== '' ? (
        <p className="text-caption text-text-muted">
          {t('editor.dragDrop.imageCurrent', { width: image.width, height: image.height })}
        </p>
      ) : null}
      {readError ? <p role="alert">{t('editor.dragDrop.imageUnreadable')}</p> : null}
      {errorCode ? (
        <p role="alert" className="text-caption text-danger">
          {t([`common:errors.${errorCode}`, 'common:errors.UNHANDLED_EXCEPTION'])}
        </p>
      ) : null}
      {message ? (
        <p className="text-caption text-danger">{t([message, 'errors.UNHANDLED_EXCEPTION'], { ns: 'common' })}</p>
      ) : null}
      <TextField<QuestionValues> name="diagramImage.alt" label={t('editor.dragDrop.imageAlt')} />
    </fieldset>
  );
}
