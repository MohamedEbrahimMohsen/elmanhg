// Throttled headless Chromium: cold lesson load and a quiz transition (docs/performance.md §1). Run by deploy/load-test.sh.
// 3G-class network = Lighthouse's mobile preset: 150 ms per request, 1.6 Mbps down, 750 kbps up, no CPU throttling.
import { browser } from 'k6/browser';
import exec from 'k6/execution';
import { check } from 'k6';
import { Trend } from 'k6/metrics';
import { baseUrl, browserStudentOffset, profile, studentEmail } from './lib/config.js';
import { discover, login } from './lib/http.js';

const browserStudents = 10;
const network = { latency: 150, download: 200000, upload: 93750 };
const lessonContentVisible = new Trend('lesson_content_visible', true);
const quizTransition = new Trend('quiz_transition', true);
const lessonBudgetMs = 2000;

export const options = {
  scenarios: {
    lesson: {
      executor: 'shared-iterations',
      vus: 1,
      iterations: profile === 'full' ? 20 : 5,
      maxDuration: '10m',
      options: { browser: { type: 'chromium' } },
    },
  },
  summaryTrendStats: ['avg', 'min', 'med', 'p(75)', 'p(95)', 'max'],
  thresholds: {
    quiz_transition: ['p(95)<300'],
    checks: ['rate>0.99'],
  },
  setupTimeout: '120s',
};

export function setup() {
  return { catalog: discover(login(studentEmail(browserStudentOffset)).token) };
}

export default async function (data) {
  const student = browserStudentOffset + (exec.scenario.iterationInTest % browserStudents);
  const { refreshCookie } = login(studentEmail(student));
  const context = await browser.newContext();
  await context.addCookies([{ name: 'elmanhg_refresh', value: refreshCookie, url: `${baseUrl}/api/auth` }]);
  const page = await context.newPage();
  try {
    await page.throttleNetwork(network);
    const lessonId = data.catalog.units[0].lessonIds[1];

    const lessonStarted = Date.now();
    await page.goto(`${baseUrl}/student/lesson/${lessonId}`);
    await page.locator('.rich-text p').first().waitFor({ state: 'visible' });
    lessonContentVisible.add(Date.now() - lessonStarted);
    check(await page.locator('h1').isVisible(), { 'lesson title visible': (visible) => visible });

    await page.goto(`${baseUrl}/student/lesson/${lessonId}/practice`);
    await page.getByRole('button', { name: '٥ أسئلة' }).click();
    await page.getByRole('radio').first().click();
    await page.getByRole('button', { name: 'تحقّق' }).click();
    const next = page.getByRole('button', { name: 'التالي' });
    await next.waitFor({ state: 'visible' });

    const transitionStarted = Date.now();
    await next.click();
    await page.getByText(/^السؤال ٢ من/).waitFor({ state: 'visible' });
    quizTransition.add(Date.now() - transitionStarted);
  } finally {
    await page.close();
    await context.close();
  }
}

const formatValues = (values) =>
  Object.entries(values)
    .map(([stat, value]) => `${stat}=${Number.isInteger(value) ? value : value.toFixed(2)}`)
    .join(' ');

// The lesson budget is advisory in CI until #220 meets it (docs/performance.md §5): it warns, it does not fail the run.
export function handleSummary(data) {
  const lessonP75 = data.metrics.lesson_content_visible?.values['p(75)'];
  const lines = Object.entries(data.metrics).map(([name, metric]) => `${name}: ${formatValues(metric.values)}`);
  if (lessonP75 === undefined || lessonP75 >= lessonBudgetMs) {
    const measured = lessonP75 === undefined ? 'was not measured against' : `${Math.round(lessonP75)} ms is over`;
    lines.push(`::warning::lesson_content_visible p75 ${measured} the ${lessonBudgetMs} ms budget (advisory until #220)`);
  }

  return { stdout: `${lines.join('\n')}\n`, '/results/browser-summary.json': JSON.stringify(data, null, 2) };
}
