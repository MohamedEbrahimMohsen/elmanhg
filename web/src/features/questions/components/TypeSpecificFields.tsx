import { useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { ChoiceOptionsField } from './ChoiceOptionsField';
import { DragDropFields } from './DragDropFields';
import { EssayFields } from './EssayFields';
import { FillBlanksField } from './FillBlanksField';
import { SelectField } from './SelectField';
import { ShortAnswerFields } from './ShortAnswerFields';

export interface TypeSpecificFieldsProps {
  lessonId: string;
}

export function TypeSpecificFields({ lessonId }: TypeSpecificFieldsProps) {
  const { t } = useTranslation('questions');
  const type = useWatch<QuestionValues, 'type'>({ name: 'type' });

  switch (type) {
    case 'Mcq':
    case 'Multi':
      return <ChoiceOptionsField />;
    case 'TrueFalse':
      return (
        <SelectField<QuestionValues>
          name="trueFalseAnswer"
          label={t('editor.trueFalse.answer')}
          placeholder={t('editor.trueFalse.choose')}
          options={[
            { value: 'true', label: t('editor.trueFalse.true') },
            { value: 'false', label: t('editor.trueFalse.false') },
          ]}
        />
      );
    case 'Fill':
      return <FillBlanksField />;
    case 'Short':
      return <ShortAnswerFields />;
    case 'Essay':
      return <EssayFields />;
    case 'DragDrop':
      return <DragDropFields lessonId={lessonId} />;
  }
}
