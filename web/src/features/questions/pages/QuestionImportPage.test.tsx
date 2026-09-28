import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { getGetLessonMockHandler } from '@/shared/api/generated/lessons/lessons.msw';
import type { LessonDetailResult, QuestionImportPreviewResult } from '@/shared/api/generated/model';
import {
  getGetQuestionImportTemplateMockHandler,
  getImportQuestionsMockHandler,
  getPreviewQuestionImportMockHandler,
} from '@/shared/api/generated/question-imports/question-imports.msw';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const lessonId = '11111111-1111-4111-8111-111111111111';
const batchResultId = '22222222-2222-4222-8222-222222222222';

const lesson: LessonDetailResult = {
  id: lessonId,
  unitId: '33333333-3333-4333-8333-333333333333',
  name: "Newton's laws",
  order: 1,
  state: 'Draft',
  explanation: '',
  summary: '',
  videoUrl: null,
  objectives: [],
};

const cleanPreview: QuestionImportPreviewResult = {
  totalRows: 3,
  validRows: 3,
  types: [
    { type: 'Mcq', count: 2 },
    { type: 'TrueFalse', count: 1 },
  ],
  errors: [],
};

const workbook = (name = 'q.xlsx') => new File([new Uint8Array([0x50, 0x4b, 0x03, 0x04])], name);

const openImport = (lng: 'en' | 'ar' = 'en') =>
  renderApp(`/admin/question/import/${lessonId}`, { session: testSessions.admin, lng });

const checkFile = async (user: ReturnType<typeof userEvent.setup>, file = workbook()) => {
  await user.upload(await screen.findByLabelText('Spreadsheet file (.xlsx)'), file);
  await user.click(screen.getByRole('button', { name: 'Check file' }));
};

const textField = (value: FormDataEntryValue | null) => (typeof value === 'string' ? value : '');

const statusWith = (text: string) => {
  const match = screen.getAllByRole('status').find((element) => within(element).queryByText(text) !== null);
  if (!match) {
    throw new Error(`No status region contains "${text}"`);
  }
  return match;
};

