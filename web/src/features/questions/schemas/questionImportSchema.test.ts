import { describe, expect, it } from 'vitest';
import { questionImportSchema } from './questionImportSchema';

const workbook = (name: string) => new File([new Uint8Array([0x50, 0x4b, 0x03, 0x04])], name);

describe('questionImportSchema', () => {
  it('accepts an .xlsx file', () => {
    expect(questionImportSchema.safeParse({ file: workbook('questions.xlsx') }).success).toBe(true);
  });

  it('accepts an upper-case .XLSX extension', () => {
    expect(questionImportSchema.safeParse({ file: workbook('QUESTIONS.XLSX') }).success).toBe(true);
  });

  it('rejects a missing file with the required key', () => {
    expect(questionImportSchema.safeParse({}).error?.issues[0]?.message).toBe('validation.fileRequired');
  });

  it('rejects a .csv file with the file type key', () => {
    expect(questionImportSchema.safeParse({ file: workbook('questions.csv') }).error?.issues[0]?.message).toBe(
      'errors.QUESTION_IMPORT_FILE_TYPE_INVALID',
    );
  });
});
