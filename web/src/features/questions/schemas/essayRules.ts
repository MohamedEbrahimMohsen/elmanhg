import { essayMaxWordsMax, rubricLevelsMin, rubricPointsMax } from '../api/questionOptions';
import { hasRichTextContent } from '../api/richTextContent';

export interface EssayRuleInput {
  maxWords: string;
  criteria: { title: string; points: string; levels: { points: string; description: string }[] }[];
  modelAnswers: { text: string }[];
}

type IssueSink = (path: (string | number)[], message: string) => void;

const errorKey = (key: string) => `questions:editor.errors.${key}`;

const isWhole = (value: string) => /^\d+$/.test(value);

function isInRange(value: string, min: number, max: number): boolean {
  return isWhole(value) && Number(value) >= min && Number(value) <= max;
}

function addCriterionIssues(criterion: EssayRuleInput['criteria'][number], index: number, issue: IssueSink): void {
  if (criterion.title.trim() === '') {
    issue(['criteria', index, 'title'], 'validation.required');
  }
  const pointsValid = isInRange(criterion.points, 1, rubricPointsMax);
  if (!pointsValid) {
    issue(['criteria', index, 'points'], errorKey('rubricPoints'));
  }
  if (criterion.levels.length < rubricLevelsMin) {
    issue(['criteria', index, 'levels'], errorKey('levelsCount'));
  }
  const full = Number(criterion.points);
  criterion.levels.forEach((level, levelIndex) => {
    if (level.description.trim() === '') {
      issue(['criteria', index, 'levels', levelIndex, 'description'], 'validation.required');
    }
    if (!isWhole(level.points) || (pointsValid && Number(level.points) > full)) {
      issue(['criteria', index, 'levels', levelIndex, 'points'], errorKey('levelPoints'));
    }
  });
  const levelsValid = criterion.levels.every((level) => isInRange(level.points, 0, full));
  if (!pointsValid || !levelsValid || criterion.levels.length < rubricLevelsMin) {
    return;
  }
  const points = criterion.levels.map((level) => Number(level.points));
  if (new Set(points).size !== points.length || !points.includes(0) || !points.includes(full)) {
    issue(['criteria', index, 'levels'], errorKey('levelScale'));
  }
}

export function addEssayIssues(values: EssayRuleInput, issue: IssueSink): void {
  if (values.maxWords !== '' && !isInRange(values.maxWords, 1, essayMaxWordsMax)) {
    issue(['maxWords'], errorKey('maxWords'));
  }
  if (values.criteria.length === 0) {
    issue(['criteria'], errorKey('criteriaCount'));
  }
  values.criteria.forEach((criterion, index) => {
    addCriterionIssues(criterion, index, issue);
  });
  if (values.modelAnswers.length === 0) {
    issue(['modelAnswers'], errorKey('modelAnswersCount'));
  }
  values.modelAnswers.forEach((answer, index) => {
    if (!hasRichTextContent(answer.text)) {
      issue(['modelAnswers', index, 'text'], 'validation.required');
    }
  });
}
