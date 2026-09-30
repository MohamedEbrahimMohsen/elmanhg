import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import type { TrainingExportResult } from '@/shared/api/generated/model';
import { getDownloadTrainingExportFileUrl } from '@/shared/api/generated/training-exports/training-exports';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { http } from '@/shared/lib/http';
import { downloadBlob } from '../api/downloadBlob';

export interface TrainingExportDownload {
  download: (item: TrainingExportResult) => void;
  pendingId: string | null;
}

export function useDownloadTrainingExport(): TrainingExportDownload {
  const { t } = useTranslation('trainingExport');
  const mutation = useMutation({
    mutationFn: (item: TrainingExportResult) => http<Blob>(getDownloadTrainingExportFileUrl(item.id)),
    onSuccess: (blob, item) => {
      downloadBlob(blob, item.fileName);
    },
    onError: (error) => {
      const code = error instanceof ApiError ? error.code : unhandledErrorCode;
      toast.error(t('list.downloadFailed'), {
        description: t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION']),
      });
    },
  });

  return {
    download: (item) => {
      mutation.mutate(item);
    },
    pendingId: mutation.isPending ? mutation.variables.id : null,
  };
}