describe('QuestionImportPage', () => {
  let batchIds: string[];

  beforeEach(() => {
    batchIds = [];
    server.use(
      getGetLessonMockHandler(lesson),
      getPreviewQuestionImportMockHandler(cleanPreview),
      getImportQuestionsMockHandler(async ({ request }) => {
        batchIds.push(textField((await request.formData()).get('batchId')));
        return { batchId: batchResultId, createdCount: 3, replayed: false };
      }),
    );
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('shows a loading state then the lesson in the breadcrumb', async () => {
    openImport();

    expect(await screen.findByRole('status', { name: 'Loading lesson' })).toBeInTheDocument();
    expect(await screen.findByRole('link', { name: "Newton's laws" })).toHaveAttribute(
      'href',
      `/admin/lesson/${lessonId}`,
    );
    expect(screen.getByRole('heading', { level: 1, name: 'Import questions' })).toBeInTheDocument();
  });

  it('shows an error with retry when the lesson fails to load', async () => {
    server.use(
      http.get('*/api/lessons/:lessonId', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 })),
    );
    const user = userEvent.setup();
    openImport();

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load the lesson');
    server.use(getGetLessonMockHandler(lesson));
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Import questions' })).toBeInTheDocument();
  });

  it('shows the required error when checking without a file', async () => {
    const user = userEvent.setup();
    openImport();

    await user.click(await screen.findByRole('button', { name: 'Check file' }));

    expect(await screen.findByText('Choose a file.')).toBeInTheDocument();
  });

  it('shows the check result with counts per type when the file is valid', async () => {
    const user = userEvent.setup();
    openImport();

    await checkFile(user);

    expect(await screen.findByText('3 rows found · 3 ready · 0 problems')).toBeInTheDocument();
    expect(screen.getByText('Multiple choice: 2')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Import 3 questions' })).toBeEnabled();
  });

  it('lists row problems and offers no import when the file has errors', async () => {
    server.use(
      getPreviewQuestionImportMockHandler({
        totalRows: 2,
        validRows: 1,
        types: [{ type: 'Mcq', count: 1 }],
        errors: [{ sheet: 'Mcq', row: 2, column: null, code: 'QUESTION_CORRECT_OPTION_INVALID' }],
      }),
    );
    const user = userEvent.setup();
    openImport();

    await checkFile(user);

    const table = await screen.findByRole('table', { name: 'Problems to fix' });
    const row = within(table).getAllByRole('row')[1];
    expect(row).toHaveTextContent('Mcq');
    expect(row).toHaveTextContent('2');
    expect(row).toHaveTextContent('Mark the correct answer among the options.');
    expect(screen.queryByRole('button', { name: /^Import/ })).not.toBeInTheDocument();
    expect(
      screen.getByText('Fix these problems in your file, then check it again. Nothing was imported.'),
    ).toBeVisible();
  });

  it('shows a file error inline when the server cannot read the file', async () => {
    server.use(
      http.post('*/api/question-imports/preview', () =>
        HttpResponse.json({ code: 'SPREADSHEET_UNREADABLE' }, { status: 400 }),
      ),
    );
    const user = userEvent.setup();
    openImport();

    await checkFile(user);

    expect(await screen.findByText('The file could not be read as an Excel spreadsheet.')).toBeInTheDocument();
    expect(screen.getByLabelText('Spreadsheet file (.xlsx)')).toHaveAccessibleDescription(
      expect.stringContaining('The file could not be read as an Excel spreadsheet.'),
    );
  });

  it('imports after a clean check and shows the success panel', async () => {
    const user = userEvent.setup();
    openImport();

    await checkFile(user);
    await user.click(await screen.findByRole('button', { name: 'Import 3 questions' }));

    await screen.findByText('3 questions imported as pending review.');
    const panel = statusWith('3 questions imported as pending review.');
    expect(within(panel).getByRole('link', { name: "View the lesson's questions" })).toHaveAttribute(
      'href',
      `/admin/questions?lessonId=${lessonId}`,
    );
  });

  it('reuses the batch id when the import is retried after a failure', async () => {
    let calls = 0;
    server.use(
      http.post('*/api/question-imports', async ({ request }) => {
        batchIds.push(textField((await request.formData()).get('batchId')));
        calls += 1;
        return calls === 1
          ? HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 })
          : HttpResponse.json({ batchId: batchResultId, createdCount: 3, replayed: false });
      }),
    );
    const user = userEvent.setup();
    openImport();

    await checkFile(user);
    await user.click(await screen.findByRole('button', { name: 'Import 3 questions' }));
    expect(await screen.findByText('Something went wrong. Please try again.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Import 3 questions' }));

    expect(await screen.findByText('3 questions imported as pending review.')).toBeInTheDocument();
    expect(batchIds).toHaveLength(2);
    expect(batchIds[0]).not.toBe('');
    expect(batchIds[1]).toBe(batchIds[0]);
  });

  it('clears the check result when another file is chosen', async () => {
    const user = userEvent.setup();
    openImport();

    await checkFile(user);
    expect(await screen.findByRole('heading', { name: 'Check result' })).toBeInTheDocument();
    await user.upload(screen.getByLabelText('Spreadsheet file (.xlsx)'), workbook('other.xlsx'));

    expect(screen.queryByRole('heading', { name: 'Check result' })).not.toBeInTheDocument();
  });

  it('downloads the template when the button is clicked', async () => {
    server.use(getGetQuestionImportTemplateMockHandler(new Blob([new Uint8Array([0x50, 0x4b])])));
    const createObjectURL = vi.fn<(blob: Blob) => string>(() => 'blob:t');
    Object.defineProperty(URL, 'createObjectURL', { value: createObjectURL, configurable: true, writable: true });
    Object.defineProperty(URL, 'revokeObjectURL', { value: vi.fn(), configurable: true, writable: true });
    const downloads: string[] = [];
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      downloads.push(this.download);
    });
    const user = userEvent.setup();
    openImport();

    await user.click(await screen.findByRole('button', { name: 'Download template' }));

    await vi.waitFor(() => {
      expect(downloads).toEqual(['elmanhg-question-import-template.xlsx']);
    });
    expect(createObjectURL).toHaveBeenCalledWith(expect.any(Blob));
  });

  it('renders right-to-left in Arabic', async () => {
    openImport('ar');

    expect(await screen.findByRole('heading', { level: 1, name: 'استيراد أسئلة' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    const { container } = openImport();

    await screen.findByRole('heading', { level: 1, name: 'Import questions' });

    expect((await axe(container)).violations).toEqual([]);
  });
});
