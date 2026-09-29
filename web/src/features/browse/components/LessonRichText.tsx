import { RichTextViewer } from '@/features/content';
import { hasRichText } from '../api/richText';

export interface LessonRichTextProps {
  html: string;
  emptyText: string;
}

export function LessonRichText({ html, emptyText }: LessonRichTextProps) {
  return (
    <div className="rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
      {hasRichText(html) ? <RichTextViewer html={html} /> : <p className="text-ui text-text-muted">{emptyText}</p>}
    </div>
  );
}
