import { describe, expect, it } from 'vitest';
import { trainingExportSearchSchema } from './trainingExportSearchSchema';

describe('trainingExportSearchSchema', () => {
  it('keeps a valid page', () => {
    expect(trainingExportSearchSchema.parse({ page: '3' })).toEqual({ page: 3 });
  });

  it('drops a page below one or that is not a number', () => {
    expect(trainingExportSearchSchema.parse({ page: 0 }).page).toBeUndefined();
    expect(trainingExportSearchSchema.parse({ page: 'two' }).page).toBeUndefined();
  });
});
