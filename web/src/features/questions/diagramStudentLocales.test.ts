import { describe, expect, it } from 'vitest';
import { i18n } from '@/app/i18n';
import { registerDiagramStudentLocales } from './diagramStudentLocales';

describe('registerDiagramStudentLocales', () => {
  it('registers the student diagram strings in both languages', () => {
    registerDiagramStudentLocales();

    expect(i18n.t('diagramStudent:zonesTitle', { lng: 'en' })).toBe('Zones');
    expect(i18n.t('diagramStudent:zonesTitle', { lng: 'ar' })).toBe('المناطق');
  });
});
