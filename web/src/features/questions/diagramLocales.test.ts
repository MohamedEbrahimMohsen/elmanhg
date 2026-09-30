import { describe, expect, it } from 'vitest';
import { i18n } from '@/app/i18n';
import commonEn from '@/shared/i18n/en.json';
import { registerDiagramLocales } from './diagramLocales';
import diagramAr from './i18n/diagram.ar.json';
import diagramEn from './i18n/diagram.en.json';
import errorsAr from './i18n/diagramErrors.ar.json';
import errorsEn from './i18n/diagramErrors.en.json';

const keysOf = (value: object, prefix = ''): string[] =>
  Object.entries(value).flatMap(([key, child]) =>
    typeof child === 'object' && child !== null ? keysOf(child as object, `${prefix}${key}.`) : [`${prefix}${key}`],
  );

describe('registerDiagramLocales', () => {
  it('keeps the diagram error codes out of the eager common bundle', () => {
    expect(Object.keys(commonEn.errors).filter((code) => code.startsWith('QUESTION_DIAGRAM_'))).toEqual([]);
  });

  it('adds the diagram namespace and error codes without overwriting common strings', () => {
    registerDiagramLocales();

    expect(i18n.t('questionsDiagram:editor.dragDrop.addZone', { lng: 'en' })).toBe('Add zone');
    expect(i18n.t('common:errors.QUESTION_DIAGRAM_ZONES_OVERLAP', { lng: 'en' })).toBe('Drop zones must not overlap.');
    expect(i18n.t('common:errors.UNHANDLED_EXCEPTION', { lng: 'en' })).toBe(commonEn.errors.UNHANDLED_EXCEPTION);
  });

  it('has the same keys in Arabic and English', () => {
    expect(keysOf(diagramAr)).toEqual(keysOf(diagramEn));
    expect(keysOf(errorsAr)).toEqual(keysOf(errorsEn));
  });
});
