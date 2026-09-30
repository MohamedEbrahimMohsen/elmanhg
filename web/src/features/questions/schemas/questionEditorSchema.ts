import { z } from 'zod';
import {
  mathAnswerForms,
  questionDifficulties,
  questionMaxScoreMax,
  questionOptionsMin,
  questionTypes,
} from '../api/questionOptions';
import { splitLines } from '../api/questionValues';
import { countOccurrences, hasRichTextContent } from '../api/richTextContent';
import { addEssayIssues } from './essayRules';
import { addMathStepsIssues } from './mathStepsRules';

const errorKey = (key: string) => `questions:editor.errors.${key}`;

function isFiniteNumber(value: string): boolean {
  return value.trim() !== '' && Number.isFinite(Number(value.trim()));
}

export const questionEditorSchema = z
  .object({
    type: z.enum(questionTypes),
    stem: z.string(),
    explanation: z.string(),
    difficulty: z.enum(questionDifficulties),
    objectiveId: z.string(),
    tags: z.string(),
    maxScore: z.string(),
    options: z.array(z.object({ id: z.string(), text: z.string(), correct: z.boolean() })),
    partialCredit: z.boolean(),
    trueFalseAnswer: z.enum(['', 'true', 'false']),
    blanks: z.array(z.object({ id: z.string(), acceptedAnswers: z.string() })),
    normalization: z.object({
      stripTashkeel: z.boolean(),
      stripTatweel: z.boolean(),
      unifyAlef: z.boolean(),
      unifyTaaMarbuta: z.boolean(),
      unifyAlefMaqsura: z.boolean(),
      convertDigits: z.boolean(),
      collapseWhitespace: z.boolean(),
      foldCase: z.boolean(),
    }),
    answerKind: z.enum(['numeric', 'text']),
    numericValue: z.string(),
    tolerance: z.string(),
    toleranceMode: z.enum(['absolute', 'percent']),
    acceptedAnswers: z.string(),
    maxWords: z.string(),
    criteria: z.array(
      z.object({
        id: z.string(),
        title: z.string(),
        description: z.string(),
        points: z.string(),
        levels: z.array(z.object({ points: z.string(), description: z.string() })),
      }),
    ),
    modelAnswers: z.array(z.object({ text: z.string() })),
    mathAnswers: z.array(z.object({ latex: z.string() })),
    mathForm: z.enum(mathAnswerForms),
    mathTolerance: z.string(),
    mathToleranceMode: z.enum(['absolute', 'percent']),
  })
  .superRefine((values, context) => {
    const issue = (path: (string | number)[], message: string) => {
      context.addIssue({ code: 'custom', path, message });
    };
    if (!hasRichTextContent(values.stem)) {
      issue(['stem'], 'validation.required');
    }
    const maxScore = Number(values.maxScore);
    if (!/^\d+$/.test(values.maxScore) || maxScore < 1 || maxScore > questionMaxScoreMax) {
      issue(['maxScore'], errorKey('maxScore'));
    }
    if (values.type === 'Mcq' || values.type === 'Multi') {
      if (values.options.length < questionOptionsMin) {
        issue(['options'], errorKey('optionsCount'));
      }
      values.options.forEach((option, index) => {
        if (!hasRichTextContent(option.text)) {
          issue(['options', index, 'text'], 'validation.required');
        }
      });
      const correctCount = values.options.filter((option) => option.correct).length;
      if (values.type === 'Mcq' && correctCount !== 1) {
        issue(['options'], errorKey('correctOption'));
      }
      if (values.type === 'Multi' && correctCount === 0) {
        issue(['options'], errorKey('correctOptions'));
      }
    }
    if (values.type === 'TrueFalse' && values.trueFalseAnswer === '') {
      issue(['trueFalseAnswer'], errorKey('trueFalseAnswer'));
    }
    if (values.type === 'Fill') {
      if (values.blanks.length === 0) {
        issue(['blanks'], errorKey('blanksCount'));
      }
      values.blanks.forEach((blank, index) => {
        if (splitLines(blank.acceptedAnswers).length === 0) {
          issue(['blanks', index, 'acceptedAnswers'], errorKey('acceptedAnswers'));
        }
      });
      if (values.blanks.some((blank) => countOccurrences(values.stem, `[[${blank.id}]]`) !== 1)) {
        issue(['stem'], errorKey('blankPlaceholder'));
      }
    }
    if (values.type === 'Short' && values.answerKind === 'numeric') {
      if (!isFiniteNumber(values.numericValue)) {
        issue(['numericValue'], errorKey('number'));
      }
      if (!isFiniteNumber(values.tolerance) || Number(values.tolerance) < 0) {
        issue(['tolerance'], errorKey('tolerance'));
      }
    }
    if (values.type === 'Short' && values.answerKind === 'text' && splitLines(values.acceptedAnswers).length === 0) {
      issue(['acceptedAnswers'], errorKey('acceptedAnswers'));
    }
    if (values.type === 'Essay') {
      addEssayIssues(values, issue);
    }
    if (values.type === 'MathSteps') {
      addMathStepsIssues(values, issue);
    }
  });

export type QuestionValues = z.infer<typeof questionEditorSchema>;
