// k6 load test of the hot API paths through Caddy (docs/performance.md §5). Run by deploy/load-test.sh.
// Each API VU n signs in as load-test student n, so two VUs never share a quiz or an exam.
import exec from 'k6/execution';
import { profile, scenarios, studentEmail, thresholds, vus } from './lib/config.js';
import * as flows from './lib/flows.js';
import { discover, login } from './lib/http.js';

export const options = { scenarios: scenarios(), thresholds: thresholds(), setupTimeout: '180s' };

export function setup() {
  const students = Object.values(vus[profile]).reduce((sum, count) => sum + count, 0);
  const tokens = [];
  for (let index = 1; index <= students; index++) {
    tokens.push(login(studentEmail(index)).token);
  }

  const catalog = discover(tokens[0]);
  flows.warmUp({ token: tokens[0], catalog });
  return { tokens, catalog };
}

const context = (data) => ({ token: data.tokens[exec.vu.idInTest - 1], catalog: data.catalog, iteration: exec.vu.iterationInScenario });

export function landing() {
  flows.landing();
}

export function browse(data) {
  flows.browse(context(data));
}

export function quiz(data) {
  flows.quiz(context(data));
}

export function exam(data) {
  flows.exam(context(data));
}

export function avatar(data) {
  flows.avatar(context(data));
}
