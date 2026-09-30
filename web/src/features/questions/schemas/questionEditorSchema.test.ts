import { describe, expect, it } from 'vitest';
import { emptyQuestionValues } from '../api/questionValues';
import { questionEditorSchema, type QuestionValues } from './questionEditorSchema';

const mcq = (overrides: Partial<QuestionValues> = {}): QuestionValues => ({
  ...emptyQuestionValues('Mcq'),
  stem: '<p>2 + 2 = ?</p>',
  options: [
    { id: 'a', text: '<p>3</p>', correct: false },
    { id: 'b', text: '<p>4</p>', correct: true },
  ],
  ...overrides,
});

type Zone = QuestionValues['diagramZones'][number];

const diagramZone = (overrides: Partial<Zone> = {}): Zone => ({
  id: 'z1',
  x: '10',
  y: '10',
  width: '20',
  height: '15',
  capacity: '1',
  ordered: false,
  itemIds: ['i1'],
  ...overrides,
});

const dragDrop = (overrides: Partial<QuestionValues> = {}): QuestionValues => ({
  ...emptyQuestionValues('DragDrop'),
  stem: '<p>Label the plant cell.</p>',
  diagramImage: { key: 'question-diagrams/x/y.png', url: '/api/media/x/y.png', width: 800, height: 600, alt: 'Cell' },
  diagramZones: [diagramZone()],
  diagramItems: [
    { id: 'i1', text: 'Nucleus' },
    { id: 'i2', text: 'Engine' },
  ],
  ...overrides,
});

const zoneIssues = (overrides: Partial<Zone>) => issues(dragDrop({ diagramZones: [diagramZone(overrides)] }));

const issues = (values: QuestionValues) =>
  (questionEditorSchema.safeParse(values).error?.issues ?? []).map((issue) => ({
    path: issue.path.join('.'),
    message: issue.message,
  }));

