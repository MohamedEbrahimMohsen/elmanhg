// Load-test settings shared by api-load.js and lesson-page.js. The budgets must equal docs/performance.md §2.
export const baseUrl = __ENV.BASE_URL || 'http://web';
export const profile = __ENV.LOAD_PROFILE || 'smoke';
export const key = __ENV.LOAD_KEY || 'loadtest';
export const password = __ENV.LOAD_STUDENT_PASSWORD || '';

// p95 in milliseconds per request name (Decision 4 of the #114 plan).
export const budgets = {
  servable_count: 100,
  landing_shell: 100,
  plans: 150,
  browse_subject: 300,
  browse_unit: 300,
  browse_lesson: 250,
  quiz_start: 500,
  quiz_answer: 250,
  quiz_finish: 500,
  exam_start: 800,
  exam_get: 300,
  exam_save: 250,
  exam_submit: 1000,
  multi_exam_start: 1000,
  avatar_message: 1500,
};

export const vus = {
  smoke: { browse: 2, quiz: 2, exam: 1, avatar: 1, landing: 1 },
  full: { browse: 15, quiz: 20, exam: 5, avatar: 5, landing: 5 },
};

const gracePeriod = '90s';

function scenario(flow, count) {
  if (profile === 'full') {
    return {
      executor: 'ramping-vus',
      exec: flow,
      startVUs: 0,
      stages: [
        { duration: '1m', target: count },
        { duration: '5m', target: count },
        { duration: '30s', target: 0 },
      ],
      gracefulRampDown: gracePeriod,
      gracefulStop: gracePeriod,
    };
  }

  return { executor: 'constant-vus', exec: flow, vus: count, duration: '45s', gracefulStop: gracePeriod };
}

export function scenarios() {
  return Object.fromEntries(Object.entries(vus[profile]).map(([flow, count]) => [flow, scenario(flow, count)]));
}

export function thresholds() {
  const perRequest = Object.entries(budgets).map(([name, p95]) => [`http_req_duration{name:${name}}`, [`p(95)<${p95}`]]);
  return { http_req_failed: ['rate<0.01'], checks: ['rate>0.99'], ...Object.fromEntries(perRequest) };
}

export const studentEmail = (index) => `${key}-student-${String(index).padStart(3, '0')}@loadtest.example.com`;
export const subjectName = `Load test ${key}`;
export const browserStudentOffset = 51;
