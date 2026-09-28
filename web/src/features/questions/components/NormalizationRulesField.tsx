import { useTranslation } from 'react-i18next';
import { normalizationRules } from '../api/questionOptions';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { CheckboxField } from './CheckboxField';

export function NormalizationRulesField() {
  const { t } = useTranslation('questions');

  return (
    <fieldset className="flex flex-col gap-1">
      <legend className="mb-2 text-caption text-text-muted">{t('editor.normalization.legend')}</legend>
      <p className="text-caption text-text-muted">{t('editor.normalization.hint')}</p>
      <div className="grid md:grid-cols-2 md:gap-x-4">
        {normalizationRules.map((rule) => (
          <CheckboxField<QuestionValues>
            key={rule}
            name={`normalization.${rule}`}
            label={t(`editor.normalization.${rule}`)}
          />
        ))}
      </div>
    </fieldset>
  );
}
