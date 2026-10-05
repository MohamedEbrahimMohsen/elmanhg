import { Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { TextAreaField } from '@/shared/form/TextAreaField';
import { TextField } from '@/shared/form/TextField';
import { Button } from '@/shared/ui/button';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { RubricLevelsField } from './RubricLevelsField';

export interface RubricCriterionCardProps {
  index: number;
  canRemove: boolean;
  onRemove: () => void;
}

export function RubricCriterionCard({ index, canRemove, onRemove }: RubricCriterionCardProps) {
  const { t } = useTranslation('questions');
  const number = index + 1;
  const prefix = `criteria.${index.toString()}` as `criteria.${number}`;

  return (
    <div className="flex flex-col gap-3 rounded-md border border-border p-3">
      <div className="flex items-center justify-between gap-2">
        <span className="text-ui font-bold text-text">{t('editor.essay.criterion', { number })}</span>
        <Button
          variant="ghost"
          size="sm"
          aria-label={t('editor.essay.removeCriterion', { number })}
          disabled={!canRemove}
          onClick={onRemove}
        >
          <Trash2 aria-hidden className="size-4" />
        </Button>
      </div>
      <TextField<QuestionValues> name={`${prefix}.title`} label={t('editor.essay.criterionTitle', { number })} />
      <TextAreaField<QuestionValues>
        name={`${prefix}.description`}
        label={t('editor.essay.criterionDescription', { number })}
      />
      <TextField<QuestionValues>
        name={`${prefix}.points`}
        label={t('editor.essay.criterionPoints', { number })}
        dir="ltr"
      />
      <RubricLevelsField criterionIndex={index} />
    </div>
  );
}
