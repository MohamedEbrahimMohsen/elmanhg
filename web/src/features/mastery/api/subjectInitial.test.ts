import { describe, expect, it } from 'vitest';
import { subjectInitial } from './subjectInitial';

describe('subjectInitial', () => {
  it('drops the Arabic definite article', () => {
    expect(subjectInitial('الرياضيات')).toBe('ر');
  });

  it('uppercases the first Latin letter', () => {
    expect(subjectInitial('physics')).toBe('P');
  });

  it('ignores surrounding spaces and keeps a bare article', () => {
    expect(subjectInitial('  ال  ')).toBe('ا');
    expect(subjectInitial(' Chemistry')).toBe('C');
  });

  it('returns an empty string for an empty name', () => {
    expect(subjectInitial('')).toBe('');
  });
});
