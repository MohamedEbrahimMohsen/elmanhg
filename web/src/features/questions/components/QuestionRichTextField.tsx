import { useId } from 'react';
import { useController } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { RichTextEditor } from '@/features/content';
import type { QuestionValues } from '../schemas/questionEditorSchema';

export interface QuestionRichTextFieldProps {
  name: 'stem' | 'explanation' | `options.${number}.text`;
  label: string;
  description?: string | undefined;
  compact?: boolean;
  onUploadImage?: ((file: File) => Promise<string>) | undefined;
}

export function QuestionRichTextField({
  name,
  label,
  description,
  compact,
  onUploadImage,
}: QuestionRichTextFieldProps) {
  const { t } = useTranslation();
  const { field, fieldState } = useController<QuestionValues, QuestionRichTextFieldProps['name']>({ name });
  const id = useId();
  const labelId = `${id}-label`;
  const descriptionId = `${id}-description`;
  const errorId = `${id}-error`;
  const describedByIds = [description ? descriptionId : null, fieldState.error ? errorId : null].filter(
    (value) => value !== null,
  );

  return (
    <div className="flex flex-col gap-1.5">
      <span id={labelId} className="text-caption text-text-muted">
        {label}
      </span>
      <RichTextEditor
        value={field.value}
        onChange={field.onChange}
        onBlur={field.onBlur}
        labelledBy={labelId}
        describedBy={describedByIds.length > 0 ? describedByIds.join(' ') : undefined}
        invalid={fieldState.invalid}
        fieldLabel={label}
        onUploadImage={onUploadImage}
        compact={compact}
      />
      {description ? (
        <p id={descriptionId} className="text-caption text-text-muted">
          {description}
        </p>
      ) : null}
      {fieldState.error ? (
        <p id={errorId} className="text-caption text-danger">
          {t([fieldState.error.message ?? '', 'errors.UNHANDLED_EXCEPTION'])}
        </p>
      ) : null}
    </div>
  );
}
