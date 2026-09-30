import { describe, expect, it } from 'vitest';
import { emptyAnswer } from '@/features/questions';
import { quizItem } from '@/test/quizFixtures';
import { fromAnswerPayload, isAnswerEmpty, questionImageSources, toQuizQuestion } from './quizItem';

const imageUrl = '/api/media/question-diagrams/l/abc.png';
const body = {
  image: { key: 'question-diagrams/l/abc.png', url: imageUrl, width: 800, height: 600, alt: 'Plant cell' },
  zones: [
    { id: 'z1', x: 10, y: 10, width: 20, height: 15, capacity: 2 },
    { id: 'z2', x: 50, y: 40, width: 30, height: 20.5, capacity: 2 },
  ],
  items: [
    { id: 'i1', text: 'Nucleus' },
    { id: 'i5', text: 'Engine' },
  ],
};
const item = quizItem(1, { type: 'DragDrop', body, stem: '<p>Label the plant cell.</p>' });
const question = toQuizQuestion(item);

describe('quizItem drag and drop', () => {
  it('reads the served diagram of a drag-and-drop item', () => {
    expect(question.type).toBe('DragDrop');
    expect(question.diagram?.image).toEqual({ url: imageUrl, width: 800, height: 600, alt: 'Plant cell' });
    expect(question.diagram?.zones.map((zone) => zone.id)).toEqual(['z1', 'z2']);
  });

  it('restores placements from an attempt answer', () => {
    const answer = fromAnswerPayload(question, { placements: [{ zoneId: 'z2', itemIds: ['i1'] }] });

    expect(answer.placements).toEqual({ z2: ['i1'] });
  });

  it('treats an answer with no placed item as empty', () => {
    expect(isAnswerEmpty(question, { ...emptyAnswer(), placements: { z1: [] } })).toBe(true);
    expect(isAnswerEmpty(question, { ...emptyAnswer(), placements: { z1: ['i1'] } })).toBe(false);
  });

  it('preloads the diagram image of a drag-and-drop item', () => {
    expect(questionImageSources(item)).toContain(imageUrl);
  });
});
