import { describe, expect, it } from 'vitest';
import { emptyQuestionValues } from './questionValues';
import { emptyAnswer, fillStemHtml, toAnswerPayload, toStudentQuestion, type StudentQuestion } from './studentQuestion';

const question = (overrides: Partial<StudentQuestion>): StudentQuestion => ({
  type: 'Mcq',
  stem: '',
  options: [],
  blankIds: [],
  answerKind: null,
  ...overrides,
});

const diagram = {
  image: { url: '/api/media/question-diagrams/l/abc.png', width: 800, height: 600, alt: 'Plant cell' },
  zones: [
    { id: 'z1', x: 10, y: 10, width: 20, height: 15, capacity: 2 },
    { id: 'z2', x: 50, y: 40, width: 30, height: 20.5, capacity: 2 },
  ],
  items: [
    { id: 'i1', text: 'Nucleus' },
    { id: 'i2', text: 'Vacuole' },
  ],
};

const payloadCases: [StudentQuestion, object][] = [
  [question({ type: 'Mcq' }), { optionId: 'b' }],
  [question({ type: 'Multi' }), { optionIds: ['b', 'c'] }],
  [question({ type: 'TrueFalse' }), { value: false }],
  [
    question({ type: 'Fill', blankIds: ['1', '2'] }),
    {
      blanks: [
        { id: '1', text: '20' },
        { id: '2', text: '' },
      ],
    },
  ],
  [question({ type: 'Short', answerKind: 'numeric' }), { text: '9.8' }],
];

describe('studentQuestion', () => {
  it('builds the student view from the form values', () => {
    const values = { ...emptyQuestionValues('Mcq'), stem: '<p>Stem</p>' };

    const view = toStudentQuestion(values);

    expect(view.options).toEqual([
      { id: 'a', text: '' },
      { id: 'b', text: '' },
      { id: 'c', text: '' },
      { id: 'd', text: '' },
    ]);
    expect(view.blankIds).toEqual(['1']);
    expect(view.answerKind).toBeNull();
    expect(toStudentQuestion({ ...values, type: 'Short', answerKind: 'text' }).answerKind).toBe('text');
  });

  it('replaces blank placeholders with numbered markers', () => {
    const html = fillStemHtml('<p>v = [[1]] m/s</p>', ['1'], (index) => `(${String(index + 1)})`);

    expect(html).toContain('<u> (1) </u>');
    expect(html).not.toContain('[[1]]');
  });

  it.each(payloadCases)('builds the answer payload for each type', (view, payload) => {
    const answer = { ...emptyAnswer(), optionIds: ['b', 'c'], trueFalse: false, blanks: { '1': '20' }, text: '9.8' };

    expect(toAnswerPayload(view, answer)).toEqual(payload);
  });

  it('carries the word limit of an essay and sends its text', () => {
    const values = { ...emptyQuestionValues('Essay'), maxWords: '150' };

    expect(toStudentQuestion(values).maxWords).toBe(150);
    expect(toStudentQuestion({ ...values, maxWords: '' }).maxWords).toBeNull();
    expect(toAnswerPayload(question({ type: 'Essay' }), { ...emptyAnswer(), text: 'x' })).toEqual({ text: 'x' });
  });

  it('sends drag-and-drop placements in zone order', () => {
    const dragDrop = question({ type: 'DragDrop', diagram });

    expect(toAnswerPayload(dragDrop, { ...emptyAnswer(), placements: { z2: [], z1: ['i1', 'x'] } })).toEqual({
      placements: [{ zoneId: 'z1', itemIds: ['i1'] }],
    });
  });

  it('builds the student diagram from editor values', () => {
    const values = {
      ...emptyQuestionValues('DragDrop'),
      diagramImage: { key: 'k', url: diagram.image.url, width: 800, height: 600, alt: 'Plant cell' },
      diagramZones: [
        { id: 'z1', x: '10', y: '10', width: '20', height: '15', capacity: '2', ordered: false, itemIds: ['i1'] },
        { id: 'z2', x: '50', y: '40', width: '30', height: '20.5', capacity: '2', ordered: true, itemIds: [] },
      ],
      diagramItems: [{ id: 'i1', text: 'Nucleus' }],
    };

    expect(toStudentQuestion(values).diagram).toEqual({
      image: diagram.image,
      zones: diagram.zones,
      items: [{ id: 'i1', text: 'Nucleus' }],
    });
    expect(toStudentQuestion({ ...values, diagramImage: { ...values.diagramImage, url: '' } }).diagram).toBeNull();
  });
});
