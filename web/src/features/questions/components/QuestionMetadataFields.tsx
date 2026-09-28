import { useTranslation } from 'react-i18next';
import type { LessonDetailResult } from '@/shared/api/generated/model';
import { TextField } from '@/shared/form/TextField';
import { questionDifficulties } from '../api/questionOptions';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { SelectField } from './SelectField';

export interface QuestionMetadataFieldsProps {
  lesson: LessonDetailResult;
}

export function QuestionMetadataFields({ lesson }: QuestionMetadataFieldsProps) {
  const { t } = useTranslation('questions');
  const objectives = [...lesson.objectives]
    .sort((a, b) => Number(a.order) - Number(b.order))
    .map((objective) => ({ value: objective.id, label: objective.text }));

  return (
    <div className="grid gap-3 md:grid-cols-2">
      <SelectField<QuestionValues>
        name="difficulty"
        label={t('editor.fields.difficulty')}
        options={questionDifficulties.map((difficulty) => ({
          value: difficulty,
          label: t(`difficulties.${difficulty}`),
        }))}
      />
      <SelectField<QuestionValues>
        name="objectiveId"
        label={t('editor.fields.objective')}
        placeholder={t('editor.fields.noObjective')}
        options={objectives}
      />
      <TextField<QuestionValues> name="maxScore" label={t('editor.fields.maxScore')} dir="ltr" />
      <TextField<QuestionValues>
        name="tags"
        label={t('editor.fields.tags')}
        description={t('editor.fields.tagsHint')}
      />
    </div>
  );
}
