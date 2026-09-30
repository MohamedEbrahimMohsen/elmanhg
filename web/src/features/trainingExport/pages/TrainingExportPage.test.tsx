import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { PageDataOfTrainingExportResult, TrainingExportResult } from '@/shared/api/generated/model';
import { getGetSubjectsMockHandler } from '@/shared/api/generated/subjects/subjects.msw';
import {
  getGetTrainingExportsMockHandler,
  getRequestTrainingExportMockHandler,
} from '@/shared/api/generated/training-exports/training-exports.msw';
import { setAccessToken } from '@/shared/lib/authToken';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const subjectId = '3fa85f64-5717-4562-b3fc-2c963f66afa6';

const exportItem = (overrides: Partial<TrainingExportResult> = {}): TrainingExportResult => ({
  id: 'x1',
  source: 'Attempts',
  from: '2026-01-01T00:00:00Z',
  to: '2026-02-01T00:00:00Z',
  subjectId,
  status: 'Completed',
  attempts: 1,
  lastErrorCode: null,
  rowCount: 1200,
  fileSizeBytes: 20480,
  sha256: 'abc',
  fileName: 'elmanhg-attempts-2026-01-01-2026-02-01.jsonl',
  requestedAt: '2026-02-01T09:00:00Z',
  completedAt: '2026-02-01T09:01:00Z',
  expiresAt: '2026-02-08T09:01:00Z',
  ...overrides,
});

const page = (items: TrainingExportResult[]): PageDataOfTrainingExportResult => ({
  items,
  pageNumber: 1,
  pageSize: 20,
  totalItems: items.length,
  totalPages: 1,
});

const openPage = (lng: 'en' | 'ar' = 'en') => renderApp('/admin/export', { session: testSessions.admin, lng });

