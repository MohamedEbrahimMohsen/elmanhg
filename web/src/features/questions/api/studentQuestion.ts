import type { DeepPartialSkipArrayKey } from 'react-hook-form';
import type { MathStepsPayload } from '@/features/mathSteps';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import type { DiagramKey, StudentDiagram } from '../schemas/studentDiagramSchema';
import { toPlacementsPayload, type DiagramPlacements } from './diagramPlacement';
import { studentDiagramFromValues } from './studentDiagram';

export interface StudentQuestion {
  type: QuestionValues['type'];
  stem: string;
  options: { id: string; text: string }[];
  blankIds: string[];
  answerKind: 'numeric' | 'text' | null;
  maxWords?: number | null | undefined;
  diagram?: StudentDiagram | null | undefined;
}

export interface QuestionAnswer {
  optionIds: string[];
  trueFalse: boolean | null;
  blanks: Record<string, string>;
  text: string;
  math: MathStepsPayload;
  placements: DiagramPlacements;
}

export interface ChoiceReview {
  correctKeys: readonly string[];
  diagramKey?: DiagramKey | undefined;
}

export function emptyAnswer(): QuestionAnswer {
  return { optionIds: [], trueFalse: null, blanks: {}, text: '', math: { steps: [], finalAnswer: '' }, placements: {} };
}

export function toStudentQuestion(values: DeepPartialSkipArrayKey<QuestionValues>): StudentQuestion {
  const type = values.type ?? 'Mcq';
  return {
    type,
    stem: values.stem ?? '',
    options: (values.options ?? []).map((option) => ({ id: option.id ?? '', text: option.text ?? '' })),
    blankIds: (values.blanks ?? []).map((blank) => blank.id ?? ''),
    answerKind: type === 'Short' ? (values.answerKind ?? 'numeric') : null,
    maxWords: type === 'Essay' && /^\d+$/.test(values.maxWords ?? '') ? Number(values.maxWords) : null,
    diagram: type === 'DragDrop' ? studentDiagramFromValues(values) : null,
  };
}

export function fillStemHtml(stem: string, blankIds: readonly string[], marker: (index: number) => string): string {
  return blankIds.reduce((html, id, index) => html.split(`[[${id}]]`).join(`<u> ${marker(index)} </u>`), stem);
}

export function toAnswerPayload(question: StudentQuestion, answer: QuestionAnswer): Record<string, unknown> {
  switch (question.type) {
    case 'Mcq':
      return { optionId: answer.optionIds[0] ?? null };
    case 'Multi':
      return { optionIds: answer.optionIds };
    case 'TrueFalse':
      return { value: answer.trueFalse };
    case 'Fill':
      return { blanks: question.blankIds.map((id) => ({ id, text: answer.blanks[id] ?? '' })) };
    case 'Short':
      return { text: answer.text };
    case 'Essay':
      return { text: answer.text };
    case 'MathSteps':
      return { steps: answer.math.steps, finalAnswer: answer.math.finalAnswer };
    case 'DragDrop':
      return { placements: question.diagram ? toPlacementsPayload(question.diagram, answer.placements) : [] };
  }
}
