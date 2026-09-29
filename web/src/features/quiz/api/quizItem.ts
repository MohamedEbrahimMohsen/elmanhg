import { z } from 'zod';
import {
  choiceBodySchema,
  emptyAnswer,
  fillBodySchema,
  shortBodySchema,
  type QuestionAnswer,
  type StudentQuestion,
} from '@/features/questions';
import type { SessionItemResult } from '@/shared/api/generated/model';

export const quizQuestionTypes = ['Mcq', 'Multi', 'TrueFalse', 'Fill', 'Short'] as const;

const answerPayloadSchema = z.object({
  optionId: z.string().nullish(),
  optionIds: z.array(z.string()).optional(),
  value: z.boolean().nullish(),
  blanks: z.array(z.object({ id: z.string(), text: z.string() })).optional(),
  text: z.string().nullish(),
});

export function toQuizQuestion(item: SessionItemResult): StudentQuestion {
  const type = z.enum(quizQuestionTypes).parse(item.type);
  const isChoice = type === 'Mcq' || type === 'Multi';
  return {
    type,
    stem: item.stem,
    options: isChoice ? choiceBodySchema.parse(item.body).options : [],
    blankIds: type === 'Fill' ? fillBodySchema.parse(item.body).blanks.map((blank) => blank.id) : [],
    answerKind: type === 'Short' ? shortBodySchema.parse(item.body).answerKind : null,
  };
}

export function fromAnswerPayload(question: StudentQuestion, payload: unknown): QuestionAnswer {
  const parsed = answerPayloadSchema.safeParse(payload);
  if (!parsed.success) {
    return emptyAnswer();
  }
  const { optionId, optionIds, value, blanks, text } = parsed.data;
  const answer = emptyAnswer();
  switch (question.type) {
    case 'Mcq':
      return { ...answer, optionIds: optionId ? [optionId] : [] };
    case 'Multi':
      return { ...answer, optionIds: optionIds ?? [] };
    case 'TrueFalse':
      return { ...answer, trueFalse: value ?? null };
    case 'Fill':
      return { ...answer, blanks: Object.fromEntries((blanks ?? []).map((blank) => [blank.id, blank.text])) };
    case 'Short':
      return { ...answer, text: text ?? '' };
    case 'Essay':
      return { ...answer, text: text ?? '' };
  }
}

export function isAnswerEmpty(question: StudentQuestion, answer: QuestionAnswer): boolean {
  switch (question.type) {
    case 'Mcq':
    case 'Multi':
      return answer.optionIds.length === 0;
    case 'TrueFalse':
      return answer.trueFalse === null;
    case 'Fill':
      return question.blankIds.every((id) => (answer.blanks[id] ?? '').trim() === '');
    case 'Short':
      return answer.text.trim() === '';
    case 'Essay':
      return answer.text.trim() === '';
  }
}

export function questionImageSources(item: SessionItemResult): string[] {
  const body = choiceBodySchema.safeParse(item.body);
  const htmls = [item.stem, ...(body.success ? body.data.options.map((option) => option.text) : [])];
  const sources = htmls.flatMap((html) =>
    Array.from(new DOMParser().parseFromString(html, 'text/html').images, (image) => image.getAttribute('src') ?? ''),
  );
  return [...new Set(sources.filter((src) => src !== ''))];
}
