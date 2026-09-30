import { useTranslation } from 'react-i18next';
import { TextField } from '@/shared/form/TextField';
import { mathAnswerForms } from '../api/questionOptions';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { MathAnswersField } from './MathAnswersField';
import { MathSolutionField } from './MathSolutionField';
import { SelectField } from './SelectField';

export function MathStepsFields() {
  const { t } = useTranslation('questions');

  return (
    <div className="flex flex-col gap-3">
      <MathAnswersField />
      <SelectField<QuestionValues>
        name="mathForm"
        label={t('editor.math.form')}
        options={mathAnswerForms.map((value) => ({ value, label: t(`editor.math.forms.${value}`) }))}
      />
      <div className="grid gap-3 md:grid-cols-2">
        <TextField<QuestionValues>
          name="mathTolerance"
          label={t('editor.math.tolerance')}
          description={t('editor.math.toleranceHint')}
          dir="ltr"
        />
        <SelectField<QuestionValues>
          name="mathToleranceMode"
          label={t('editor.math.toleranceMode')}
          options={[
            { value: 'absolute', label: t('editor.short.absolute') },
            { value: 'percent', label: t('editor.short.percent') },
          ]}
        />
      </div>
      <TextField<QuestionValues>
        name="mathStepsWeight"
        label={t('editor.math.stepsWeight')}
        description={t('editor.math.stepsWeightHint')}
        dir="ltr"
      />
      <MathSolutionField />
      <p className="text-caption text-text-muted">{t('editor.math.stepsNote')}</p>
    </div>
  );
}
