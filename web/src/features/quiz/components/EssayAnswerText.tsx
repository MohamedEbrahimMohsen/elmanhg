import { useTranslation } from 'react-i18next';

export interface EssayAnswerTextProps {
  text: string;
}

export function EssayAnswerText({ text }: EssayAnswerTextProps) {
  const { t } = useTranslation('quiz');

  return (
    <div className="flex flex-col gap-1">
      <p className="text-caption font-semibold text-text-muted">{t('essayAnswer.label')}</p>
      <p
        dir="auto"
        className="rounded-md border border-border bg-soft px-3.5 py-3 text-ui whitespace-pre-wrap text-text"
      >
        {text}
      </p>
    </div>
  );
}