describe('questionEditorSchema', () => {
  it('accepts a complete multiple-choice question', () => {
    expect(questionEditorSchema.safeParse(mcq()).success).toBe(true);
  });

  it('requires question text', () => {
    expect(issues(mcq({ stem: '<p></p>' }))).toContainEqual({ path: 'stem', message: 'validation.required' });
  });

  it('accepts question text that is only a formula', () => {
    const stem = '<p><span data-type="inline-math" data-latex="F=ma"></span></p>';

    expect(questionEditorSchema.safeParse(mcq({ stem })).success).toBe(true);
  });

  it.each(['0', '101', '1.5', 'abc'])('rejects points outside 1 to 100', (maxScore) => {
    expect(issues(mcq({ maxScore }))).toContainEqual({
      path: 'maxScore',
      message: 'questions:editor.errors.maxScore',
    });
  });

  it('requires at least two options', () => {
    const options = [{ id: 'a', text: '<p>4</p>', correct: true }];

    expect(issues(mcq({ options }))).toContainEqual({
      path: 'options',
      message: 'questions:editor.errors.optionsCount',
    });
  });

  it('requires text on every option', () => {
    const options = [
      { id: 'a', text: '<p>3</p>', correct: true },
      { id: 'b', text: '<p></p>', correct: false },
    ];

    expect(issues(mcq({ options }))).toContainEqual({ path: 'options.1.text', message: 'validation.required' });
  });

  it.each([0, 2])('requires exactly one correct option for multiple choice', (count) => {
    const options = ['a', 'b'].map((id, index) => ({ id, text: `<p>${id}</p>`, correct: index < count }));

    expect(issues(mcq({ options }))).toContainEqual({
      path: 'options',
      message: 'questions:editor.errors.correctOption',
    });
  });

  it('requires a correct option for multiple answers', () => {
    const options = ['a', 'b'].map((id) => ({ id, text: `<p>${id}</p>`, correct: false }));

    expect(issues(mcq({ type: 'Multi', options }))).toContainEqual({
      path: 'options',
      message: 'questions:editor.errors.correctOptions',
    });
  });

  it('requires the true-or-false answer', () => {
    expect(issues(mcq({ type: 'TrueFalse', trueFalseAnswer: '' })).map((issue) => issue.path)).toContain(
      'trueFalseAnswer',
    );
  });

  it('requires accepted answers for each blank', () => {
    const values = mcq({ type: 'Fill', stem: '<p>v = [[1]] m/s</p>', blanks: [{ id: '1', acceptedAnswers: ' \n ' }] });

    expect(issues(values)).toContainEqual({
      path: 'blanks.0.acceptedAnswers',
      message: 'questions:editor.errors.acceptedAnswers',
    });
  });

  it.each(['<p>v = m/s</p>', '<p>[[1]] and [[1]]</p>'])('requires each blank placeholder exactly once', (stem) => {
    const values = mcq({ type: 'Fill', stem, blanks: [{ id: '1', acceptedAnswers: '20' }] });

    expect(issues(values)).toContainEqual({ path: 'stem', message: 'questions:editor.errors.blankPlaceholder' });
  });

  it('requires a numeric value and a non-negative tolerance', () => {
    const paths = issues(mcq({ type: 'Short', answerKind: 'numeric', numericValue: '', tolerance: '-1' })).map(
      (issue) => issue.path,
    );

    expect(paths).toEqual(expect.arrayContaining(['numericValue', 'tolerance']));
  });

  it('requires accepted answers for a text short answer', () => {
    const paths = issues(mcq({ type: 'Short', answerKind: 'text', acceptedAnswers: '' })).map((issue) => issue.path);

    expect(paths).toContain('acceptedAnswers');
  });

  it('ignores the sections of other types', () => {
    const values = mcq({ type: 'Short', answerKind: 'text', acceptedAnswers: 'ماء', options: [] });

    expect(questionEditorSchema.safeParse(values).success).toBe(true);
  });

  const essay = (overrides: Partial<QuestionValues> = {}): QuestionValues => ({
    ...emptyQuestionValues('Essay'),
    stem: '<p>Explain inertia.</p>',
    criteria: [
      {
        id: 'c1',
        title: 'Definition',
        description: '',
        points: '2',
        levels: [
          { points: '0', description: 'Missing' },
          { points: '2', description: 'Complete' },
        ],
      },
    ],
    modelAnswers: [{ text: '<p>x</p>' }],
    ...overrides,
  });

  const essayLevels = (levels: { points: string; description: string }[]) =>
    essay({ criteria: [{ id: 'c1', title: 'Definition', description: '', points: '2', levels }] });

  it('accepts a complete essay', () => {
    expect(questionEditorSchema.safeParse(essay()).success).toBe(true);
  });

  it('requires a criterion and a model answer for an essay', () => {
    const paths = issues(essay({ criteria: [], modelAnswers: [] })).map((issue) => issue.path);

    expect(paths).toEqual(expect.arrayContaining(['criteria', 'modelAnswers']));
  });

  it('flags an essay criterion without a title or with invalid points', () => {
    const criteria = [
      {
        id: 'c1',
        title: '',
        description: '',
        points: '0',
        levels: [
          { points: '0', description: 'Missing' },
          { points: '2', description: 'Complete' },
        ],
      },
    ];

    const paths = issues(essay({ criteria })).map((issue) => issue.path);

    expect(paths).toEqual(expect.arrayContaining(['criteria.0.title', 'criteria.0.points']));
  });

  it('requires two levels on a full 0-to-points scale', () => {
    expect(issues(essayLevels([{ points: '0', description: 'Missing' }]))).toContainEqual({
      path: 'criteria.0.levels',
      message: 'questions:editor.errors.levelsCount',
    });
    expect(
      issues(
        essayLevels([
          { points: '1', description: 'Partial' },
          { points: '2', description: 'Complete' },
        ]),
      ),
    ).toContainEqual({ path: 'criteria.0.levels', message: 'questions:editor.errors.levelScale' });
  });

  it('flags an essay level without a description or above the criterion points', () => {
    const paths = issues(
      essayLevels([
        { points: '0', description: 'Missing' },
        { points: '5', description: '' },
      ]),
    ).map((issue) => issue.path);

    expect(paths).toEqual(expect.arrayContaining(['criteria.0.levels.1.description', 'criteria.0.levels.1.points']));
  });

  it('rejects an essay word limit outside 1 to 2000', () => {
    for (const maxWords of ['0', '2001', 'abc']) {
      expect(issues(essay({ maxWords }))).toContainEqual({
        path: 'maxWords',
        message: 'questions:editor.errors.maxWords',
      });
    }
    expect(issues(essay({ maxWords: '' })).map((issue) => issue.path)).not.toContain('maxWords');
  });

  it('requires model answer text', () => {
    expect(issues(essay({ modelAnswers: [{ text: '<p></p>' }] }))).toContainEqual({
      path: 'modelAnswers.0.text',
      message: 'validation.required',
    });
  });

  it('accepts a complete drag-and-drop question', () => {
    expect(questionEditorSchema.safeParse(dragDrop()).success).toBe(true);
  });

  it('requires a diagram image, its description, a zone and an item', () => {
    const values = dragDrop({
      diagramImage: { key: '', url: '', width: 0, height: 0, alt: ' ' },
      diagramZones: [],
      diagramItems: [],
    });

    expect(issues(values)).toEqual(
      expect.arrayContaining([
        { path: 'diagramImage', message: 'questionsDiagram:editor.errors.diagramImage' },
        { path: 'diagramImage.alt', message: 'validation.required' },
        { path: 'diagramZones', message: 'questionsDiagram:editor.errors.zonesCount' },
        { path: 'diagramItems', message: 'questionsDiagram:editor.errors.itemsCount' },
      ]),
    );
  });

  it('flags a zone outside the image or below the minimum size', () => {
    expect(zoneIssues({ x: '101' })).toContainEqual({
      path: 'diagramZones.0.x',
      message: 'questionsDiagram:editor.errors.zonePosition',
    });
    expect(zoneIssues({ width: '1' })).toContainEqual({
      path: 'diagramZones.0.width',
      message: 'questionsDiagram:editor.errors.zoneSize',
    });
    expect(zoneIssues({ x: '90', width: '20' })).toContainEqual({
      path: 'diagramZones.0.width',
      message: 'questionsDiagram:editor.errors.zoneSize',
    });
    expect(zoneIssues({ y: '1.234' })).toContainEqual({
      path: 'diagramZones.0.y',
      message: 'questionsDiagram:editor.errors.zonePosition',
    });
  });

  it('flags zone capacity and more correct items than a zone holds', () => {
    expect(zoneIssues({ capacity: '0' })).toContainEqual({
      path: 'diagramZones.0.capacity',
      message: 'questionsDiagram:editor.errors.zoneCapacity',
    });
    expect(zoneIssues({ capacity: '1', itemIds: ['i1', 'i2'] })).toContainEqual({
      path: 'diagramZones.0.capacity',
      message: 'questionsDiagram:editor.errors.zoneOverCapacity',
    });
  });

  it('flags an ordered zone with fewer than two correct items', () => {
    expect(zoneIssues({ ordered: true })).toContainEqual({
      path: 'diagramZones.0.ordered',
      message: 'questionsDiagram:editor.errors.zoneOrder',
    });
  });

  it('flags overlapping zones', () => {
    const values = dragDrop({
      diagramZones: [diagramZone(), diagramZone({ id: 'z2', x: '25', y: '20', itemIds: [] })],
    });

    expect(issues(values)).toContainEqual({
      path: 'diagramZones',
      message: 'questionsDiagram:editor.errors.zonesOverlap',
    });
  });

  it('requires a placed item and item text within the limit', () => {
    expect(zoneIssues({ itemIds: [] })).toContainEqual({
      path: 'diagramZones',
      message: 'questionsDiagram:editor.errors.keyEmpty',
    });
    expect(issues(dragDrop({ diagramItems: [{ id: 'i1', text: '' }] }))).toContainEqual({
      path: 'diagramItems.0.text',
      message: 'validation.required',
    });
    expect(issues(dragDrop({ diagramItems: [{ id: 'i1', text: 'a'.repeat(101) }] }))).toContainEqual({
      path: 'diagramItems.0.text',
      message: 'questionsDiagram:editor.errors.itemText',
    });
  });
});
