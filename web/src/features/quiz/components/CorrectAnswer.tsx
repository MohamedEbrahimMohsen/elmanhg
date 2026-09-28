import { useTranslation } from 'react-i18next';
import { RichTextViewer } from '@/features/content';
import { formatNumber } from '@/shared/lib/format';
import type { CorrectAnswerView } from '../api/correctAnswer';

export interface CorrectAnswerProps {
  view: CorrectAnswerView;
}

export function CorrectAnswer({ view }: CorrectAnswerProps) {
  const { t, i18n } = useTranslation('quiz');
  const lng = i18n.resolvedLanguage ?? i18n.language;

  switch (view.kind) {
    case 'options':
      return (
        <ul className="flex flex-col gap-1 text-ui text-text">
          {view.options.map((option) => (
            <li key={option.id}>
              <RichTextViewer html={option.text} />
            </li>
          ))}
        </ul>
      );
    case 'trueFalse':
      return <p className="text-ui text-text">{t(view.value ? 'feedback.true' : 'feedback.false')}</p>;
    case 'blanks':
      return (
        <ul className="flex flex-col gap-1 text-ui text-text">
          {view.answers.map((answer, index) => (
            <li key={answer.id}>
              {t('feedback.blank', { number: index + 1 })} <bdi>{answer.text}</bdi>
            </li>
          ))}
        </ul>
      );
    case 'numeric': {
      const value = formatNumber(view.value, lng, 'arabic-indic', { maximumFractionDigits: 6 });
      const tolerance = formatNumber(view.tolerance, lng, 'arabic-indic', { maximumFractionDigits: 6 });
      const text =
        view.tolerance > 0
          ? t(view.toleranceMode === 'percent' ? 'feedback.tolerancePercent' : 'feedback.tolerance', {
              value,
              tolerance,
            })
          : value;
      return (
        <p className="text-ui text-text">
          <bdi>{text}</bdi>
        </p>
      );
    }
    case 'text':
      return (
        <p className="text-ui text-text">
          <bdi>{view.text}</bdi>
        </p>
      );
  }
}
