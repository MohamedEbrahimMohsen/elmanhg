// One function per student journey; `ctx` is { token, catalog, iteration } from api-load.js.
import { sleep } from 'k6';
import { get, post, put } from './http.js';

const optionIds = ['a', 'b', 'c', 'd'];
const quizSize = 10;
const multiUnitExamSize = 20;

const pick = (list) => list[Math.floor(Math.random() * list.length)];
const think = (min, max) => sleep(min + Math.random() * (max - min));
const allLessons = (catalog) => catalog.units.flatMap((unit) => unit.lessonIds);

export const avatarBody = (lessonId) => ({
  entryPoint: 'Lesson',
  lessonId,
  sessionId: null,
  questionId: null,
  conversationId: null,
  message: 'اشرح لي فكرة الدرس باختصار.',
});

// Pays the API's first-call cost (JIT, EF query compilation) before the scenarios start. The `warmup` name carries
// no budget: in the smoke profile a quiz VU starts only 2-3 quizzes, so one cold start would be the p95.
export function warmUp({ token, catalog }) {
  const started = post('/api/sessions/quiz', { lessonId: pick(allLessons(catalog)), questionCount: quizSize }, token, 'warmup');
  if (started.status === 200) {
    post(`/api/sessions/${started.json('id')}/finish`, undefined, token, 'warmup');
  }

  post('/api/avatar/messages', avatarBody(pick(allLessons(catalog))), token, 'warmup');
}

export function landing() {
  get('/', null, 'landing_shell');
  get('/api/questions/servable-count', null, 'servable_count');
  get('/api/plans', null, 'plans');
  think(1, 3);
}

export function browse({ token, catalog }) {
  get(`/api/browse/subjects/${catalog.subjectId}`, token, 'browse_subject');
  think(1, 3);
  const unit = pick(catalog.units);
  get(`/api/browse/units/${unit.id}`, token, 'browse_unit');
  think(1, 3);
  get(`/api/browse/lessons/${pick(unit.lessonIds)}`, token, 'browse_lesson');
  think(1, 3);
}

export function quiz({ token, catalog }) {
  const started = post('/api/sessions/quiz', { lessonId: pick(allLessons(catalog)), questionCount: quizSize }, token, 'quiz_start');
  if (started.status !== 200) {
    return;
  }

  const session = started.json();
  const open = session.items.filter((item) => item.attempt === null).sort((a, b) => a.position - b.position);
  for (const item of open) {
    think(1, 3);
    post(`/api/sessions/${session.id}/answers`, { questionId: item.questionId, answer: { optionId: pick(optionIds) }, timeTakenMilliseconds: 2000 }, token, 'quiz_answer');
  }

  post(`/api/sessions/${session.id}/finish`, undefined, token, 'quiz_finish');
}

export function exam({ token, catalog, iteration }) {
  const started = iteration % 2 === 1
    ? post(`/api/exams/units/${pick(catalog.units).id}`, undefined, token, 'exam_start')
    : post(`/api/exams/subjects/${catalog.subjectId}/multi-unit`, { unitIds: catalog.units.map((unit) => unit.id), size: multiUnitExamSize }, token, 'multi_exam_start');
  if (started.status !== 200) {
    return;
  }

  const examId = started.json('id');
  const items = get(`/api/exams/${examId}`, token, 'exam_get').json('items');
  for (const item of items) {
    sleep(1);
    put(`/api/exams/${examId}/answers/${item.questionId}`, { answer: { optionId: pick(optionIds) } }, token, 'exam_save');
  }

  post(`/api/exams/${examId}/submit`, undefined, token, 'exam_submit');
}

export function avatar({ token, catalog }) {
  post('/api/avatar/messages', avatarBody(pick(allLessons(catalog))), token, 'avatar_message');
  think(5, 10);
}
