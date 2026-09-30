import { describe, expect, it } from 'vitest';
import type { TrainingExportResult } from '@/shared/api/generated/model';
import { hasPendingExports, toRequestTrainingExportBody, toTrainingExportPage } from './trainingExportParams';

const item = (status: TrainingExportResult['status']): TrainingExportResult => ({
  id: `x-${status}`,
  source: 'Attempts',
  from: '2026-01-01T00:00:00Z',
  to: '2026-02-01T00:00:00Z',
  subjectId: null,
  status,
  attempts: 0,
  lastErrorCode: null,
  rowCount: null,
  fileSizeBytes: null,
  sha256: null,
  fileName: 'elmanhg-attempts-2026-01-01-2026-02-01.jsonl',
  requestedAt: '2026-02-01T09:00:00Z',
  completedAt: null,
  expiresAt: null,
});

describe('trainingExportParams', () => {
  it('sends from as local midnight and to as the next local midnight', () => {
    const body = toRequestTrainingExportBody({
      source: 'Avatar',
      subjectId: 's1',
      from: '2026-09-10',
      to: '2026-09-30',
    });

    expect(body).toEqual({
      source: 'Avatar',
      subjectId: 's1',
      from: new Date(2026, 8, 10).toISOString(),
      to: new Date(2026, 8, 31).toISOString(),
    });
  });

  it('sends no subject when all subjects are chosen', () => {
    expect(
      toRequestTrainingExportBody({ source: 'Avatar', subjectId: '', from: '2026-09-10', to: '2026-09-10' }).subjectId,
    ).toBeNull();
  });

  it('detects pending exports', () => {
    expect(hasPendingExports([item('Completed'), item('Pending')])).toBe(true);
    expect(hasPendingExports([item('Completed'), item('Failed'), item('Expired')])).toBe(false);
  });

  it('normalises string paging numbers', () => {
    expect(
      toTrainingExportPage({ items: [], pageNumber: '2', pageSize: '20', totalItems: '41', totalPages: '3' }),
    ).toEqual({
      items: [],
      pageNumber: 2,
      totalPages: 3,
      totalItems: 41,
    });
  });
});
