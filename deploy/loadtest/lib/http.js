// HTTP helpers: every request carries a `name` tag, which the budgets in config.js key on.
import http from 'k6/http';
import { check, fail } from 'k6';
import { baseUrl, password, subjectName } from './config.js';

const jsonHeaders = { 'Content-Type': 'application/json' };

function params(token, name) {
  return { headers: token ? { ...jsonHeaders, Authorization: `Bearer ${token}` } : jsonHeaders, tags: { name } };
}

function verify(response, name) {
  check(response, { [`${name} 2xx`]: (r) => r.status >= 200 && r.status < 300 });
  return response;
}

const body = (value) => (value === undefined ? null : JSON.stringify(value));

export const get = (path, token, name) => verify(http.get(`${baseUrl}${path}`, params(token, name)), name);

export const post = (path, value, token, name) => verify(http.post(`${baseUrl}${path}`, body(value), params(token, name)), name);

export const put = (path, value, token, name) => verify(http.put(`${baseUrl}${path}`, body(value), params(token, name)), name);

export function login(email) {
  const response = http.post(`${baseUrl}/api/auth/login/email`, JSON.stringify({ email, password }), params(null, 'login'));
  if (!check(response, { 'login 200': (r) => r.status === 200 })) {
    fail(`login failed for ${email}: ${response.status}`);
  }

  const cookie = response.cookies.elmanhg_refresh;
  return { token: response.json('accessToken'), refreshCookie: cookie && cookie.length > 0 ? cookie[0].value : '' };
}

export function discover(token) {
  const interests = get('/api/students/me/subject-interests', token, 'discover').json('subjects');
  const subject = interests.find((x) => x.name === subjectName);
  if (!subject) {
    fail(`subject "${subjectName}" not found; run the seed first`);
  }

  const units = get(`/api/browse/subjects/${subject.subjectId}`, token, 'discover').json('units');
  return {
    subjectId: subject.subjectId,
    units: units.map((unit) => ({
      id: unit.id,
      lessonIds: get(`/api/browse/units/${unit.id}`, token, 'discover')
        .json('lessons')
        .map((lesson) => lesson.id),
    })),
  };
}
