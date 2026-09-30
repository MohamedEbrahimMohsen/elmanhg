import type {
  PageDataOfTrainingExportResult,
  RequestTrainingExportRequest,
  TrainingExportResult,
} from '@/shared/api/generated/model';
import type { TrainingExportRequestValues } from '../schemas/trainingExportRequestSchema';

export const trainingExportSources = ['TeacherThreads', 'Avatar', 'Attempts', 'EssayGrades'] as const;

export const trainingExportPageSize = 20;

export const pendingPollIntervalMs = 5000;

export interface TrainingExportPage {
  items: TrainingExportResult[];
  pageNumber: number;
  totalPages: number;
  totalItems: number;
}

function localMidnight(isoDate: string, dayOffset: number): string {
  const [year = 0, month = 1, day = 1] = isoDate.split('-').map(Number);
  return new Date(year, month - 1, day + dayOffset).toISOString();
}

export function toRequestTrainingExportBody(values: TrainingExportRequestValues): RequestTrainingExportRequest {
  return {
    source: values.source,
    from: localMidnight(values.from, 0),
    to: localMidnight(values.to, 1),
    subjectId: values.subjectId === '' ? null : values.subjectId,
  };
}

export function hasPendingExports(items: TrainingExportResult[]): boolean {
  return items.some((item) => item.status === 'Pending');
}

export function toTrainingExportPage(data: PageDataOfTrainingExportResult): TrainingExportPage {
  return {
    items: data.items ?? [],
    pageNumber: Number(data.pageNumber ?? 1),
    totalPages: Number(data.totalPages ?? 0),
    totalItems: Number(data.totalItems ?? 0),
  };
}
