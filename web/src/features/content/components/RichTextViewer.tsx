import { lazy, Suspense } from 'react';
import { SafeHtml } from '@/shared/components/SafeHtml';
import { lazyImages } from './lazyImages';
import { hasMath, loadMathRenderer } from './mathRenderer';

export interface RichTextViewerProps {
  html: string;
}

const MathHtml = lazy(() =>
  loadMathRenderer().then(({ renderMath }) => ({
    default: function MathHtml({ html }: RichTextViewerProps) {
      return <SafeHtml html={renderMath(html)} className="rich-text" />;
    },
  })),
);

export function RichTextViewer(props: RichTextViewerProps) {
  const html = lazyImages(props.html);
  if (!hasMath(html)) {
    return <SafeHtml html={html} className="rich-text" />;
  }

  return (
    <Suspense fallback={<SafeHtml html={html} className="rich-text" />}>
      <MathHtml html={html} />
    </Suspense>
  );
}
