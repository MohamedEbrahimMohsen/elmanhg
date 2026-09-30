import { describe, expect, it } from 'vitest';
import { trainingExportRequestSchema } from './trainingExportRequestSchema';

const valid = {
  source: 'Avatar',
  subjectId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
  from: '2026-01-01',
  to: '2026-01-31',
};

describe('trainingExportRequestSchema', () => {
  it('accepts valid values', () => {
    expect(trainingExportRequestSchema.safeParse(valid).success).toBe(true);
  });

  it('rejects an unknown source', () => {
    const result = trainingExportRequestSchema.safeParse({ ...valid, source: 'Payments' });

    expect(result.error?.issues[0]?.message).toBe('trainingExport:form.errors.source');
  });

  it('rejects a date that is not a date', () => {
    const result = trainingExportRequestSchema.safeParse({ ...valid, from: '' });

    expect(result.error?.issues[0]?.message).toBe('trainingExport:form.errors.date');
    expect(result.error?.issues[0]?.path).toEqual(['from']);
  });

  it('rejects an end date before the start date on the end field', () => {
    const result = trainingExportRequestSchema.safeParse({ ...valid, from: '2026-02-01', to: '2026-01-31' });

    expect(result.error?.issues[0]?.message).toBe('trainingExport:form.errors.dateRange');
    expect(result.error?.issues[0]?.path).toEqual(['to']);
  });

  it('rejects a 367-day span', () => {
    const result = trainingExportRequestSchema.safeParse({ ...valid, from: '2026-01-01', to: '2027-01-02' });

    expect(result.error?.issues[0]?.message).toBe('trainingExport:form.errors.rangeTooWide');
    expect(result.error?.issues[0]?.path).toEqual(['to']);
  });

  it('accepts a 366-day span', () => {
    expect(trainingExportRequestSchema.safeParse({ ...valid, from: '2026-01-01', to: '2027-01-01' }).success).toBe(
      true,
    );
  });

  it('accepts all subjects and rejects a subject that is not an id', () => {
    expect(trainingExportRequestSchema.safeParse({ ...valid, subjectId: '' }).success).toBe(true);
    expect(trainingExportRequestSchema.safeParse({ ...valid, subjectId: 'physics' }).success).toBe(false);
  });
});
