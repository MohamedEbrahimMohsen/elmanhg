import { describe, expect, it } from 'vitest';
import { i18n } from '@/app/i18n';
import { registerTrainingExportLocales } from './locales';

describe('trainingExport locales', () => {
  it('does not ship the trainingExport namespace until registered', () => {
    expect(i18n.hasResourceBundle('en', 'trainingExport')).toBe(false);
  });

  it('resolves trainingExport:page.title after registering', () => {
    registerTrainingExportLocales();

    expect(i18n.t('trainingExport:page.title', { lng: 'en' })).toBe('Training data export (JSONL)');
    expect(i18n.t('trainingExport:page.title', { lng: 'ar' })).toBe('تصدير بيانات التدريب (JSONL)');
  });
});
