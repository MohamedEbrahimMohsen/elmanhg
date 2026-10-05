import type { ReactNode } from 'react';
import { QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook, screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { TrainingExportResult } from '@/shared/api/generated/model';
import { Toaster } from '@/shared/ui/toaster';
import { server } from '@/test/msw/server';
import { createTestQueryClient } from '@/test/renderWithProviders';
import { registerTrainingExportLocales } from '../locales';
import { useDownloadTrainingExport } from './useDownloadTrainingExport';

registerTrainingExportLocales();

const fileName = 'elmanhg-attempts-2026-01-01-2026-02-01.jsonl';

const exportItem: TrainingExportResult = {
  id: 'x1',
  source: 'Attempts',
  from: '2026-01-01T00:00:00Z',
  to: '2026-02-01T00:00:00Z',
  subjectId: null,
  status: 'Completed',
  attempts: 1,
  lastErrorCode: null,
  rowCount: 0,
  fileSizeBytes: 0,
  sha256: 'abc',
  fileName,
  requestedAt: '2026-02-01T09:00:00Z',
  completedAt: '2026-02-01T09:01:00Z',
  expiresAt: '2026-02-08T09:01:00Z',
};

function wrapper({ children }: { children: ReactNode }) {
  return (
    <QueryClientProvider client={createTestQueryClient()}>
      {children}
      <Toaster />
    </QueryClientProvider>
  );
}

describe('useDownloadTrainingExport', () => {
  let saved: { blob: Blob; name: string }[];

  beforeEach(() => {
    saved = [];
    let lastBlob = new Blob();
    Object.defineProperty(URL, 'createObjectURL', {
      value: vi.fn((blob: Blob) => {
        lastBlob = blob;
        return 'blob:x1';
      }),
      configurable: true,
      writable: true,
    });
    Object.defineProperty(URL, 'revokeObjectURL', { value: vi.fn(), configurable: true, writable: true });
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      saved.push({ blob: lastBlob, name: this.download });
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  const serve = (body: string) => {
    server.use(
      http.get(
        '*/api/training-exports/x1/file',
        () => new HttpResponse(body, { headers: { 'Content-Type': 'application/x-ndjson' } }),
      ),
    );
  };

  it('saves a non-empty JSONL export as a Blob under its file name', async () => {
    const lines = '{"row":1}\n{"row":2}\n';
    serve(lines);
    const { result } = renderHook(() => useDownloadTrainingExport(), { wrapper });

    act(() => {
      result.current.download(exportItem);
    });

    await vi.waitFor(() => {
      expect(saved).toHaveLength(1);
    });
    const [file] = saved;
    expect(file?.name).toBe(fileName);
    expect(file?.blob).toBeInstanceOf(Blob);
    await expect(file?.blob.text()).resolves.toBe(lines);
  });

  it('saves an empty JSONL export as an empty Blob without an error toast', async () => {
    serve('');
    const { result } = renderHook(() => useDownloadTrainingExport(), { wrapper });

    act(() => {
      result.current.download(exportItem);
    });

    await vi.waitFor(() => {
      expect(saved).toHaveLength(1);
    });
    const [file] = saved;
    expect(file?.name).toBe(fileName);
    expect(file?.blob).toBeInstanceOf(Blob);
    expect(file?.blob.size).toBe(0);
    await vi.waitFor(() => {
      expect(result.current.pendingId).toBeNull();
    });
    expect(screen.queryByText('Could not download the export file.')).not.toBeInTheDocument();
  });
});
