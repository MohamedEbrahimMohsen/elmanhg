import 'katex/dist/katex.min.css';
import { SafeHtml } from '@/shared/components/SafeHtml';
import { renderMath } from './renderMath';

export interface RichTextViewerProps {
  html: string;
}

export function RichTextViewer({ html }: RichTextViewerProps) {
  return <SafeHtml html={renderMath(html)} className="rich-text" />;
}
