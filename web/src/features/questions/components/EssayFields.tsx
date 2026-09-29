import { useTranslation } from 'react-i18next';
import { TextField } from '@/shared/form/TextField';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { ModelAnswersField } from './ModelAnswersField';
import { RubricCriteriaField } from './RubricCriteriaField';

export function EssayFields() {
  const { t } = useTranslation('questions');

  return (
    <div className="flex flex-col gap-3">
      <TextField<QuestionValues>
        name="maxWords"
        label={t('editor.essay.maxWords')}
        description={t('editor.essay.maxWordsHint')}
        dir="ltr"
      />
      <RubricCriteriaField />
      <ModelAnswersField />
    </div>
  );
}
