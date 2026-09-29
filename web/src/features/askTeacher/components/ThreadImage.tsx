import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { http } from '@/shared/lib/http';

export interface ThreadImageProps {
  url: string;
}

// Keeps each String.fromCharCode spread well under the engine's argument limit.
const chunkSize = 0x8000;

async function toDataUrl(blob: Blob): Promise<string> {
  if (!blob.type.startsWith('image/')) {
    throw new Error('The attachment is not an image.');
  }
  const bytes = new Uint8Array(await blob.arrayBuffer());
  let binary = '';
  for (let index = 0; index < bytes.length; index += chunkSize) {
    binary += String.fromCharCode(...bytes.subarray(index, index + chunkSize));
  }
  return `data:${blob.type};base64,${btoa(binary)}`;
}

export function ThreadImage({ url }: ThreadImageProps) {
  const { t } = useTranslation('askTeacher');
  const image = useQuery({
    queryKey: ['askTeacher', 'image', url],
    queryFn: async ({ signal }) => toDataUrl(await http<Blob>(url, { signal })),
    staleTime: Infinity,
  });

  if (image.isPending) {
    return (
      <div
        role="status"
        aria-busy="true"
        aria-label={t('thread.imageLoading')}
        className="h-40 w-40 rounded-sm bg-soft"
      />
    );
  }
  if (image.isError) {
    return <p className="text-caption text-danger">{t('thread.imageError')}</p>;
  }
  return (
    <img
      src={image.data}
      alt={t('thread.imageAlt')}
      loading="lazy"
      className="max-h-80 w-auto rounded-sm border border-border"
    />
  );
}
