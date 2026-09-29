import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { http } from '@/shared/lib/http';
import { blobToDataUrl } from '../api/blobToDataUrl';

export interface ThreadImageProps {
  url: string;
}

export function ThreadImage({ url }: ThreadImageProps) {
  const { t } = useTranslation('askTeacher');
  const image = useQuery({
    queryKey: ['askTeacher', 'image', url],
    queryFn: async ({ signal }) => blobToDataUrl(await http<Blob>(url, { signal }), 'image/'),
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
