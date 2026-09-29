import { describe, expect, it } from 'vitest';
import { countWords, nextCriterionId, readEssay, rubricTotalPoints, toEssayContent } from './essayValues';
import { emptyQuestionValues } from './questionValues';

const spec = {
  criteria: [
    {
      id: 'c1',
      title: 'Definition',
      points: 2,
      levels: [
        { points: 0, description: 'Missing' },
        { points: 2, description: 'Complete' },
      ],
    },
  ],
  modelAnswers: ['<p>Inertia.</p>'],
};

describe('essayValues', () => {
  it('reads a stored essay into editor values', () => {
    expect(readEssay({ maxWords: 200 }, spec)).toEqual({
      maxWords: '200',
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
      modelAnswers: [{ text: '<p>Inertia.</p>' }],
    });
  });

  it('reads an essay without a word limit as empty', () => {
    expect(readEssay({}, spec).maxWords).toBe('');
  });

  it('builds the essay body and grading spec from values', () => {
    const values = {
      ...emptyQuestionValues('Essay'),
      criteria: [
        {
          id: 'c1',
          title: 'Definition',
          description: 'Names the law',
          points: '2',
          levels: [
            { points: '0', description: 'Missing' },
            { points: '2', description: 'Complete' },
          ],
        },
      ],
      modelAnswers: [{ text: '<p>Inertia.</p>' }],
    };

    expect(toEssayContent(values)).toEqual({
      body: {},
      gradingSpec: {
        criteria: [
          {
            id: 'c1',
            title: 'Definition',
            description: 'Names the law',
            points: 2,
            levels: [
              { points: 0, description: 'Missing' },
              { points: 2, description: 'Complete' },
            ],
          },
        ],
        modelAnswers: ['<p>Inertia.</p>'],
      },
    });
    expect(toEssayContent({ ...values, maxWords: '150' }).body).toEqual({ maxWords: 150 });
  });

  it('picks the first free criterion id', () => {
    expect(nextCriterionId(['c1', 'c3'])).toBe('c2');
    expect(nextCriterionId([])).toBe('c1');
  });

  it('totals only whole criterion points', () => {
    expect(rubricTotalPoints([{ points: '2' }, { points: 'x' }, { points: '3' }])).toBe(5);
  });

  it('counts words across spaces and new lines', () => {
    expect(countWords('  a  b\nc ')).toBe(3);
    expect(countWords('')).toBe(0);
  });
});
