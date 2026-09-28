import { useId } from 'react';
import { useController } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import type { LessonValues } from '../schemas/lessonSchema';
import { RichTextEditor } from './RichTextEditor';

export interface RichTextFieldProps {
  name: 'explanation' | 'summary';
  label: string;
  onUploadImage: (file: File) => Promise<string>;
}

export function RichTextField({ name, label, onUploadImage }: RichTextFieldProps) {
  const { t } = useTranslation('content');
  const { field, fieldState } = useController<LessonValues, 'explanation' | 'summary'>({ name });
  const id = useId();
  const labelId = `${id}-label`;
  const errorId = `${id}-error`;

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
        describedBy={fieldState.error ? errorId : undefined}
        invalid={fieldState.invalid}
        fieldLabel={label}
        onUploadImage={onUploadImage}
      />
      {fieldState.error ? (
        <p id={errorId} className="text-caption text-danger">
          {t([fieldState.error.message ?? '', 'errors.UNHANDLED_EXCEPTION'], { ns: 'common' })}
        </p>
      ) : null}
    </div>
  );
}
