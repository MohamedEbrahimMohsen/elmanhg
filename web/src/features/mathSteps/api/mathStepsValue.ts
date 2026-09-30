export interface MathStep {
  id: string;
  latex: string;
}

export interface MathStepsValue {
  steps: MathStep[];
  finalAnswer: string;
}

export interface MathStepsPayload {
  steps: string[];
  finalAnswer: string;
}

export type MoveDirection = -1 | 1;

export type MathFieldId = 'final' | `step:${string}`;

export const mathStepsMaxCount = 20;
export const mathStepMaxLength = 500;
export const mathFinalAnswerMaxLength = 200;

const arabicIndicDigits = /[٠-٩۰-۹٫]/g;

const htmlEscapes: Record<string, string> = { '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };

export function stepFieldId(stepId: string): MathFieldId {
  return `step:${stepId}`;
}

export function newMathStep(latex = ''): MathStep {
  return { id: crypto.randomUUID(), latex };
}

export function emptyMathStepsValue(): MathStepsValue {
  return { steps: [newMathStep()], finalAnswer: '' };
}

export function addMathStep(value: MathStepsValue): MathStepsValue {
  return value.steps.length >= mathStepsMaxCount ? value : { ...value, steps: [...value.steps, newMathStep()] };
}

export function removeMathStep(value: MathStepsValue, stepId: string): MathStepsValue {
  if (value.steps.length <= 1 || !value.steps.some((step) => step.id === stepId)) {
    return value;
  }
  return { ...value, steps: value.steps.filter((step) => step.id !== stepId) };
}

export function moveMathStep(value: MathStepsValue, stepId: string, direction: MoveDirection): MathStepsValue {
  const index = value.steps.findIndex((step) => step.id === stepId);
  const moving = value.steps[index];
  const other = value.steps[index + direction];
  if (index === -1 || !moving || !other) {
    return value;
  }
  const steps = [...value.steps];
  steps[index] = other;
  steps[index + direction] = moving;
  return { ...value, steps };
}

export function fieldValue(value: MathStepsValue, field: MathFieldId): string {
  if (field === 'final') {
    return value.finalAnswer;
  }
  return value.steps.find((step) => stepFieldId(step.id) === field)?.latex ?? '';
}

export function setFieldValue(value: MathStepsValue, field: MathFieldId, latex: string): MathStepsValue {
  if (field === 'final') {
    return { ...value, finalAnswer: latex };
  }
  return { ...value, steps: value.steps.map((step) => (stepFieldId(step.id) === field ? { ...step, latex } : step)) };
}

export function fieldMaxLength(field: MathFieldId): number {
  return field === 'final' ? mathFinalAnswerMaxLength : mathStepMaxLength;
}

export function toLatinDigits(text: string): string {
  return text.replace(arabicIndicDigits, (digit) => {
    const code = digit.charCodeAt(0);
    if (code === 0x066b) {
      return '.';
    }
    return String(code >= 0x06f0 ? code - 0x06f0 : code - 0x0660);
  });
}

export function toMathStepsPayload(value: MathStepsValue): MathStepsPayload {
  return {
    steps: value.steps.map((step) => toLatinDigits(step.latex).trim()).filter((latex) => latex !== ''),
    finalAnswer: toLatinDigits(value.finalAnswer).trim(),
  };
}

export function fromMathStepsPayload(payload: MathStepsPayload): MathStepsValue {
  const steps = payload.steps.map((latex) => newMathStep(latex));
  return { steps: steps.length === 0 ? [newMathStep()] : steps, finalAnswer: payload.finalAnswer };
}

export function isMathStepsPayload(candidate: unknown): candidate is MathStepsPayload {
  if (typeof candidate !== 'object' || candidate === null) {
    return false;
  }
  const { steps, finalAnswer } = candidate as Partial<Record<keyof MathStepsPayload, unknown>>;
  return (
    Array.isArray(steps) &&
    steps.length <= mathStepsMaxCount &&
    steps.every((step) => typeof step === 'string' && step.length <= mathStepMaxLength) &&
    typeof finalAnswer === 'string' &&
    finalAnswer.length <= mathFinalAnswerMaxLength
  );
}

export function toMathPreviewHtml(latex: string): string {
  const escaped = toLatinDigits(latex)
    .replaceAll('&', '&amp;')
    .replace(/[<>"']/g, (character) => htmlEscapes[character] ?? character);
  return `<div data-type="block-math" data-latex="${escaped}"></div>`;
}