describe('TrainingExportPage', () => {
  beforeEach(() => {
    server.use(getGetSubjectsMockHandler([{ id: subjectId, name: 'Physics', order: 1, unitCount: 2 }]));
  });

  afterEach(() => {
    setAccessToken(null);
    vi.restoreAllMocks();
  });

  it('shows the loading state then the export rows', async () => {
    server.use(getGetTrainingExportsMockHandler(page([exportItem()])));
    openPage();

    expect(await screen.findByRole('status', { name: 'Loading export files' })).toBeInTheDocument();
    const row = await screen.findByRole('row', { name: /Attempts/ });
    expect(row).toHaveTextContent('Physics');
    expect(row).toHaveTextContent('1,200');
    expect(row).toHaveTextContent('Ready');
  });

  it('shows the empty state', async () => {
    server.use(getGetTrainingExportsMockHandler(page([])));
    openPage();

    expect(await screen.findByText('No export files yet.')).toBeInTheDocument();
  });

  it('shows an error and recovers on retry', async () => {
    server.use(
      http.get('*/api/training-exports', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 })),
    );
    const user = userEvent.setup();
    openPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load the export files');
    server.use(getGetTrainingExportsMockHandler(page([exportItem()])));
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('row', { name: /Attempts/ })).toBeInTheDocument();
  });

  it('submits the form, posts the body and shows the success toast', async () => {
    const bodies: unknown[] = [];
    server.use(
      getGetTrainingExportsMockHandler(page([])),
      getRequestTrainingExportMockHandler(async ({ request }) => {
        bodies.push(await request.json());
        return exportItem({ status: 'Pending' });
      }),
    );
    const user = userEvent.setup();
    openPage();

    await screen.findByText('No export files yet.');
    await user.selectOptions(screen.getByLabelText('Source'), 'Avatar');
    await user.selectOptions(await screen.findByLabelText('Subject'), 'Physics');
    await user.type(screen.getByLabelText('From'), '2026-01-01');
    await user.type(screen.getByLabelText('To'), '2026-01-31');
    await user.click(screen.getByRole('button', { name: 'Create export file' }));

    expect(await screen.findByText('The export file is being prepared.')).toBeInTheDocument();
    expect(bodies).toEqual([
      {
        source: 'Avatar',
        subjectId,
        from: new Date(2026, 0, 1).toISOString(),
        to: new Date(2026, 1, 1).toISOString(),
      },
    ]);
  });

  it('shows a range the server rejects inline under To', async () => {
    server.use(
      getGetTrainingExportsMockHandler(page([])),
      http.post('*/api/training-exports', () =>
        HttpResponse.json({ code: 'TRAINING_EXPORT_DATE_RANGE_TOO_WIDE' }, { status: 422 }),
      ),
    );
    const user = userEvent.setup();
    openPage();

    await screen.findByText('No export files yet.');
    await user.type(screen.getByLabelText('From'), '2026-01-01');
    await user.type(screen.getByLabelText('To'), '2026-01-31');
    await user.click(screen.getByRole('button', { name: 'Create export file' }));

    expect(await screen.findByText('The date range is longer than an export allows.')).toBeInTheDocument();
    expect(screen.getByLabelText('To')).toHaveAttribute('aria-invalid', 'true');
  });

  it('downloads a completed export with the admin token and saves it under its file name', async () => {
    setAccessToken('admin-token');
    const authorizations: (string | null)[] = [];
    server.use(
      getGetTrainingExportsMockHandler(page([exportItem()])),
      http.get('*/api/training-exports/x1/file', ({ request }) => {
        authorizations.push(request.headers.get('Authorization'));
        return new HttpResponse('{}\n', { headers: { 'Content-Type': 'application/x-ndjson' } });
      }),
    );
    Object.defineProperty(URL, 'createObjectURL', {
      value: vi.fn(() => 'blob:x1'),
      configurable: true,
      writable: true,
    });
    Object.defineProperty(URL, 'revokeObjectURL', { value: vi.fn(), configurable: true, writable: true });
    const downloads: string[] = [];
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      downloads.push(this.download);
    });
    const user = userEvent.setup();
    openPage();

    const row = await screen.findByRole('row', { name: /Attempts/ });
    await user.click(within(row).getByRole('button', { name: 'Download' }));

    await vi.waitFor(() => {
      expect(downloads).toEqual(['elmanhg-attempts-2026-01-01-2026-02-01.jsonl']);
    });
    expect(authorizations).toEqual(['Bearer admin-token']);
  });

  it('shows Preparing and no download for a pending export', async () => {
    server.use(
      getGetTrainingExportsMockHandler(page([exportItem({ status: 'Pending', rowCount: null, fileSizeBytes: null })])),
    );
    openPage();

    const row = await screen.findByRole('row', { name: /Attempts/ });
    expect(row).toHaveTextContent('Preparing…');
    expect(within(row).queryByRole('button', { name: 'Download' })).not.toBeInTheDocument();
  });

  it('shows the failed badge and the error code for a failed export', async () => {
    server.use(
      getGetTrainingExportsMockHandler(
        page([exportItem({ status: 'Failed', lastErrorCode: 'InvalidOperationException' })]),
      ),
    );
    openPage();

    const row = await screen.findByRole('row', { name: /Attempts/ });
    expect(row).toHaveTextContent('Failed');
    expect(row).toHaveTextContent('InvalidOperationException');
    expect(within(row).queryByRole('button', { name: 'Download' })).not.toBeInTheDocument();
  });

  it('shows an expired export without a download', async () => {
    server.use(getGetTrainingExportsMockHandler(page([exportItem({ status: 'Expired' })])));
    openPage();

    const row = await screen.findByRole('row', { name: /Attempts/ });
    expect(row).toHaveTextContent('Expired');
    expect(within(row).queryByRole('button', { name: 'Download' })).not.toBeInTheDocument();
  });

  it('renders right-to-left in Arabic', async () => {
    server.use(getGetTrainingExportsMockHandler(page([exportItem()])));
    openPage('ar');

    expect(await screen.findByRole('heading', { level: 1, name: 'تصدير بيانات التدريب (JSONL)' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    server.use(
      getGetTrainingExportsMockHandler(
        page([exportItem(), exportItem({ id: 'x2', status: 'Failed', lastErrorCode: 'IOException' })]),
      ),
    );
    const { container } = openPage();

    await screen.findByRole('table');

    expect((await axe(container)).violations).toEqual([]);
  });
});
