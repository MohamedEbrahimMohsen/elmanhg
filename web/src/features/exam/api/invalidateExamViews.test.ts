import { describe, expect, it } from 'vitest';
import {
  getGetExamSessionQueryKey,
  getGetMultiUnitExamOverviewQueryKey,
  getGetUnitExamOverviewQueryKey,
} from '@/shared/api/generated/exams/exams';
import { getGetSessionHistoryQueryKey, getGetSubjectProgressQueryKey } from '@/shared/api/generated/progress/progress';
import { createTestQueryClient } from '@/test/renderWithProviders';
import { examSessionId, examSubjectId, examUnitId } from '@/test/examFixtures';
import { invalidateExamViews } from './invalidateExamViews';

describe('invalidateExamViews', () => {
  it('marks the unit exam start page, history and progress stale and leaves the exam session fresh', async () => {
    const client = createTestQueryClient();
    const overviewKey = getGetUnitExamOverviewQueryKey(examUnitId);
    const historyKey = getGetSessionHistoryQueryKey({ pageNumber: 1, pageSize: 20 });
    const progressKey = getGetSubjectProgressQueryKey();
    const sessionKey = getGetExamSessionQueryKey(examSessionId);
    client.setQueryData(overviewKey, { unitId: examUnitId });
    client.setQueryData(historyKey, { items: [] });
    client.setQueryData(progressKey, []);
    client.setQueryData(sessionKey, { id: examSessionId });

    await invalidateExamViews(client);

    expect(client.getQueryState(overviewKey)?.isInvalidated).toBe(true);
    expect(client.getQueryState(historyKey)?.isInvalidated).toBe(true);
    expect(client.getQueryState(progressKey)?.isInvalidated).toBe(true);
    expect(client.getQueryState(sessionKey)?.isInvalidated).toBe(false);
  });

  it('marks the multi-unit overview stale', async () => {
    const client = createTestQueryClient();
    const overviewKey = getGetMultiUnitExamOverviewQueryKey(examSubjectId);
    client.setQueryData(overviewKey, { subjectId: examSubjectId });

    await invalidateExamViews(client);

    expect(client.getQueryState(overviewKey)?.isInvalidated).toBe(true);
  });
});
