import DOMPurify from 'dompurify';

export interface SafeHtmlProps {
  html: string;
  className?: string;
}

export function SafeHtml({ html, className }: SafeHtmlProps) {
  // eslint-disable-next-line no-restricted-syntax -- the one sanctioned raw-HTML sink; DOMPurify runs on every render.
  return <div className={className} dangerouslySetInnerHTML={{ __html: DOMPurify.sanitize(html) }} />;
}
