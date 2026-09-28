import { Trash2 } from 'lucide-react';
import { useFormContext, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { QuestionRichTextField } from './QuestionRichTextField';

export interface ChoiceOptionRowProps {
  index: number;
  multiple: boolean;
  groupName: string;
  canRemove: boolean;
  onRemove: () => void;
}

export function ChoiceOptionRow({ index, multiple, groupName, canRemove, onRemove }: ChoiceOptionRowProps) {
  const { t } = useTranslation('questions');
  const { register, setValue, getValues } = useFormContext<QuestionValues>();
  const correct = useWatch<QuestionValues, `options.${number}.correct`>({
    name: `options.${index.toString()}.correct` as `options.${number}.correct`,
  });
  const position = index + 1;
  const correctName = `options.${index.toString()}.correct` as `options.${number}.correct`;

  return (
    <div className="flex items-start gap-2.5">
      {multiple ? (
        <input
          type="checkbox"
          aria-label={t('editor.options.correct', { number: position })}
          className="mt-3 size-4.5 accent-text"
          {...register(correctName)}
        />
      ) : (
        <input
          type="radio"
          name={groupName}
          aria-label={t('editor.options.correct', { number: position })}
          checked={correct}
          onChange={() => {
            getValues('options').forEach((_, other) => {
              setValue(`options.${other.toString()}.correct` as `options.${number}.correct`, other === index, {
                shouldDirty: true,
              });
            });
          }}
          className="mt-3 size-4.5 accent-text"
        />
      )}
      <div className="min-w-0 flex-1">
        <QuestionRichTextField
          name={`options.${index.toString()}.text` as `options.${number}.text`}
          label={t('editor.options.option', { number: position })}
          compact
        />
      </div>
      <Button
        variant="ghost"
        size="sm"
        aria-label={t('editor.options.remove', { number: position })}
        disabled={!canRemove}
        onClick={onRemove}
      >
        <Trash2 aria-hidden className="size-4" />
      </Button>
    </div>
  );
}
