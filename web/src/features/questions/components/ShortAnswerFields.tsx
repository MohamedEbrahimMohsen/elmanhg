import { useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { TextField } from '@/shared/form/TextField';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { CheckboxField } from './CheckboxField';
import { SelectField } from './SelectField';
import { TextAreaField } from './TextAreaField';

export function ShortAnswerFields() {
  const { t } = useTranslation('questions');
  const answerKind = useWatch<QuestionValues, 'answerKind'>({ name: 'answerKind' });

  return (
    <div className="flex flex-col gap-3">
      <SelectField<QuestionValues>
        name="answerKind"
        label={t('editor.short.answerKind')}
        options={[
          { value: 'numeric', label: t('editor.short.numeric') },
          { value: 'text', label: t('editor.short.text') },
        ]}
      />
      {answerKind === 'numeric' ? (
        <div className="grid gap-3 md:grid-cols-3">
          <TextField<QuestionValues> name="numericValue" label={t('editor.short.value')} dir="ltr" />
          <TextField<QuestionValues> name="tolerance" label={t('editor.short.tolerance')} dir="ltr" />
          <SelectField<QuestionValues>
            name="toleranceMode"
            label={t('editor.short.toleranceMode')}
            options={[
              { value: 'absolute', label: t('editor.short.absolute') },
              { value: 'percent', label: t('editor.short.percent') },
            ]}
          />
        </div>
      ) : (
        <>
          <TextAreaField<QuestionValues> name="acceptedAnswers" label={t('editor.short.accepted')} />
          <CheckboxField<QuestionValues> name="unifyLetterVariants" label={t('editor.blanks.unify')} />
        </>
      )}
    </div>
  );
}
