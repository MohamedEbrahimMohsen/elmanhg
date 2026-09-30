import { useTranslation } from 'react-i18next';
import { RichTextViewer } from '@/features/content';
import { toMathPreviewHtml } from '../api/mathStepsValue';

export interface MathPreviewProps {
  latex: string;
  label: string;
}

export function MathPreview({ latex, label }: MathPreviewProps) {
  const { t } = useTranslation('mathSteps');

  return (
    <div
      role="group"
      aria-label={label}
      className="min-h-11 overflow-x-auto rounded-sm border border-border bg-surface px-3 py-2"
    >
      {latex.trim() === '' ? (
        <p className="text-caption text-text-muted">{t('preview.empty')}</p>
      ) : (
        <div dir="ltr">
          <RichTextViewer html={toMathPreviewHtml(latex)} />
        </div>
      )}
    </div>
  );
}
