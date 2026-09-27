/* Elmanhg wireframe prototype - app.js
   Sections:
   1. Utilities          2. State & persistence   3. Content & business rules
   4. Grading            5. Quiz / exam engines   6. Question rendering component
   7. Shell (header/nav/modal/avatar)             8. Student views
   9. Teacher views      10. Admin views           11. Actions (A.*)
   12. Router & boot */
(function () {
  'use strict';

  /* ================= 1. UTILITIES ================= */
  var H = 3600e3, DAY = 24 * H;
  function esc(s) { return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }
  function clone(o) { return JSON.parse(JSON.stringify(o)); }
  function uid(p) { return (p || 'id') + '-' + Date.now().toString(36) + Math.random().toString(36).slice(2, 6); }
  function pad(n) { return n < 10 ? '0' + n : '' + n; }
  function dayKey(ts) { var d = new Date(ts); return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()); }
  function fmt(ts) { return ts ? new Date(ts).toLocaleString('ar-EG-u-nu-latn', { dateStyle: 'short', timeStyle: 'short' }) : '—'; }
  function pct(x) { return Math.round((x || 0) * 100) + '%'; }
  function shuffle(a) { a = a.slice(); for (var i = a.length - 1; i > 0; i--) { var j = Math.floor(Math.random() * (i + 1)); var t = a[i]; a[i] = a[j]; a[j] = t; } return a; }
  function median(a) { if (!a.length) return 0; a = a.slice().sort(function (x, y) { return x - y; }); var m = Math.floor(a.length / 2); return a.length % 2 ? a[m] : (a[m - 1] + a[m]) / 2; }
  function dur(ms) { var h = ms / H; return h < 48 ? h.toFixed(1) + ' ساعة' : (h / 24).toFixed(1) + ' يوم'; }
  function hashId(s) { var h = 5381; s = String(s); for (var i = 0; i < s.length; i++) h = ((h << 5) + h + s.charCodeAt(i)) >>> 0; return 'anon_' + h.toString(16); }
  function byOrder(a, b) { return a.order - b.order; }
  function last(a) { return a && a.length ? a[a.length - 1] : null; }
  function $(id) { return document.getElementById(id); }

  var TYPE = { mcq: 'اختيار من متعدد', multi: 'اختيار متعدد الإجابات', tf: 'صح / خطأ', fill: 'أكمل الفراغ', short: 'إجابة قصيرة', essay: 'مقالي', math_steps: 'رياضيات بالخطوات' };
  var TYPES = Object.keys(TYPE), V2 = ['essay', 'math_steps'];
  var DIFF = { easy: 'سهل', medium: 'متوسط', hard: 'صعب' };
  var QST = { pending: 'قيد المراجعة', approved: 'معتمد', rejected: 'مرفوض' };
  var LST = { draft: 'مسودة', published: 'منشور', archived: 'مؤرشف' };
  var ROLE = { student: 'طالب', teacher: 'معلّم', admin: 'مدير' };
  var PLAN = { free: 'مجاني', base: 'الأساسية', ask: 'اسأل معلّم' };
  var PRICES = { base: 199, ask: 99 };
  var MT = 0.8, FREE_DAILY = 10, FREE_AVATAR = 5, ASK_QUOTA = 20;

  /* ================= 2. STATE & PERSISTENCE ================= */
  var KEY = 'elmanhg.v1', S = null;
  function load() {
    try { S = JSON.parse(localStorage.getItem(KEY)); } catch (e) { S = null; }
    if (!S || S.schema !== window.SEED.schema) { S = clone(window.SEED); save(); }
  }
  function save() { try { localStorage.setItem(KEY, JSON.stringify(S)); } catch (e) { /* storage unavailable */ } }
  function find(coll, id) { var a = S[coll]; for (var i = 0; i < a.length; i++) if (a[i].id == id) return a[i]; return null; }
  function role() { return S.ui.role; }
  function me() { return find('users', S.ui.users[S.ui.role]); }
  function uname(id) { var u = find('users', id); return u ? u.name : (id || '—'); }
  function audit(action, entity, entityId, diff, actor) {
    S.audit.push({ id: uid('au'), actorId: actor || (me() ? me().id : 'system'), action: action, entity: entity, entityId: entityId, diff: diff || null, at: Date.now() });
  }

  /* ================= 3. CONTENT & BUSINESS RULES ================= */
  function subjectUnits(sid) { return S.units.filter(function (u) { return u.subjectId == sid; }).sort(byOrder); }
  function unitLessons(uId) { return S.lessons.filter(function (l) { return l.unitId == uId; }).sort(byOrder); }
  function pubLessons(uId) { return unitLessons(uId).filter(function (l) { return l.state === 'published'; }); }
  function lessonOf(q) { return find('lessons', q.lessonId); }
  function objText(l, oid) { var o = l && l.objectives.filter(function (x) { return x.id === oid; })[0]; return o ? o.text : '—'; }

  // Rule 1: servable is DERIVED, never stored.
  function servable(q) { var l = lessonOf(q); return q.validationStatus === 'approved' && !!l && l.state === 'published' && !q.retiredAt; }
  function servableWhere(pred) { return S.questions.filter(function (q) { return servable(q) && (!pred || pred(q)); }); }
  function inLesson(lid) { return function (q) { return q.lessonId == lid; }; }
  function inUnits(ids) { return function (q) { var l = lessonOf(q); return !!l && ids.some(function (i) { return i == l.unitId; }); }; }
  function inSubject(sid) { return function (q) { return q.subjectId == sid; }; }

  // Attempts & mastery (Rule 5)
  function attMap(sid) {
    var m = {};
    S.attempts.forEach(function (a) { if (a.studentId === sid) (m[a.questionId] = m[a.questionId] || []).push(a); });
    Object.keys(m).forEach(function (k) { m[k].sort(function (x, y) { return x.at - y.at; }); });
    return m;
  }
  function isMastered(m, qid) { var a = m[qid]; return !!a && a.length >= 2 && a[a.length - 1].normalised >= MT && a[a.length - 2].normalised >= MT; }
  function mastery(m, qs) { if (!qs.length) return 0; return qs.filter(function (q) { return isMastered(m, q.id); }).length / qs.length; }
  function headline(sid) { // Rule 6
    var m = attMap(sid), all = servableWhere();
    var mastered = all.filter(function (q) { return isMastered(m, q.id); }).length;
    return { Y: all.length, X: all.length - mastered, seen: all.filter(function (q) { return m[q.id]; }).length, mastered: mastered };
  }
  function streak(sid) {
    var days = {}; S.attempts.forEach(function (a) { if (a.studentId === sid && a.kind === 'quiz') days[dayKey(a.at)] = 1; });
    var t = Date.now(), n = 0; if (!days[dayKey(t)]) t -= DAY;
    while (days[dayKey(t)]) { n++; t -= DAY; }
    return n;
  }

  // Plans / entitlement
  function activeSubs(sid) { return S.subscriptions.filter(function (s) { return s.studentId === sid && s.status === 'active'; }); }
  function hasPlan(sid, p) { return activeSubs(sid).some(function (s) { return s.plan === p; }); }
  function isFree(sid) { return !hasPlan(sid, 'base'); }
  function planLabel(sid) { return isFree(sid) ? PLAN.free : PLAN.base + (hasPlan(sid, 'ask') ? ' + ' + PLAN.ask : ''); }
  function quizUsedToday(sid) { var k = dayKey(Date.now()); return S.attempts.filter(function (a) { return a.studentId === sid && a.kind === 'quiz' && dayKey(a.at) === k; }).length; }
  function lessonLocked(sid, l) { if (!isFree(sid)) return false; var first = pubLessons(l.unitId)[0]; return !first || first.id !== l.id; }
  function avatarUsedToday(sid) { var k = dayKey(Date.now()), n = 0; S.avatarConvs.forEach(function (c) { if (c.studentId === sid) c.messages.forEach(function (m) { if (m.role === 'user' && dayKey(m.at) === k) n++; }); }); return n; }

  function blueprintForUnit(u) { return find('blueprints', u.blueprintId) || find('blueprints', find('subjects', u.subjectId).defaultBlueprintId); }
  function shortfall(pool, counts) {
    var out = [];
    Object.keys(counts).forEach(function (t) { var need = +counts[t] || 0, have = pool.filter(function (q) { return q.type === t; }).length; if (need > have) out.push({ type: t, need: need, have: have }); });
    return out;
  }
  function shortfallMsg(sf) { return sf.map(function (s) { return TYPE[s.type] + ': مطلوب ' + s.need + '، متاح ' + s.have + ' (ينقص ' + (s.need - s.have) + ')'; }).join('<br>'); }
  function mergeBlueprints(unitIds, size) { // Rule 7: proportional merge
    var sum = {};
    unitIds.forEach(function (id) { var bp = blueprintForUnit(find('units', id)); Object.keys(bp.typeCounts).forEach(function (t) { sum[t] = (sum[t] || 0) + (+bp.typeCounts[t] || 0); }); });
    var total = Object.keys(sum).reduce(function (a, t) { return a + sum[t]; }, 0), out = {}, rem = [], used = 0;
    Object.keys(sum).forEach(function (t) { var exact = sum[t] * size / total; out[t] = Math.floor(exact); used += out[t]; rem.push({ t: t, r: exact - out[t] }); });
    rem.sort(function (a, b) { return b.r - a.r; });
    for (var i = 0; used < size && i < rem.length; i++, used++) out[rem[i].t]++;
    return out;
  }
  function threadAwaiting(th) { return th.status !== 'closed' && last(th.messages).senderId === th.studentId; }
  function threadBreached(th) { return threadAwaiting(th) && Date.now() > th.slaDueAt; }
  function teacherSubjects(u) { return (u && u.subjects) || []; }
  function teacherCan(u, subjectId) { return teacherSubjects(u).some(function (s) { return s == subjectId; }); }
  function activeExam(sid) { return S.sessions.filter(function (s) { return s.studentId === sid && s.kind !== 'quiz' && !s.submittedAt; })[0]; }

  /* ================= 4. GRADING (PRD §6) ================= */
  function normAr(s) {
    s = String(s == null ? '' : s);
    s = s.replace(/[٠-٩]/g, function (c) { return String(c.charCodeAt(0) - 0x660); })
      .replace(/[۰-۹]/g, function (c) { return String(c.charCodeAt(0) - 0x6F0); })
      .replace(/٫/g, '.').replace(/−/g, '-');
    s = s.replace(/[ً-ٰٟۖ-ۭ]/g, '').replace(/ـ/g, '');
    s = s.replace(/[أإآٱ]/g, 'ا').replace(/ة/g, 'ه').replace(/ى/g, 'ي');
    return s.toLowerCase().replace(/\s+/g, ' ').trim();
  }
  function matchAny(ans, list) {
    var a = normAr(ans); if (!a) return false;
    return list.some(function (x) { var n = normAr(x); return n === a || n.replace(/ /g, '') === a.replace(/ /g, ''); });
  }
  function parseNum(s) { var t = normAr(s).replace(/,/g, '.').replace(/[^\d.\-]/g, ''); return t === '' || t === '-' ? NaN : parseFloat(t); }

  function grade(q, ans) {
    var sp = q.gradingSpec, n = 0, fb = '';
    switch (q.type) {
      case 'mcq': n = ans != null && +ans === sp.correct ? 1 : 0; break;
      case 'tf': n = ans === sp.correct ? 1 : 0; break;
      case 'multi':
        var sel = ans || [], c = 0, w = 0;
        sel.forEach(function (i) { if (sp.correct.indexOf(i) >= 0) c++; else w++; });
        n = Math.max(0, (c - w) / sp.correct.length);
        if (n > 0 && n < 1) fb = 'درجة جزئية: ' + c + ' اختيار صحيح و ' + w + ' خطأ.';
        break;
      case 'fill':
        var arr = ans || [], hit = 0;
        sp.accepted.forEach(function (acc, i) { if (matchAny(arr[i], acc)) hit++; });
        n = hit / sp.accepted.length; if (sp.accepted.length > 1) fb = 'فراغات صحيحة: ' + hit + ' من ' + sp.accepted.length;
        break;
      case 'short':
        if (sp.kind === 'numeric') {
          var v = parseNum(ans), tol = sp.tolType === 'pct' ? Math.abs(sp.value) * sp.tol / 100 : +sp.tol || 0;
          n = !isNaN(v) && Math.abs(v - sp.value) <= tol + 1e-9 ? 1 : 0;
        } else n = matchAny(ans, sp.accepted) ? 1 : 0;
        break;
      case 'essay': // v2 simulated: keyword overlap heuristic
        var t = normAr(ans), words = t ? t.split(' ').length : 0;
        var found = sp.keywords.filter(function (k) { return t.indexOf(normAr(k)) >= 0; });
        var missing = sp.keywords.filter(function (k) { return found.indexOf(k) < 0; });
        n = found.length / sp.keywords.length; if (words < 8) n = n * 0.5;
        fb = 'تبرير الذكاء الاصطناعي (محاكاة): ذكرت ' + found.length + ' من ' + sp.keywords.length + ' عناصر أساسية' +
          (found.length ? ' (' + found.join('، ') + ')' : '') + '.' + (missing.length ? ' ينقص الإجابة: ' + missing.join('، ') + '.' : '') +
          (words < 8 ? ' الإجابة قصيرة جدًا فخُصم نصف الدرجة.' : '') + ' الثقة: ' + (0.6 + n * 0.3).toFixed(2);
        break;
      case 'math_steps': // v2 simulated: final answer only
        var f = normAr(ans && ans.final).replace(/\s/g, '');
        n = f && sp.finalAnswers.some(function (x) { return normAr(x).replace(/\s/g, '') === f; }) ? 1 : 0;
        fb = 'تم تصحيح الإجابة النهائية فقط. تصحيح الخطوات في الإصدار 2';
        break;
    }
    n = Math.round(n * 100) / 100;
    return { score: Math.round(n * q.maxScore * 100) / 100, normalised: n, feedback: fb };
  }
  function correctText(q) {
    var sp = q.gradingSpec, b = q.body;
    switch (q.type) {
      case 'mcq': return b.options[sp.correct];
      case 'multi': return sp.correct.map(function (i) { return b.options[i]; }).join(' + ');
      case 'tf': return sp.correct ? 'صح' : 'خطأ';
      case 'fill': return sp.accepted.map(function (a) { return a[0]; }).join(' ، ');
      case 'short': return sp.kind === 'numeric' ? sp.value + ' ' + (b.unit || '') + (sp.tol ? ' (± ' + sp.tol + (sp.tolType === 'pct' ? '%' : '') + ')' : '') : sp.accepted[0];
      case 'essay': return 'إجابة نموذجية: ' + sp.modelAnswer;
      case 'math_steps': return sp.finalAnswers[0];
    }
    return '';
  }
  function answerText(q, a) {
    if (a == null || a === '') return '— (بدون إجابة)';
    switch (q.type) {
      case 'mcq': return q.body.options[a] || '—';
      case 'multi': return a.length ? a.map(function (i) { return q.body.options[i]; }).join(' + ') : '—';
      case 'tf': return a ? 'صح' : 'خطأ';
      case 'fill': return a.join(' ، ');
      case 'math_steps': return (a.steps || []).join(' ← ') + ' | النهائي: ' + (a.final || '—');
    }
    return String(a);
  }

  /* ================= 5. QUIZ / EXAM ENGINES ================= */
  // Rule 4: unseen -> last wrong -> correct exactly once -> rest (least-recent first). Random within buckets 1-3.
  function selectQuiz(sid, pool, n) {
    var m = attMap(sid), b1 = [], b2 = [], b3 = [], b4 = [];
    pool.forEach(function (q) {
      var a = m[q.id];
      if (!a) b1.push(q);
      else if (last(a).normalised < MT) b2.push(q);
      else if (a.filter(function (x) { return x.normalised >= MT; }).length === 1) b3.push(q);
      else b4.push(q);
    });
    b4.sort(function (x, y) { return last(m[x.id]).at - last(m[y.id]).at; });
    return shuffle(b1).concat(shuffle(b2), shuffle(b3), b4).slice(0, n); // slice => no repeats in a session
  }
  function genExam(sid, pool, counts) { // prefers not-yet-mastered, then random
    var m = attMap(sid), picked = [], sf = shortfall(pool, counts);
    if (sf.length) return { questions: [], shortfall: sf };
    Object.keys(counts).forEach(function (t) {
      var c = pool.filter(function (q) { return q.type === t; });
      var nm = shuffle(c.filter(function (q) { return !isMastered(m, q.id); })), ms = shuffle(c.filter(function (q) { return isMastered(m, q.id); }));
      picked = picked.concat(nm.concat(ms).slice(0, +counts[t] || 0));
    });
    return { questions: picked, shortfall: [] };
  }
  function recordAttempt(ses, q, ans, g, timeMs) {
    S.attempts.push({ id: uid('att'), sessionId: ses.id, kind: ses.kind, studentId: ses.studentId, questionId: q.id, questionVersion: q.version,
      answer: ans, score: g.score, normalised: g.normalised, gradedBy: V2.indexOf(q.type) >= 0 ? 'ai' : 'auto', timeMs: timeMs || 0, at: Date.now() });
  }
  function sessionScore(ses) {
    var got = 0, tot = 0;
    ses.questionIds.forEach(function (id) { var q = find('questions', id), r = ses.results[id]; tot += q.maxScore; if (r) got += r.score; });
    return tot ? Math.round(got / tot * 100) : 0;
  }
  function submitExam(ses) {
    ses.questionIds.forEach(function (id) {
      var q = find('questions', id), a = ses.answers[id], g = grade(q, a == null ? null : a);
      ses.results[id] = { answer: a == null ? null : a, score: g.score, normalised: g.normalised, feedback: g.feedback };
      recordAttempt(ses, q, a == null ? null : a, g, 0);
    });
    ses.submittedAt = Date.now(); ses.scorePct = sessionScore(ses); save();
  }

  /* ================= 6. QUESTION RENDERING COMPONENT ================= */
  function v2badge(q) { return V2.indexOf(q.type) >= 0 ? ' <span class="badge v2">v2</span>' : ''; }
  function qMeta(q) { return '<span class="badge">' + TYPE[q.type] + '</span>' + v2badge(q) + ' <span class="badge">' + DIFF[q.difficulty] + '</span>'; }
  // Renders stem + inputs inside a container whose id is pfx-qid. oninputJs is attached to the container.
  function qBlock(q, ans, dis, pfx, oninputJs) {
    var nm = pfx + '-' + q.id, d = dis ? ' disabled' : '', h = '';
    var stem = '<div class="stem">' + esc(q.stem) + '</div>';
    if (q.type === 'mcq' || q.type === 'multi') {
      var multi = q.type === 'multi';
      h = q.body.options.map(function (o, i) {
        var ck = multi ? (ans || []).indexOf(i) >= 0 : ans === i;
        return '<label class="opt"><input type="' + (multi ? 'checkbox' : 'radio') + '" name="' + nm + '" value="' + i + '"' + (ck ? ' checked' : '') + d + '> ' + esc(o) + '</label>';
      }).join('');
    } else if (q.type === 'tf') {
      h = [true, false].map(function (v) { return '<label class="opt"><input type="radio" name="' + nm + '" value="' + v + '"' + (ans === v ? ' checked' : '') + d + '> ' + (v ? 'صح' : 'خطأ') + '</label>'; }).join('');
    } else if (q.type === 'fill') {
      var parts = q.stem.split(/_{3,}/);
      stem = '<div class="stem">' + parts.map(function (p, i) {
        return esc(p) + (i < parts.length - 1 ? '<input class="blank" size="10" value="' + esc((ans || [])[i] || '') + '"' + d + ' aria-label="فراغ ' + (i + 1) + '">' : '');
      }).join('') + '</div>';
    } else if (q.type === 'short') {
      h = '<input class="short" placeholder="اكتب إجابتك' + (q.body.unit ? ' (' + esc(q.body.unit) + ')' : '') + '" value="' + esc(ans || '') + '"' + d + '>';
    } else if (q.type === 'essay') {
      h = '<textarea class="essay" rows="5" placeholder="اكتب إجابتك المقالية"' + d + '>' + esc(ans || '') + '</textarea>';
    } else if (q.type === 'math_steps') {
      h = '<label class="small">الخطوات (خطوة في كل سطر)</label><textarea class="steps" rows="4"' + d + '>' + esc(ans ? (ans.steps || []).join('\n') : '') + '</textarea>' +
        '<label class="small">الإجابة النهائية</label><input class="final" value="' + esc(ans ? ans.final || '' : '') + '"' + d + '>';
    }
    return '<div class="qbox" id="' + nm + '"' + (oninputJs ? ' oninput="' + oninputJs + '" onchange="' + oninputJs + '"' : '') + '>' + stem + '<div class="inputs">' + h + '</div></div>';
  }
  function readAns(q, pfx) {
    var r = $(pfx + '-' + q.id); if (!r) return null;
    switch (q.type) {
      case 'mcq': var c = r.querySelector('input:checked'); return c ? +c.value : null;
      case 'multi': return Array.prototype.map.call(r.querySelectorAll('input:checked'), function (x) { return +x.value; });
      case 'tf': var t = r.querySelector('input:checked'); return t ? t.value === 'true' : null;
      case 'fill': return Array.prototype.map.call(r.querySelectorAll('input.blank'), function (x) { return x.value; });
      case 'short': return r.querySelector('input.short').value;
      case 'essay': return r.querySelector('textarea').value;
      case 'math_steps': return { steps: r.querySelector('textarea.steps').value.split('\n').filter(function (s) { return s.trim(); }), final: r.querySelector('input.final').value };
    }
    return null;
  }
  function isEmpty(q, a) {
    if (a == null) return true;
    if (q.type === 'multi') return !a.length;
    if (q.type === 'fill') return !a.some(function (x) { return x.trim(); });
    if (q.type === 'math_steps') return !a.final.trim();
    if (typeof a === 'string') return !a.trim();
    return false;
  }
  function feedbackBox(q, r) {
    var ok = r.normalised >= MT, partial = !ok && r.normalised > 0;
    return '<div class="fb ' + (ok ? 'ok' : partial ? 'part' : 'bad') + '"><b>' + (ok ? 'إجابة صحيحة' : partial ? 'إجابة صحيحة جزئيًا' : 'إجابة خاطئة') + '</b> — الدرجة ' + r.score + ' / ' + q.maxScore +
      '<div>الإجابة الصحيحة: <b>' + esc(correctText(q)) + '</b></div><div>الشرح: ' + esc(q.explanation) + '</div>' +
      (r.feedback ? '<div class="small">' + esc(r.feedback) + '</div>' : '') + '</div>';
  }

  /* ================= 7. SHELL: header, nav, modal, avatar ================= */
  var NAV = {
    student: [['#/student', 'الرئيسية'], ['#/student/progress', 'تقدّمي'], ['#/student/multi-exam', 'امتحان متعدد الوحدات'], ['#/student/ask', 'اسأل معلّم'], ['#/student/subscription', 'الاشتراك']],
    teacher: [['#/teacher', 'قائمة المراجعة'], ['#/teacher/inbox', 'أسئلة الطلاب'], ['#/teacher/stats', 'إحصائياتي']],
    admin: [['#/admin', 'لوحة المؤشرات'], ['#/admin/content', 'المحتوى'], ['#/admin/questions', 'الأسئلة'], ['#/admin/blueprints', 'نماذج الامتحانات'], ['#/admin/users', 'المستخدمون'], ['#/admin/audit', 'سجل التدقيق'], ['#/admin/export', 'تصدير البيانات']]
  };
  function header() {
    var r = role(), u = me();
    var roleSel = Object.keys(ROLE).map(function (k) { return '<option value="' + k + '"' + (k === r ? ' selected' : '') + '>' + ROLE[k] + '</option>'; }).join('');
    var userSel = S.users.filter(function (x) { return x.role === r; }).map(function (x) {
      var extra = x.role === 'teacher' ? ' (' + teacherSubjects(x).map(function (s) { return find('subjects', s).name; }).join('، ') + ')' : '';
      return '<option value="' + x.id + '"' + (u && x.id === u.id ? ' selected' : '') + '>' + esc(x.name + extra) + '</option>';
    }).join('');
    var h = location.hash || '';
    var nav = NAV[r].map(function (n) {
      var active = h === n[0] || (n[0] !== '#/' + r && h.indexOf(n[0]) === 0);
      return '<a href="' + n[0] + '" class="' + (active ? 'active' : '') + '">' + n[1] + '</a>';
    }).join('');
    return '<header class="top"><a class="brand" href="#/' + r + '">المنهج</a><span class="badge role">' + ROLE[r] + '</span>' +
      '<span class="spacer"></span><label class="small">الدور <select onchange="A.setRole(this.value)">' + roleSel + '</select></label>' +
      '<label class="small">المستخدم <select onchange="A.setUser(this.value)">' + userSel + '</select></label>' +
      '<button class="btn sm" onclick="A.reset()">إعادة ضبط البيانات</button></header><nav class="tabs">' + nav + '</nav>';
  }
  function modal(html) { $('modal').innerHTML = '<div class="overlay" onclick="if(event.target===this)A.closeModal()"><div class="dialog">' + html + '</div></div>'; }
  function crumbs(list) { return '<div class="crumbs">' + list.map(function (c) { return c[1] ? '<a href="' + c[1] + '">' + esc(c[0]) + '</a>' : esc(c[0]); }).join(' / ') + '</div>'; }
  function bar(v, max, label) { var w = max ? Math.round(v / max * 100) : 0; return '<div class="barrow"><span class="bl">' + esc(label) + '</span><span class="bt"><span class="bf" style="width:' + w + '%"></span></span><span class="bv">' + v + '</span></div>'; }
  function progressBar(p) { return '<span class="pbar"><span style="width:' + Math.round(p * 100) + '%"></span></span> ' + pct(p); }

  // ---- Avatar (Rule 12)
  function avatarPanel() {
    var av = S.ui.avatar; if (!av.open || role() !== 'student') return '';
    var conv = find('avatarConvs', av.convId), sid = me().id;
    var msgs = conv ? conv.messages.map(function (m) { return '<div class="msg ' + m.role + '">' + esc(m.text).replace(/\n/g, '<br>') + '</div>'; }).join('') : '';
    var ctx = conv && conv.context.lessonId ? find('lessons', conv.context.lessonId) : null;
    var pick = '';
    if (!ctx) pick = '<div class="small">اختر درسًا: ' + S.lessons.filter(function (l) { return l.state === 'published'; }).map(function (l) { return '<button class="btn sm" onclick="A.avatarLesson(' + l.id + ')">' + esc(l.name) + '</button>'; }).join(' ') + '</div>';
    return '<aside class="avatar"><div class="avhead"><b>المساعد الذكي (محاكاة)</b><button class="btn sm" onclick="A.avatarClose()">إغلاق</button></div>' +
      '<div class="small">السياق: ' + (ctx ? esc(ctx.name) : 'عام') + (conv && conv.context.questionId ? ' — سؤال #' + conv.context.questionId : '') + (conv && conv.context.exam ? ' — امتحان جارٍ' : '') + '</div>' +
      (isFree(sid) ? '<div class="small">رسائل اليوم: ' + avatarUsedToday(sid) + ' / ' + FREE_AVATAR + '</div>' : '') +
      pick + '<div class="avmsgs" id="avmsgs">' + msgs + '</div>' +
      '<div class="row"><input id="avin" placeholder="اكتب سؤالك..." onkeydown="if(event.key===\'Enter\')A.avatarSend()"><button class="btn" onclick="A.avatarSend()">إرسال</button></div>' +
      '<div class="small">نموذج: ' + (conv ? conv.model + ' / ' + conv.promptVersion : '—') + '</div></aside>';
  }
  var GENERIC = ['اشرح', 'لماذا', 'كيف', 'مثال', 'خطا', 'الاجابه', 'ساعدني', 'افهم', 'الدرس', 'السؤال', 'ما', 'هل'];
  function tokens(s) { return normAr(s).replace(/[^؀-ۿa-z0-9 ]/g, ' ').split(' ').filter(function (w) { return w.length > 2; }); }
  function avatarReply(conv, text) {
    var sid = conv.studentId;
    if (activeExam(sid)) return 'لا يمكنني إظهار الإجابة أثناء الامتحان. أكمل امتحانك وسلّمه، وبعدها أشرح لك كل سؤال بالتفصيل.';
    if (isFree(sid) && avatarUsedToday(sid) > FREE_AVATAR) return 'وصلت للحد اليومي للباقة المجانية (' + FREE_AVATAR + ' رسائل). اشترك في الباقة الأساسية لحد أعلى.';
    var l = conv.context.lessonId ? find('lessons', conv.context.lessonId) : null;
    if (!l) return 'أنا مساعد المنهج. اختر درسًا من الأزرار بالأعلى حتى أساعدك في محتواه.';
    var q = conv.context.questionId ? find('questions', conv.context.questionId) : null;
    var corpus = [{ sec: 'الشرح', text: l.explanation }, { sec: 'الملخص', text: l.summary }].concat(l.objectives.map(function (o) { return { sec: 'الأهداف', text: o.text }; }));
    var tk = text ? tokens(text) : [];
    if (text && q == null) {
      var overlapAll = tk.filter(function (w) { return corpus.some(function (c) { return normAr(c.text).indexOf(w) >= 0; }) || GENERIC.indexOf(w) >= 0; });
      if (tk.length && !overlapAll.length) return 'عذرًا، أستطيع المساعدة فقط في محتوى المنهج. سؤالك خارج درس "' + l.name + '". جرّب أن تسألني عن: ' + l.objectives[0].text + '.';
    }
    if (q) {
      var ans = conv.context.answer, lines = [];
      if (conv.context.hasAnswer) lines.push('إجابتك كانت: ' + answerText(q, ans) + '.');
      lines.push('خطوة 1: الإجابة الصحيحة هي: ' + correctText(q) + '.');
      lines.push('خطوة 2: لماذا؟ ' + q.explanation);
      lines.push('خطوة 3: هذا السؤال يقيس الهدف: "' + objText(l, q.objectiveId) + '".');
      lines.push('للمراجعة من الملخص: ' + l.summary);
      lines.push('(المصدر: شرح السؤال + أهداف وملخص درس "' + l.name + '")');
      return lines.join('\n');
    }
    // Retrieval-ish: pick the sentence with max token overlap
    var sentences = [];
    corpus.forEach(function (c) { c.text.split(/(?<=[.،؟!])\s+/).forEach(function (s) { if (s.trim()) sentences.push({ sec: c.sec, s: s.trim() }); }); });
    sentences.forEach(function (x) { var n = normAr(x.s); x.score = tk.filter(function (w) { return n.indexOf(w) >= 0; }).length; });
    sentences.sort(function (a, b) { return b.score - a.score; });
    var best = sentences[0], second = sentences.filter(function (x) { return x !== best && x.sec !== best.sec; })[0];
    return 'خطوة 1: ' + best.s + '\nخطوة 2: ' + (second ? second.s : l.summary) + '\n(المصدر: ' + best.sec + ' - ' + l.name + ')';
  }

  /* ================= 8. STUDENT VIEWS ================= */
  function studentGuard() { var u = me(); if (u.status === 'suspended') return '<div class="card warn">هذا الحساب موقوف من الإدارة. تواصل مع الدعم.</div>'; return ''; }

  function vStudentHome() {
    var u = me(), sid = u.id, m = attMap(sid), hl = headline(sid), ex = activeExam(sid);
    var h = '<h2>أهلًا ' + esc(u.name) + '</h2>';
    if (ex) h += '<div class="card warn">لديك امتحان جارٍ. <a href="#/student/exam/' + ex.id + '">استكمل الامتحان</a></div>';
    h += '<div class="card headline"><div class="big">متبقّي لك ' + hl.X + ' سؤال من ' + hl.Y + '</div><div class="small">شاهدت ' + hl.seen + ' سؤالًا · أتقنت ' + hl.mastered + ' · سلسلة الأيام: ' + streak(sid) + ' يوم</div></div>';
    h += '<div class="card row"><span>باقتك: <b>' + planLabel(sid) + '</b></span>' + (isFree(sid) ? '<span>أسئلة التدريب اليوم: ' + quizUsedToday(sid) + ' / ' + FREE_DAILY + '</span><a class="btn sm" href="#/student/subscription">اشترك</a>' : '') + '</div>';
    // next recommended lesson: lowest mastery among accessible published lessons (in order)
    var cands = [];
    S.subjects.slice().sort(byOrder).forEach(function (s) { subjectUnits(s.id).forEach(function (un) { pubLessons(un.id).forEach(function (l) { if (!lessonLocked(sid, l)) { var qs = servableWhere(inLesson(l.id)); if (qs.length) cands.push({ l: l, p: mastery(m, qs) }); } }); }); });
    var next = cands.filter(function (c) { return c.p < 1; }).sort(function (a, b) { return a.p - b.p; })[0];
    if (next) h += '<div class="card"><div class="small">الدرس التالي المقترح</div><b>' + esc(next.l.name) + '</b> — إتقان ' + pct(next.p) + ' <a class="btn sm" href="#/student/lesson/' + next.l.id + '/practice">تدرّب الآن</a></div>';
    h += '<h3>موادك</h3><div class="grid">';
    S.subjects.slice().sort(byOrder).forEach(function (s) {
      var qs = servableWhere(inSubject(s.id));
      h += '<a class="card subj" href="#/student/subject/' + s.id + '"><b>' + esc(s.name) + '</b><div>' + progressBar(mastery(m, qs)) + '</div><div class="small">' + qs.length + ' سؤال متاح</div></a>';
    });
    return h + '</div>';
  }

  function bestExam(sid, unitId) {
    var ss = S.sessions.filter(function (s) { return s.studentId === sid && s.kind === 'unit' && s.submittedAt && s.unitIds[0] == unitId; });
    return ss.length ? Math.max.apply(null, ss.map(function (s) { return s.scorePct; })) : null;
  }
  function vSubject(p) {
    var s = find('subjects', p.id); if (!s) return notFound();
    var sid = me().id, m = attMap(sid);
    var h = crumbs([['الرئيسية', '#/student'], [s.name]]) + '<h2>' + esc(s.name) + '</h2><div class="list">';
    subjectUnits(s.id).forEach(function (u) {
      var qs = servableWhere(inUnits([u.id])), b = bestExam(sid, u.id);
      h += '<div class="item"><a href="#/student/unit/' + u.id + '"><b>' + esc(u.name) + '</b></a><div>' + progressBar(mastery(m, qs)) + '</div>' +
        '<div class="small">أفضل درجة امتحان: ' + (b == null ? '—' : b + '/100') + '</div><a class="btn sm" href="#/student/exam-start/' + u.id + '">امتحان الوحدة</a></div>';
    });
    return h + '</div><a class="btn" href="#/student/multi-exam/' + s.id + '">امتحان متعدد الوحدات</a>';
  }
  function vUnit(p) {
    var u = find('units', p.id); if (!u) return notFound();
    var s = find('subjects', u.subjectId), sid = me().id, m = attMap(sid);
    var h = crumbs([['الرئيسية', '#/student'], [s.name, '#/student/subject/' + s.id], [u.name]]) + '<h2>' + esc(u.name) + '</h2><div class="list">';
    pubLessons(u.id).forEach(function (l) {
      var qs = servableWhere(inLesson(l.id)), locked = lessonLocked(sid, l);
      h += '<div class="item' + (locked ? ' locked' : '') + '">' + (locked ? '<b>' + esc(l.name) + '</b> <span class="badge">مقفل - للمشتركين</span>' : '<a href="#/student/lesson/' + l.id + '"><b>' + esc(l.name) + '</b></a>') +
        '<div>' + progressBar(mastery(m, qs)) + ' <span class="small">(' + qs.length + ' سؤال)</span></div>' + (locked ? '<a class="btn sm" href="#/student/subscription">اشترك لفتح الدرس</a>' : '') + '</div>';
    });
    return h + '</div><a class="btn" href="#/student/exam-start/' + u.id + '">امتحان الوحدة</a>';
  }
  function vLesson(p) {
    var l = find('lessons', p.id); if (!l || l.state !== 'published') return notFound();
    var sid = me().id, u = find('units', l.unitId), s = find('subjects', l.subjectId), tab = p.tab || 'explain';
    if (lessonLocked(sid, l)) return crumbs([['الرئيسية', '#/student'], [u.name, '#/student/unit/' + u.id], [l.name]]) + '<div class="card warn">هذا الدرس متاح للمشتركين فقط. الباقة المجانية تتيح الدرس الأول من كل وحدة. <a class="btn sm" href="#/student/subscription">اشترك</a></div>';
    var tabs = [['explain', 'الشرح'], ['objectives', 'الأهداف'], ['summary', 'الملخص'], ['practice', 'التدريب']];
    var h = crumbs([['الرئيسية', '#/student'], [s.name, '#/student/subject/' + s.id], [u.name, '#/student/unit/' + u.id], [l.name]]) + '<h2>' + esc(l.name) + '</h2>';
    h += '<div class="subtabs">' + tabs.map(function (t) { return '<a href="#/student/lesson/' + l.id + '/' + t[0] + '" class="' + (t[0] === tab ? 'active' : '') + '">' + t[1] + '</a>'; }).join('') + '</div><div class="card">';
    if (tab === 'explain') h += l.explanation.split('\n\n').map(function (x) { return '<p>' + esc(x) + '</p>'; }).join('');
    else if (tab === 'objectives') h += '<ol>' + l.objectives.map(function (o) { return '<li>' + esc(o.text) + '</li>'; }).join('') + '</ol>';
    else if (tab === 'summary') h += '<p>' + esc(l.summary) + '</p>';
    else {
      var qs = servableWhere(inLesson(l.id)), m = attMap(sid);
      h += '<p>أسئلة متاحة: ' + qs.length + ' · إتقانك: ' + pct(mastery(m, qs)) + ' · شاهدت: ' + qs.filter(function (q) { return m[q.id]; }).length + '</p>';
      if (isFree(sid)) h += '<p class="small">الباقة المجانية: ' + quizUsedToday(sid) + ' / ' + FREE_DAILY + ' سؤال اليوم</p>';
      h += '<div class="row">عدد الأسئلة: ' + [5, 10, 20].map(function (n) { return '<button class="btn" onclick="A.startQuiz(' + l.id + ',' + n + ')">' + n + '</button>'; }).join(' ') + '</div>';
    }
    h += '</div><div class="row"><button class="btn" onclick="A.avatarOpen({lessonId:' + l.id + '})">اسأل المساعد عن الدرس</button><a class="btn" href="#/student/ask-new/' + l.id + '">اسأل معلّم</a></div>';
    return h;
  }

  var qShownAt = 0;
  function vQuiz(p) {
    var ses = find('sessions', p.id); if (!ses) return notFound();
    var sid = me().id; if (ses.studentId !== sid) return notFound();
    var l = find('lessons', ses.lessonId);
    if (ses.idx >= ses.questionIds.length) { location.replace('#/student/quiz-result/' + ses.id); return ''; }
    var q = find('questions', ses.questionIds[ses.idx]), r = ses.results[q.id];
    qShownAt = Date.now();
    var h = crumbs([[l.name, '#/student/lesson/' + l.id + '/practice'], ['تدريب']]) + '<div class="row"><b>سؤال ' + (ses.idx + 1) + ' من ' + ses.questionIds.length + '</b>' + qMeta(q) +
      (isFree(sid) ? '<span class="small">اليوم: ' + quizUsedToday(sid) + ' / ' + FREE_DAILY + '</span>' : '') + '</div>';
    h += '<div class="card">' + qBlock(q, r ? r.answer : null, !!r, 'qz') + '</div>';
    if (!r) h += '<div class="row"><button class="btn primary" onclick="A.quizCheck(\'' + ses.id + '\')">تحقّق</button><button class="btn" onclick="A.quizFinish(\'' + ses.id + '\')">إنهاء التدريب</button></div>';
    else h += feedbackBox(q, r) + '<div class="row"><button class="btn" onclick="A.avatarOpen({lessonId:' + l.id + ',questionId:' + q.id + ',sessionId:\'' + ses.id + '\'})">اسأل المساعد</button><a class="btn" href="#/student/ask-new/' + l.id + '/' + q.id + '">اسأل معلّم</a>' +
      '<button class="btn primary" onclick="A.quizNext(\'' + ses.id + '\')">' + (ses.idx + 1 < ses.questionIds.length ? 'التالي' : 'عرض النتيجة') + '</button></div>';
    return h;
  }
  function vQuizResult(p) {
    var ses = find('sessions', p.id); if (!ses) return notFound();
    var l = find('lessons', ses.lessonId), done = ses.questionIds.filter(function (id) { return ses.results[id]; });
    var h = crumbs([[l.name, '#/student/lesson/' + l.id + '/practice'], ['نتيجة التدريب']]) + '<div class="card headline"><div class="big">' + (ses.scorePct || 0) + ' / 100</div><div class="small">أجبت ' + done.length + ' سؤالًا · الوقت: ' + Math.round(((ses.submittedAt || Date.now()) - ses.startedAt) / 60000) + ' دقيقة</div></div>';
    h += '<h3>مراجعة الأسئلة</h3>' + done.map(function (id, i) {
      var q = find('questions', id), r = ses.results[id];
      return '<div class="card"><div class="small">' + (i + 1) + '. ' + qMeta(q) + '</div><div class="stem">' + esc(q.stem) + '</div><div>إجابتك: ' + esc(answerText(q, r.answer)) + '</div>' + feedbackBox(q, r) +
        '<button class="btn sm" onclick="A.avatarOpen({lessonId:' + q.lessonId + ',questionId:' + q.id + ',sessionId:\'' + ses.id + '\'})">اسأل المساعد</button></div>';
    }).join('');
    return h + '<div class="row"><button class="btn primary" onclick="A.startQuiz(' + l.id + ',' + ses.questionIds.length + ')">تدريب جديد</button><a class="btn" href="#/student/lesson/' + l.id + '">العودة للدرس</a></div>';
  }

  function vExamStart(p) {
    var u = find('units', p.id); if (!u) return notFound();
    var s = find('subjects', u.subjectId), sid = me().id, bp = blueprintForUnit(u), pool = servableWhere(inUnits([u.id])), sf = shortfall(pool, bp.typeCounts);
    var h = crumbs([['الرئيسية', '#/student'], [s.name, '#/student/subject/' + s.id], [u.name, '#/student/unit/' + u.id], ['امتحان الوحدة']]) + '<h2>امتحان: ' + esc(u.name) + '</h2>';
    h += '<div class="card"><b>' + esc(bp.name) + '</b>' + (bp.scope === 'subject' ? ' <span class="badge">النموذج الافتراضي للمادة</span>' : '') + '<table><tr><th>النوع</th><th>العدد</th><th>المتاح</th></tr>' +
      Object.keys(bp.typeCounts).filter(function (t) { return +bp.typeCounts[t]; }).map(function (t) { return '<tr><td>' + TYPE[t] + '</td><td>' + bp.typeCounts[t] + '</td><td>' + pool.filter(function (q) { return q.type === t; }).length + '</td></tr>'; }).join('') +
      '</table><p>الزمن: ' + (bp.timeLimitMin || 'مفتوح') + ' دقيقة · درجة النجاح: ' + bp.passMark + '</p><p class="small">لا تظهر الإجابات الصحيحة إلا بعد التسليم. تُحفظ إجاباتك تلقائيًا.</p></div>';
    if (sf.length) h += '<div class="card warn"><b>لا يمكن إنشاء الامتحان: عدد الأسئلة المتاحة غير كافٍ.</b><br>' + shortfallMsg(sf) + '</div>';
    else h += '<button class="btn primary" onclick="A.startExam([' + u.id + '])">ابدأ الامتحان</button>';
    h += examHistory(sid, function (x) { return x.kind === 'unit' && x.unitIds[0] == u.id; });
    return h;
  }
  function examHistory(sid, pred) {
    var ss = S.sessions.filter(function (x) { return x.studentId === sid && x.kind !== 'quiz' && x.submittedAt && pred(x); }).sort(function (a, b) { return b.submittedAt - a.submittedAt; });
    if (!ss.length) return '';
    var best = Math.max.apply(null, ss.map(function (x) { return x.scorePct; }));
    return '<h3>محاولاتك السابقة (أفضل درجة: ' + best + '/100)</h3><table><tr><th>التاريخ</th><th>الدرجة</th><th></th></tr>' + ss.map(function (x) {
      return '<tr><td>' + fmt(x.submittedAt) + '</td><td>' + x.scorePct + (x.scorePct === best ? ' <span class="badge">الأفضل</span>' : '') + '</td><td><a href="#/student/exam-result/' + x.id + '">عرض</a></td></tr>';
    }).join('') + '</table>';
  }
  var TIMER = null;
  function vExam(p) {
    var ses = find('sessions', p.id); if (!ses || ses.studentId !== me().id) return notFound();
    if (ses.submittedAt) { location.replace('#/student/exam-result/' + ses.id); return ''; }
    var endAt = ses.timeLimitMin ? ses.startedAt + ses.timeLimitMin * 60000 : null;
    if (endAt && Date.now() >= endAt) { submitExam(ses); location.replace('#/student/exam-result/' + ses.id); return ''; }
    var h = '<div class="sticky row"><b>' + esc(ses.title) + '</b><span class="timer" id="timer"></span><span class="small" id="saved">محفوظ تلقائيًا</span></div>';
    h += ses.questionIds.map(function (id, i) {
      var q = find('questions', id);
      return '<div class="card"><div class="small">' + (i + 1) + '. ' + qMeta(q) + ' · ' + q.maxScore + ' درجة</div>' + qBlock(q, ses.answers[id], false, 'ex', 'A.examSave(\'' + ses.id + '\',' + id + ')') + '</div>';
    }).join('');
    h += '<div class="row"><button class="btn primary" onclick="A.examSubmit(\'' + ses.id + '\')">تسليم الامتحان</button><button class="btn" onclick="A.avatarOpen({exam:true})">اسأل المساعد</button></div>';
    if (endAt) setTimeout(function () {
      var tick = function () {
        var left = endAt - Date.now(), el = $('timer'); if (!el) return;
        if (left <= 0) { clearInterval(TIMER); submitExam(ses); location.hash = '#/student/exam-result/' + ses.id; return; }
        el.textContent = 'الوقت المتبقي ' + Math.floor(left / 60000) + ':' + pad(Math.floor(left / 1000) % 60);
        el.className = 'timer' + (left < 60000 ? ' red' : '');
      };
      tick(); TIMER = setInterval(tick, 1000);
    }, 0);
    return h;
  }
  function vExamResult(p) {
    var ses = find('sessions', p.id); if (!ses) return notFound();
    var passed = ses.scorePct >= ses.passMark, byL = {}, byO = {};
    ses.questionIds.forEach(function (id) {
      var q = find('questions', id), r = ses.results[id] || { normalised: 0 };
      (byL[q.lessonId] = byL[q.lessonId] || []).push(r.normalised);
      if (q.objectiveId) (byO[q.objectiveId] = byO[q.objectiveId] || { l: q.lessonId, v: [] }).v.push(r.normalised);
    });
    var avg = function (a) { return a.reduce(function (x, y) { return x + y; }, 0) / a.length; };
    var h = '<h2>نتيجة: ' + esc(ses.title || 'امتحان') + '</h2><div class="card headline"><div class="big">' + ses.scorePct + ' / 100</div><div>' + (passed ? '<span class="badge ok">ناجح</span>' : '<span class="badge bad">لم تبلغ درجة النجاح (' + ses.passMark + ')</span>') + '</div></div>';
    h += '<h3>حسب الدرس</h3><table><tr><th>الدرس</th><th>النسبة</th><th></th></tr>' + Object.keys(byL).map(function (lid) {
      return '<tr><td>' + esc(find('lessons', lid).name) + '</td><td>' + pct(avg(byL[lid])) + '</td><td><button class="btn sm" onclick="A.startQuiz(' + lid + ',10)">درّب الآن</button></td></tr>';
    }).join('') + '</table>';
    var weak = Object.keys(byO).map(function (o) { return { o: o, l: byO[o].l, p: avg(byO[o].v) }; }).filter(function (x) { return x.p < 1; }).sort(function (a, b) { return a.p - b.p; }).slice(0, 3);
    h += '<h3>أضعف الأهداف</h3>' + (weak.length ? '<ul>' + weak.map(function (w) { return '<li>' + esc(objText(find('lessons', w.l), w.o)) + ' — ' + pct(w.p) + '</li>'; }).join('') + '</ul>' : '<p>لا توجد أهداف ضعيفة. أحسنت!</p>');
    h += '<h3>مراجعة الأسئلة</h3>' + ses.questionIds.map(function (id, i) {
      var q = find('questions', id), r = ses.results[id] || { answer: null, score: 0, normalised: 0 };
      return '<div class="card"><div class="small">' + (i + 1) + '. ' + qMeta(q) + '</div><div class="stem">' + esc(q.stem) + '</div><div>إجابتك: ' + esc(answerText(q, r.answer)) + '</div>' + feedbackBox(q, r) +
        '<button class="btn sm" onclick="A.avatarOpen({lessonId:' + q.lessonId + ',questionId:' + q.id + ',sessionId:\'' + ses.id + '\'})">اسأل المساعد</button></div>';
    }).join('');
    h += '<div class="row"><button class="btn primary" onclick="A.retake(\'' + ses.id + '\')">إعادة الامتحان</button><a class="btn" href="#/student/progress">تقدّمي</a></div>';
    return h;
  }
  var MX = { subject: null, units: [], size: 10 };
  function vMultiExam(p) {
    if (p.id && MX.subject != p.id) { MX.subject = +p.id; MX.units = []; }
    if (!MX.subject) MX.subject = S.subjects[0].id;
    var h = '<h2>امتحان متعدد الوحدات</h2><div class="card"><label>المادة <select onchange="A.mxSubject(this.value)">' + S.subjects.map(function (s) { return '<option value="' + s.id + '"' + (s.id == MX.subject ? ' selected' : '') + '>' + esc(s.name) + '</option>'; }).join('') + '</select></label>';
    h += '<div>اختر وحدتين أو أكثر:</div>' + subjectUnits(MX.subject).map(function (u) { return '<label class="opt"><input type="checkbox" ' + (MX.units.indexOf(u.id) >= 0 ? 'checked' : '') + ' onchange="A.mxUnit(' + u.id + ',this.checked)"> ' + esc(u.name) + ' <span class="small">(' + servableWhere(inUnits([u.id])).length + ' سؤال متاح)</span></label>'; }).join('');
    h += '<div class="row">الحجم: ' + [10, 20].map(function (n) { return '<label><input type="radio" name="mxs" ' + (MX.size === n ? 'checked' : '') + ' onchange="A.mxSize(' + n + ')"> ' + n + '</label>'; }).join(' ') + '</div></div>';
    if (MX.units.length < 2) return h + '<p class="small">اختر وحدتين على الأقل.</p>';
    var counts = mergeBlueprints(MX.units, MX.size), pool = servableWhere(inUnits(MX.units)), sf = shortfall(pool, counts);
    h += '<div class="card"><b>النموذج المدمج (تناسبيًا)</b><table><tr><th>النوع</th><th>العدد</th><th>المتاح</th></tr>' + Object.keys(counts).filter(function (t) { return counts[t]; }).map(function (t) { return '<tr><td>' + TYPE[t] + '</td><td>' + counts[t] + '</td><td>' + pool.filter(function (q) { return q.type === t; }).length + '</td></tr>'; }).join('') + '</table></div>';
    if (sf.length) h += '<div class="card warn"><b>عجز في الأسئلة المتاحة:</b><br>' + shortfallMsg(sf) + '</div>';
    else h += '<button class="btn primary" onclick="A.startExam(' + JSON.stringify(MX.units) + ',' + MX.size + ')">ابدأ الامتحان</button>';
    return h;
  }

  function vProgress(p, forSid) {
    var sid = forSid || me().id, u = find('users', sid), m = attMap(sid), admin = !!forSid;
    var hl = headline(sid), h = '<h2>' + (admin ? 'تقدّم الطالب: ' + esc(u.name) : 'تقدّمي') + '</h2>';
    h += '<div class="card">متبقّي ' + hl.X + ' من ' + hl.Y + ' · شاهد ' + hl.seen + ' · أتقن ' + hl.mastered + ' · سلسلة الأيام: ' + streak(sid) + ' · الباقة: ' + planLabel(sid) + '</div>';
    S.subjects.forEach(function (s) {
      h += '<div class="card"><b>' + esc(s.name) + '</b> — ' + progressBar(mastery(m, servableWhere(inSubject(s.id)))) + '<table><tr><th>الوحدة</th><th>الإتقان</th><th>أفضل امتحان</th></tr>' +
        subjectUnits(s.id).map(function (un) { var b = bestExam(sid, un.id); return '<tr><td>' + esc(un.name) + '</td><td>' + pct(mastery(m, servableWhere(inUnits([un.id])))) + '</td><td>' + (b == null ? '—' : b) + '</td></tr>'; }).join('') + '</table></div>';
    });
    var weak = [];
    S.lessons.filter(function (l) { return l.state === 'published'; }).forEach(function (l) { var qs = servableWhere(inLesson(l.id)); if (qs.some(function (q) { return m[q.id]; })) weak.push({ l: l, p: mastery(m, qs) }); });
    weak.sort(function (a, b) { return a.p - b.p; });
    h += '<h3>نقاط الضعف</h3>' + (weak.length ? '<table><tr><th>الدرس</th><th>الإتقان</th><th></th></tr>' + weak.slice(0, 4).map(function (w) { return '<tr><td>' + esc(w.l.name) + '</td><td>' + pct(w.p) + '</td><td>' + (admin ? '' : '<button class="btn sm" onclick="A.startQuiz(' + w.l.id + ',10)">درّب الآن</button>') + '</td></tr>'; }).join('') + '</table>' : '<p class="small">لا توجد بيانات بعد.</p>');
    var objs = {};
    S.attempts.forEach(function (a) { if (a.studentId !== sid) return; var q = find('questions', a.questionId); if (!q || !q.objectiveId) return; var o = objs[q.objectiveId] = objs[q.objectiveId] || { l: q.lessonId, n: 0, ok: 0 }; o.n++; if (a.normalised >= MT) o.ok++; });
    var wo = Object.keys(objs).map(function (k) { return { k: k, l: objs[k].l, p: objs[k].ok / objs[k].n }; }).sort(function (a, b) { return a.p - b.p; }).slice(0, 3);
    if (wo.length) h += '<h4>أضعف الأهداف</h4><ul>' + wo.map(function (w) { return '<li>' + esc(objText(find('lessons', w.l), w.k)) + ' — ' + pct(w.p) + '</li>'; }).join('') + '</ul>';
    var f = S.ui.histFilter || 'all';
    var ss = S.sessions.filter(function (x) { return x.studentId === sid && (f === 'all' || (f === 'quiz' ? x.kind === 'quiz' : x.kind !== 'quiz')); }).sort(function (a, b) { return b.startedAt - a.startedAt; });
    h += '<h3>سجل الجلسات</h3><select onchange="A.histFilter(this.value)">' + [['all', 'الكل'], ['quiz', 'تدريبات'], ['exam', 'امتحانات']].map(function (o) { return '<option value="' + o[0] + '"' + (o[0] === f ? ' selected' : '') + '>' + o[1] + '</option>'; }).join('') + '</select>';
    h += '<table><tr><th>التاريخ</th><th>النوع</th><th>النطاق</th><th>الدرجة</th><th></th></tr>' + ss.map(function (x) {
      var scope = x.kind === 'quiz' ? find('lessons', x.lessonId).name : x.title || x.unitIds.map(function (i) { return find('units', i).name; }).join('، ');
      var link = admin ? '' : x.kind === 'quiz' ? '#/student/quiz-result/' + x.id : '#/student/exam-result/' + x.id;
      return '<tr><td>' + fmt(x.startedAt) + '</td><td>' + (x.kind === 'quiz' ? 'تدريب' : x.kind === 'unit' ? 'امتحان وحدة' : 'متعدد الوحدات') + '</td><td>' + esc(scope) + '</td><td>' + (x.submittedAt ? x.scorePct : 'جارٍ') + '</td><td>' + (link && x.submittedAt ? '<a href="' + link + '">عرض</a>' : '') + '</td></tr>';
    }).join('') + '</table>';
    return h;
  }

  function vAskList() {
    var sid = me().id, h = '<h2>اسأل معلّم</h2>';
    if (!hasPlan(sid, 'ask')) return h + '<div class="card warn">هذه الخدمة إضافة مدفوعة (' + PRICES.ask + ' ج.م/شهر) وتتطلب الباقة الأساسية. <a class="btn sm" href="#/student/subscription">الاشتراك</a></div>';
    var mine = S.threads.filter(function (t) { return t.studentId === sid; }).sort(function (a, b) { return b.submittedAt - a.submittedAt; });
    var month = mine.filter(function (t) { return Date.now() - t.submittedAt < 30 * DAY; }).length;
    h += '<div class="row"><a class="btn primary" href="#/student/ask-new">سؤال جديد</a><span class="small">الرصيد الشهري: ' + month + ' / ' + ASK_QUOTA + '</span></div>';
    return h + threadTable(mine, '#/student/thread/', false);
  }
  function slaBadge(th) {
    if (th.status === 'closed') return '<span class="badge">مغلق</span>';
    if (threadBreached(th)) return '<span class="badge bad">متأخر</span>';
    if (threadAwaiting(th)) return '<span class="badge">بانتظار الرد · متبقٍ ' + dur(th.slaDueAt - Date.now()) + '</span>';
    return '<span class="badge ok">تم الرد</span>';
  }
  function threadTable(list, base, forTeacher) {
    if (!list.length) return '<p class="small">لا توجد أسئلة.</p>';
    return '<table><tr><th>السؤال</th><th>المادة / الدرس</th>' + (forTeacher ? '<th>الطالب</th><th>المعلّم</th>' : '') + '<th>الحالة</th><th>أُرسل</th></tr>' + list.map(function (t) {
      var l = find('lessons', t.context.lessonId);
      return '<tr><td><a href="' + base + t.id + '">' + esc(t.messages[0].text.slice(0, 50)) + '…</a></td><td>' + esc(find('subjects', t.subjectId).name) + (l ? ' / ' + esc(l.name) : '') + '</td>' +
        (forTeacher ? '<td>' + esc(uname(t.studentId)) + '</td><td>' + (t.teacherId ? esc(uname(t.teacherId)) : '<i>غير مُستلم</i>') + '</td>' : '') + '<td>' + slaBadge(t) + '</td><td>' + fmt(t.submittedAt) + '</td></tr>';
    }).join('') + '</table>';
  }
  function vAskNew(p) {
    var sid = me().id; if (!hasPlan(sid, 'ask')) return vAskList();
    var l = p.lid ? find('lessons', p.lid) : null, q = p.qid ? find('questions', p.qid) : null;
    var h = '<h2>سؤال جديد للمعلّم</h2><div class="card">';
    if (l) h += '<div class="small">السياق المرفق تلقائيًا: ' + esc(find('subjects', l.subjectId).name) + ' / ' + esc(find('units', l.unitId).name) + ' / ' + esc(l.name) + (q ? ' / سؤال #' + q.id + ': ' + esc(q.stem) : '') + '</div>';
    else h += '<label>الدرس <select id="ask-lesson">' + S.lessons.filter(function (x) { return x.state === 'published'; }).map(function (x) { return '<option value="' + x.id + '">' + esc(find('subjects', x.subjectId).name + ' — ' + x.name) + '</option>'; }).join('') + '</select></label>';
    h += '<textarea id="ask-text" rows="5" placeholder="اكتب سؤالك للمعلّم"></textarea><label class="small"><input type="checkbox" id="ask-img"> إرفاق صورة لحلّك (محاكاة)</label>';
    h += '<p class="small">سيُرسل السؤال لمعلّمي المادة، ومهلة الرد 24 ساعة.</p><button class="btn primary" onclick="A.askSubmit(' + (l ? l.id : 'null') + ',' + (q ? q.id : 'null') + ')">إرسال</button></div>';
    return h;
  }
  function threadView(th, viewer) {
    var l = find('lessons', th.context.lessonId), q = th.context.questionId ? find('questions', th.context.questionId) : null;
    var h = '<div class="card small">المادة: ' + esc(find('subjects', th.subjectId).name) + (l ? ' · الدرس: ' + esc(l.name) : '') + (q ? ' · السؤال: ' + esc(q.stem) : '') + '<br>الطالب: ' + esc(uname(th.studentId)) + ' · المعلّم: ' + (th.teacherId ? esc(uname(th.teacherId)) : 'غير مُستلم') + ' · موعد الرد: ' + fmt(th.slaDueAt) + ' ' + slaBadge(th) + (th.attachedImage ? ' · صورة مرفقة (محاكاة)' : '') + '</div>';
    h += th.messages.map(function (m) {
      var mine = m.senderId === th.studentId;
      return '<div class="msg ' + (mine ? 'user' : 'assistant') + '"><div class="small">' + esc(uname(m.senderId)) + ' · ' + fmt(m.at) + '</div>' +
        (m.kind === 'voice' ? '<div class="audio">▶ ━━━━━━━━━━━━ ' + (m.audioSec || 30) + ' ث (تسجيل صوتي - محاكاة)</div><div class="small">نص التفريغ الصوتي:</div>' : '') + esc(m.text) + '</div>';
    }).join('');
    if (th.rating) h += '<div class="card">التقييم: ' + '★'.repeat(th.rating) + '☆'.repeat(5 - th.rating) + '</div>';
    return h;
  }
  function vThreadStudent(p) {
    var th = find('threads', p.id); if (!th || th.studentId !== me().id) return notFound();
    var h = crumbs([['اسأل معلّم', '#/student/ask'], ['المحادثة']]) + threadView(th);
    var teacherReplies = th.messages.filter(function (m) { return m.senderId !== th.studentId; }).length;
    if (th.status !== 'closed' && !threadAwaiting(th) && teacherReplies > 0) {
      if (!th.followupUsed) h += '<div class="card"><b>سؤال متابعة (مرة واحدة فقط)</b><textarea id="fu-text" rows="3"></textarea><button class="btn" onclick="A.askFollowup(\'' + th.id + '\')">إرسال المتابعة</button></div>';
      h += '<div class="card"><b>قيّم الإجابة وأغلق السؤال</b><div class="row">' + [1, 2, 3, 4, 5].map(function (n) { return '<button class="btn" onclick="A.askRate(\'' + th.id + '\',' + n + ')">' + n + ' ★</button>'; }).join('') + '</div></div>';
    }
    return h;
  }
  function vSubscription() {
    var sid = me().id, subs = activeSubs(sid);
    var h = '<div class="card aurora"><div class="big">اشترك في المنهج</div><div class="small">100,000 سؤال معتمد من معلّمين حقيقيين · تدريب لا نهائي · امتحانات وحدات</div></div><h2>الاشتراك</h2><div class="card">باقتك الحالية: <b>' + planLabel(sid) + '</b>' + subs.map(function (s) { return '<div class="small">' + PLAN[s.plan] + ' — نشط حتى ' + fmt(s.periodEnd) + ' (' + esc(s.paymobRef) + ') <button class="btn sm" onclick="A.cancelSub(\'' + s.id + '\')">إلغاء</button></div>'; }).join('') + '</div><div class="grid">';
    h += '<div class="card"><b>مجاني</b><ul><li>تصفح المحتوى</li><li>الدرس الأول من كل وحدة</li><li>' + FREE_DAILY + ' أسئلة تدريب يوميًا</li><li>المساعد: ' + FREE_AVATAR + ' رسائل يوميًا</li></ul></div>';
    h += '<div class="card"><b>الأساسية — ' + PRICES.base + ' ج.م/شهر</b><ul><li>تدريب وامتحانات بلا حدود</li><li>كل الدروس</li><li>تقدم كامل</li></ul>' + (hasPlan(sid, 'base') ? '<span class="badge ok">مفعّلة</span>' : '<button class="btn primary" onclick="A.checkout(\'base\')">اشترك</button>') + '</div>';
    h += '<div class="card"><b>اسأل معلّم — ' + PRICES.ask + ' ج.م/شهر</b><ul><li>تتطلب الأساسية</li><li>' + ASK_QUOTA + ' سؤالًا شهريًا</li><li>رد خلال 24 ساعة</li></ul>' + (hasPlan(sid, 'ask') ? '<span class="badge ok">مفعّلة</span>' : '<button class="btn" onclick="A.checkout(\'ask\')"' + (hasPlan(sid, 'base') ? '' : ' disabled title="تتطلب الباقة الأساسية"') + '>اشترك</button>') + '</div></div>';
    var pays = S.payments.filter(function (x) { return x.studentId === sid; });
    if (pays.length) h += '<h3>المدفوعات</h3><table><tr><th>التاريخ</th><th>الباقة</th><th>المبلغ</th><th>الحالة</th></tr>' + pays.map(function (x) { return '<tr><td>' + fmt(x.at) + '</td><td>' + PLAN[x.plan] + '</td><td>' + x.amount + ' ' + x.currency + '</td><td>' + (x.status === 'success' ? 'ناجحة' : 'فاشلة') + '</td></tr>'; }).join('') + '</table>';
    return h + '<p class="small">ملاحظة: الاشتراك يُفعَّل فقط عبر Webhook من Paymob (محاكاة هنا).</p>';
  }

  /* ================= 9. TEACHER VIEWS ================= */
  var TF = { unit: '', lesson: '', type: '', difficulty: '' };
  function vTeacherQueue() {
    var t = me(), subs = teacherSubjects(t);
    var qs = S.questions.filter(function (q) { return q.validationStatus === 'pending' && subs.indexOf(q.subjectId) >= 0; });
    var units = S.units.filter(function (u) { return subs.indexOf(u.subjectId) >= 0; }), lessons = S.lessons.filter(function (l) { return subs.indexOf(l.subjectId) >= 0; });
    qs = qs.filter(function (q) { var l = lessonOf(q); return (!TF.unit || l.unitId == TF.unit) && (!TF.lesson || l.id == TF.lesson) && (!TF.type || q.type === TF.type) && (!TF.difficulty || q.difficulty === TF.difficulty); })
      .sort(function (a, b) { return a.createdAt - b.createdAt; });
    var sel = function (key, opts) { return '<select onchange="A.tf(\'' + key + '\',this.value)"><option value="">الكل</option>' + opts.map(function (o) { return '<option value="' + o[0] + '"' + (TF[key] == o[0] ? ' selected' : '') + '>' + esc(o[1]) + '</option>'; }).join('') + '</select>'; };
    var h = '<h2>قائمة المراجعة — ' + subs.map(function (s) { return esc(find('subjects', s).name); }).join('، ') + '</h2>' + statsCard(t);
    h += '<div class="card row filters">الوحدة ' + sel('unit', units.map(function (u) { return [u.id, u.name]; })) + ' الدرس ' + sel('lesson', lessons.map(function (l) { return [l.id, l.name]; })) +
      ' النوع ' + sel('type', TYPES.map(function (x) { return [x, TYPE[x]]; })) + ' الصعوبة ' + sel('difficulty', Object.keys(DIFF).map(function (d) { return [d, DIFF[d]]; })) + '</div>';
    h += '<table><tr><th>#</th><th>السؤال</th><th>النوع</th><th>الدرس</th><th>الصعوبة</th><th>الإصدار</th><th>العمر</th></tr>' + qs.map(function (q) {
      return '<tr><td>' + q.id + '</td><td><a href="#/teacher/q/' + q.id + '">' + esc(q.stem.slice(0, 60)) + '</a></td><td>' + TYPE[q.type] + v2badge(q) + '</td><td>' + esc(lessonOf(q).name) + '</td><td>' + DIFF[q.difficulty] + '</td><td>v' + q.version + '</td><td>' + dur(Date.now() - q.createdAt) + '</td></tr>';
    }).join('') + '</table>' + (qs.length ? '' : '<p>لا توجد أسئلة بانتظار المراجعة.</p>');
    return h;
  }
  function statsCard(t) {
    var dec = S.audit.filter(function (a) { return a.actorId === t.id && (a.action === 'approve' || a.action === 'reject'); });
    var times = dec.map(function (a) { var q = find('questions', a.entityId); return q ? a.at - q.createdAt : 0; }).filter(function (x) { return x > 0; });
    var ths = S.threads.filter(function (th) { return th.teacherId === t.id; }), okSla = ths.filter(function (th) { var r = th.messages.filter(function (m) { return m.senderId === t.id; })[0]; return r && r.at <= th.slaDueAt; }).length;
    return '<div class="card stats"><b>إحصائياتي</b> · معتمد: ' + dec.filter(function (a) { return a.action === 'approve'; }).length + ' · مرفوض: ' + dec.filter(function (a) { return a.action === 'reject'; }).length +
      ' · متوسط (وسيط) زمن القرار: ' + (times.length ? dur(median(times)) : '—') + ' · الالتزام بمهلة الرد: ' + (ths.length ? pct(okSla / ths.length) : '—') + ' (' + ths.length + ' محادثة)</div>';
  }
  function history(qid) {
    var rows = S.audit.filter(function (a) { return a.entity === 'question' && a.entityId == qid; }).sort(function (a, b) { return a.at - b.at; });
    return '<table><tr><th>التاريخ</th><th>بواسطة</th><th>الإجراء</th><th>تفاصيل</th></tr>' + rows.map(function (a) { return '<tr><td>' + fmt(a.at) + '</td><td>' + esc(uname(a.actorId)) + '</td><td>' + esc(a.action) + '</td><td class="small">' + esc(a.diff ? JSON.stringify(a.diff) : '') + '</td></tr>'; }).join('') + '</table>';
  }
  function vTeacherQuestion(p) {
    var t = me(), q = find('questions', p.id);
    if (!q || !teacherCan(t, q.subjectId)) return '<div class="card warn">غير مسموح: هذا السؤال خارج المواد المسندة إليك.</div>';
    var l = lessonOf(q), h = crumbs([['قائمة المراجعة', '#/teacher'], ['سؤال #' + q.id]]);
    h += '<h2>سؤال #' + q.id + ' <span class="badge">' + QST[q.validationStatus] + '</span> <span class="badge">v' + q.version + '</span></h2>';
    h += '<div class="card small">الدرس: ' + esc(l.name) + ' (' + LST[l.state] + ') · الهدف: ' + esc(objText(l, q.objectiveId)) + ' · ' + qMeta(q) + ' · الدرجة: ' + q.maxScore + '</div>';
    h += '<div class="card"><div class="small">معاينة كما يراها الطالب:</div>' + qBlock(q, null, true, 'tv') + '</div>';
    h += '<div class="card"><div>الإجابة الصحيحة: <b>' + esc(correctText(q)) + '</b></div><div>الشرح: ' + esc(q.explanation) + '</div><details><summary>مواصفات التصحيح (JSON)</summary><pre>' + esc(JSON.stringify({ body: q.body, gradingSpec: q.gradingSpec }, null, 1)) + '</pre></details></div>';
    if (q.rejectionReason) h += '<div class="card warn">سبب الرفض السابق: ' + esc(q.rejectionReason) + '</div>';
    if (q.validationStatus === 'pending') {
      h += '<div class="card"><b>القرار</b><div class="row">الصعوبة: <select id="t-diff">' + Object.keys(DIFF).map(function (d) { return '<option value="' + d + '"' + (d === q.difficulty ? ' selected' : '') + '>' + DIFF[d] + '</option>'; }).join('') + '</select>' +
        '<button class="btn primary" onclick="A.approve(' + q.id + ')">اعتماد</button></div><textarea id="t-reason" rows="2" placeholder="سبب الرفض (إلزامي عند الرفض)"></textarea><button class="btn danger" onclick="A.reject(' + q.id + ')">رفض</button>' +
        '<p class="small">لا يمكن للمعلّم تعديل نص السؤال أو الخيارات أو الإجابة. إذا كان هناك خطأ ارفض مع ذكر السبب.</p></div>';
    }
    h += '<h3>سجل الإصدارات والقرارات</h3>' + history(q.id);
    return h;
  }
  var INBOX_F = 'all';
  function vTeacherInbox() {
    var t = me(), subs = teacherSubjects(t);
    var list = S.threads.filter(function (th) { return subs.indexOf(th.subjectId) >= 0; });
    if (INBOX_F === 'unclaimed') list = list.filter(function (th) { return !th.teacherId; });
    if (INBOX_F === 'mine') list = list.filter(function (th) { return th.teacherId === t.id; });
    list.sort(function (a, b) { return a.slaDueAt - b.slaDueAt; });
    return '<h2>أسئلة الطلاب</h2><div class="subtabs">' + [['all', 'الكل'], ['unclaimed', 'غير مُستلمة'], ['mine', 'الخاصة بي']].map(function (f) { return '<a href="javascript:A.inboxF(\'' + f[0] + '\')" class="' + (INBOX_F === f[0] ? 'active' : '') + '">' + f[1] + '</a>'; }).join('') + '</div>' + threadTable(list, '#/teacher/thread/', true);
  }
  function vTeacherThread(p) {
    var t = me(), th = find('threads', p.id), isAdmin = role() === 'admin';
    if (!th || (!isAdmin && !teacherCan(t, th.subjectId))) return '<div class="card warn">غير مسموح: هذه المحادثة خارج موادك.</div>';
    var h = crumbs([['أسئلة الطلاب', isAdmin ? '#/admin' : '#/teacher/inbox'], ['المحادثة']]) + threadView(th);
    if (isAdmin) return h;
    if (!th.teacherId) h += '<button class="btn primary" onclick="A.claim(\'' + th.id + '\')">استلام السؤال</button>';
    else if (th.teacherId !== t.id) h += '<p class="small">هذه المحادثة مستلمة بواسطة معلّم آخر.</p>';
    else if (threadAwaiting(th)) {
      h += '<div class="card"><b>الرد</b><div class="row"><label><input type="radio" name="rk" value="text" checked onchange="A.replyKind(this.value)"> نص</label><label><input type="radio" name="rk" value="voice" onchange="A.replyKind(this.value)"> صوت</label></div>' +
        '<div id="voicebox" class="hidden"><button class="btn" onclick="A.fakeRecord()">● تسجيل (محاكاة)</button><div class="audio" id="fakeaudio"></div><label class="small">نص التفريغ الصوتي (يمكنك تصحيحه قبل الإرسال)</label></div>' +
        '<textarea id="reply-text" rows="4" placeholder="اكتب ردك"></textarea><button class="btn primary" onclick="A.reply(\'' + th.id + '\')">إرسال الرد</button></div>';
    }
    return h;
  }

  /* ================= 10. ADMIN VIEWS ================= */
  var DF = { subject: '', days: 14 };
  function vAdminDash() {
    var now = Date.now(), today = dayKey(now), sf = DF.subject;
    var students = S.users.filter(function (u) { return u.role === 'student'; });
    var atts = S.attempts.filter(function (a) { var q = find('questions', a.questionId); return !sf || (q && q.subjectId == sf); });
    var dau = students.filter(function (s) { return atts.some(function (a) { return a.studentId === s.id && dayKey(a.at) === today; }); }).length;
    var mau = students.filter(function (s) { return atts.some(function (a) { return a.studentId === s.id && now - a.at < 30 * DAY; }); }).length;
    var todayAtt = atts.filter(function (a) { return dayKey(a.at) === today; }).length;
    var subsA = S.subscriptions.filter(function (s) { return s.status === 'active'; });
    var churn = S.subscriptions.filter(function (s) { return s.status === 'cancelled' && now - (s.cancelledAt || 0) < 30 * DAY; }).length;
    var mrr = subsA.reduce(function (x, s) { return x + PRICES[s.plan]; }, 0);
    var qs = S.questions.filter(function (q) { return !sf || q.subjectId == sf; });
    var lessons = S.lessons.filter(function (l) { return !sf || l.subjectId == sf; });
    var srv = qs.filter(servable).length;
    var correct = atts.filter(function (a) { return a.normalised >= MT; }).length;
    var decided = qs.filter(function (q) { return q.validatedAt; }).map(function (q) { return q.validatedAt - q.createdAt; });
    var ths = S.threads.filter(function (t) { return !sf || t.subjectId == sf; });
    var replyTimes = ths.map(function (t) { var r = t.messages.filter(function (m) { return m.senderId !== t.studentId; })[0]; return r ? r.at - t.submittedAt : null; }).filter(function (x) { return x != null; });
    var pays = S.payments, okPays = pays.filter(function (p) { return p.status === 'success'; });
    var card = function (title, body) { return '<div class="card"><div class="small">' + title + '</div>' + body + '</div>'; };
    var h = '<h2>لوحة المؤشرات</h2><div class="card row filters">المادة <select onchange="A.df(\'subject\',this.value)"><option value="">الكل</option>' + S.subjects.map(function (s) { return '<option value="' + s.id + '"' + (sf == s.id ? ' selected' : '') + '>' + esc(s.name) + '</option>'; }).join('') + '</select>' +
      ' الفترة <select onchange="A.df(\'days\',this.value)">' + [7, 14, 30].map(function (d) { return '<option value="' + d + '"' + (DF.days == d ? ' selected' : '') + '>آخر ' + d + ' يوم</option>'; }).join('') + '</select></div><div class="grid">';
    h += card('الطلاب', '<div class="big">' + students.length + '</div>جدد هذا الأسبوع: ' + students.filter(function (s) { return now - s.joinedAt < 7 * DAY; }).length + '<br>نشط اليوم (DAU): ' + dau + '<br>نشط هذا الشهر (MAU): ' + mau);
    h += card('المشتركون', 'الأساسية: ' + subsA.filter(function (s) { return s.plan === 'base'; }).length + '<br>اسأل معلّم: ' + subsA.filter(function (s) { return s.plan === 'ask'; }).length + '<br>ألغوا هذا الشهر: ' + churn + '<br>MRR: ' + mrr + ' ج.م');
    h += card('المحتوى', '<div class="big">' + srv + '</div>سؤال قابل للعرض (الرقم المُعلن)<br>دروس: منشور ' + lessons.filter(function (l) { return l.state === 'published'; }).length + ' · مسودة ' + lessons.filter(function (l) { return l.state === 'draft'; }).length + ' · مؤرشف ' + lessons.filter(function (l) { return l.state === 'archived'; }).length +
      '<br>' + Object.keys(QST).map(function (k) { return QST[k] + ': ' + qs.filter(function (q) { return q.validationStatus === k; }).length; }).join(' · '));
    h += card('معدل الحل', '<div class="big">' + (dau ? (todayAtt / dau).toFixed(1) : 0) + '</div>محاولة لكل طالب نشط اليوم');
    h += card('معدل النجاح', '<div class="big">' + pct(atts.length ? correct / atts.length : 0) + '</div>' + S.subjects.map(function (s) { var a = S.attempts.filter(function (x) { var q = find('questions', x.questionId); return q && q.subjectId === s.id; }); return esc(s.name) + ': ' + pct(a.length ? a.filter(function (x) { return x.normalised >= MT; }).length / a.length : 0); }).join('<br>'));
    h += card('المراجعة', 'قيد المراجعة: <b>' + qs.filter(function (q) { return q.validationStatus === 'pending'; }).length + '</b><br>وسيط زمن القرار: ' + (decided.length ? dur(median(decided)) : '—') + '<br>' + S.users.filter(function (u) { return u.role === 'teacher'; }).map(function (t) { return esc(t.name) + ': ' + S.audit.filter(function (a) { return a.actorId === t.id && (a.action === 'approve' || a.action === 'reject'); }).length + ' قرار'; }).join('<br>'));
    h += card('اسأل معلّم', 'مفتوحة: ' + ths.filter(function (t) { return t.status !== 'closed'; }).length + '<br>متأخرة: <b class="red">' + ths.filter(threadBreached).length + '</b><br>وسيط زمن الرد: ' + (replyTimes.length ? dur(median(replyTimes)) : '—') +
      '<br>متوسط التقييم: ' + (function () { var r = ths.filter(function (t) { return t.rating; }); return r.length ? (r.reduce(function (x, t) { return x + t.rating; }, 0) / r.length).toFixed(1) : '—'; })());
    h += card('المدفوعات', 'ناجحة: ' + okPays.length + ' · فاشلة: ' + (pays.length - okPays.length) + '<br>الإيرادات: <b>' + okPays.reduce(function (x, p) { return x + p.amount; }, 0) + ' ج.م</b><br>مستردات: 0');
    h += '</div>';
    // charts
    var days = [];
    for (var i = DF.days - 1; i >= 0; i--) days.push(dayKey(now - i * DAY));
    var perDay = days.map(function (d) { return atts.filter(function (a) { return dayKey(a.at) === d; }).length; });
    var revDay = days.map(function (d) { return okPays.filter(function (p) { return dayKey(p.at) === d; }).reduce(function (x, p) { return x + p.amount; }, 0); });
    h += '<div class="grid"><div class="card"><b>المحاولات يوميًا</b>' + svgBars(days, perDay) + '</div><div class="card"><b>الإيرادات يوميًا (ج.م)</b>' + svgBars(days, revDay) + '</div>';
    var byType = TYPES.map(function (t) { return qs.filter(function (q) { return q.type === t; }).length; }), mx = Math.max.apply(null, byType);
    h += '<div class="card"><b>الأسئلة حسب النوع</b>' + TYPES.map(function (t, i) { return bar(byType[i], mx, TYPE[t]); }).join('') + '</div></div>';
    return h;
  }
  function svgBars(labels, vals) {
    var W = 320, Hh = 120, max = Math.max.apply(null, vals.concat([1])), bw = W / vals.length;
    var bars = vals.map(function (v, i) { var h = Math.round(v / max * (Hh - 20)); return '<rect x="' + (i * bw + 2) + '" y="' + (Hh - 14 - h) + '" width="' + (bw - 4) + '" height="' + h + '" fill="#888"><title>' + labels[i] + ': ' + v + '</title></rect>' + (v ? '<text x="' + (i * bw + bw / 2) + '" y="' + (Hh - 16 - h) + '" font-size="8" text-anchor="middle">' + v + '</text>' : ''); }).join('');
    return '<svg viewBox="0 0 ' + W + ' ' + Hh + '" class="chart" direction="ltr">' + bars + '<line x1="0" y1="' + (Hh - 14) + '" x2="' + W + '" y2="' + (Hh - 14) + '" stroke="#999"/><text x="2" y="' + (Hh - 2) + '" font-size="8">' + labels[0].slice(5) + '</text><text x="' + (W - 2) + '" y="' + (Hh - 2) + '" font-size="8" text-anchor="end">' + last(labels).slice(5) + '</text></svg>';
  }

  function vAdminContent() {
    var h = '<h2>شجرة المحتوى</h2><button class="btn" onclick="A.addSubject()">+ مادة</button>';
    S.subjects.slice().sort(byOrder).forEach(function (s) {
      h += '<div class="card tree"><div class="row"><b>' + esc(s.name) + '</b>' + ctl('subjects', s.id) + '<button class="btn sm" onclick="A.addUnit(' + s.id + ')">+ وحدة</button></div>';
      subjectUnits(s.id).forEach(function (u) {
        h += '<div class="tunit"><div class="row"><b>' + esc(u.name) + '</b>' + ctl('units', u.id) + '<button class="btn sm" onclick="A.addLesson(' + u.id + ')">+ درس</button></div>';
        unitLessons(u.id).forEach(function (l) {
          var qn = S.questions.filter(function (q) { return q.lessonId === l.id; });
          h += '<div class="tlesson row"><a href="#/admin/lesson/' + l.id + '">' + esc(l.name) + '</a><span class="badge ' + (l.state === 'published' ? 'ok' : '') + '">' + LST[l.state] + '</span><span class="small">' + qn.length + ' سؤال · ' + qn.filter(servable).length + ' قابل للعرض</span>' + ctl('lessons', l.id) +
            ['published', 'draft', 'archived'].filter(function (st) { return st !== l.state; }).map(function (st) { return '<button class="btn sm" onclick="A.lessonState(' + l.id + ',\'' + st + '\')">' + { published: 'نشر', draft: 'إلغاء النشر', archived: 'أرشفة' }[st] + '</button>'; }).join('') + '</div>';
        });
        h += '</div>';
      });
      h += '</div>';
    });
    return h;
  }
  function ctl(coll, id) { return '<button class="btn sm" title="أعلى" onclick="A.move(\'' + coll + '\',' + id + ',-1)">▲</button><button class="btn sm" title="أسفل" onclick="A.move(\'' + coll + '\',' + id + ',1)">▼</button><button class="btn sm" onclick="A.rename(\'' + coll + '\',' + id + ')">إعادة تسمية</button><button class="btn sm danger" onclick="A.del(\'' + coll + '\',' + id + ')">حذف</button>'; }
  function vAdminLesson(p) {
    var l = find('lessons', p.id); if (!l) return notFound();
    var h = crumbs([['المحتوى', '#/admin/content'], [l.name]]) + '<h2>تحرير الدرس <span class="badge">' + LST[l.state] + '</span></h2><div class="card">';
    h += '<label>الاسم<input id="le-name" value="' + esc(l.name) + '"></label><label>الشرح (فقرات يفصلها سطر فارغ)<textarea id="le-exp" rows="6">' + esc(l.explanation) + '</textarea></label>' +
      '<label>الأهداف (هدف في كل سطر)<textarea id="le-obj" rows="4">' + esc(l.objectives.map(function (o) { return o.text; }).join('\n')) + '</textarea></label><label>الملخص<textarea id="le-sum" rows="3">' + esc(l.summary) + '</textarea></label>' +
      '<label>رابط فيديو (اختياري)<input id="le-video" value="' + esc(l.videoUrl || '') + '" placeholder="https://..."></label><button class="btn primary" onclick="A.saveLesson(' + l.id + ')">حفظ</button></div>';
    h += '<h3>أسئلة الدرس</h3><a class="btn" href="#/admin/question/new/' + l.id + '">+ سؤال جديد</a>' + questionTable(S.questions.filter(function (q) { return q.lessonId === l.id; }));
    return h;
  }
  function questionTable(qs) {
    return '<table><tr><th>#</th><th>السؤال</th><th>النوع</th><th>الحالة</th><th>قابل للعرض</th><th>الإصدار</th><th>المعلّم</th><th>سبب الرفض</th><th></th></tr>' + qs.map(function (q) {
      return '<tr><td>' + q.id + '</td><td>' + esc(q.stem.slice(0, 50)) + '</td><td>' + TYPE[q.type] + v2badge(q) + '</td><td><span class="badge ' + (q.validationStatus === 'approved' ? 'ok' : q.validationStatus === 'rejected' ? 'bad' : '') + '">' + QST[q.validationStatus] + '</span>' + (q.retiredAt ? ' <span class="badge">متقاعد</span>' : '') + '</td><td>' + (servable(q) ? 'نعم' : 'لا') + '</td><td>v' + q.version + '</td><td>' + esc(q.validatedBy ? uname(q.validatedBy) : '—') + '</td><td class="small">' + esc(q.rejectionReason || '') + '</td>' +
        '<td><a class="btn sm" href="#/admin/question/' + q.id + '">' + (q.validationStatus === 'rejected' ? 'تعديل وإعادة إرسال' : 'تعديل') + '</a><button class="btn sm" onclick="A.retire(' + q.id + ')">' + (q.retiredAt ? 'إعادة تفعيل' : 'تقاعد') + '</button></td></tr>';
    }).join('') + '</table>';
  }
  var QF = { status: '', subject: '', type: '' };
  function vAdminQuestions() {
    var qs = S.questions.filter(function (q) { return (!QF.status || q.validationStatus === QF.status) && (!QF.subject || q.subjectId == QF.subject) && (!QF.type || q.type === QF.type); });
    var sel = function (k, opts) { return '<select onchange="A.qf(\'' + k + '\',this.value)"><option value="">الكل</option>' + opts.map(function (o) { return '<option value="' + o[0] + '"' + (QF[k] == o[0] ? ' selected' : '') + '>' + esc(o[1]) + '</option>'; }).join('') + '</select>'; };
    return '<h2>بنك الأسئلة (' + qs.length + ')</h2><div class="card row filters">الحالة ' + sel('status', Object.keys(QST).map(function (k) { return [k, QST[k]]; })) + ' المادة ' + sel('subject', S.subjects.map(function (s) { return [s.id, s.name]; })) + ' النوع ' + sel('type', TYPES.map(function (t) { return [t, TYPE[t]]; })) +
      '<span class="small">لإضافة سؤال افتح الدرس من شجرة المحتوى</span></div>' + questionTable(qs);
  }

  // Question editor (draft kept in QE)
  var QE = null;
  function vAdminQuestion(p) {
    if (!QE || QE._key !== location.hash) {
      if (p.lid) {
        var l0 = find('lessons', p.lid); if (!l0) return notFound();
        QE = { id: null, lessonId: l0.id, subjectId: l0.subjectId, type: 'mcq', stem: '', difficulty: 'medium', objectiveId: l0.objectives[0] ? l0.objectives[0].id : null, explanation: '', body: { options: ['', '', '', ''] }, gradingSpec: { correct: 0 }, maxScore: 1 };
      } else { var q0 = find('questions', p.id); if (!q0) return notFound(); QE = clone(q0); }
      QE._key = location.hash;
    }
    var q = QE, l = find('lessons', q.lessonId), b = q.body, sp = q.gradingSpec;
    var f = function (label, inner) { return '<label>' + label + inner + '</label>'; };
    var inp = function (key, val) { return '<input value="' + esc(val) + '" oninput="A.qe(\'' + key + '\',this.value)">'; };
    var ta = function (key, val, rows) { return '<textarea rows="' + (rows || 3) + '" oninput="A.qe(\'' + key + '\',this.value)">' + esc(val) + '</textarea>'; };
    var h = crumbs([['الأسئلة', '#/admin/questions'], [l.name, '#/admin/lesson/' + l.id], [q.id ? 'سؤال #' + q.id : 'سؤال جديد']]);
    h += '<h2>' + (q.id ? 'تحرير سؤال #' + q.id + ' <span class="badge">' + QST[q.validationStatus] + '</span> <span class="badge">v' + q.version + '</span>' : 'سؤال جديد') + '</h2>';
    if (q.validationStatus === 'approved') h += '<div class="card warn small">تنبيه: تعديل نص السؤال أو الخيارات أو الإجابة سيعيده إلى "قيد المراجعة" ويزيد الإصدار. تعديل الصعوبة فقط لا يغيّر الحالة.</div>';
    if (q.validationStatus === 'rejected') h += '<div class="card warn">سبب الرفض: ' + esc(q.rejectionReason) + '</div>';
    h += '<div class="editor"><div class="card">';
    h += f('النوع', '<select onchange="A.qeType(this.value)"' + (q.id ? ' disabled' : '') + '>' + TYPES.map(function (t) { return '<option value="' + t + '"' + (t === q.type ? ' selected' : '') + '>' + TYPE[t] + (V2.indexOf(t) >= 0 ? ' (v2)' : '') + '</option>'; }).join('') + '</select>');
    h += f('نص السؤال' + (q.type === 'fill' ? ' (استخدم ____ للفراغ)' : ''), ta('stem', q.stem, 3));
    if (q.type === 'mcq' || q.type === 'multi') {
      h += f('الخيارات (خيار في كل سطر)', ta('options', b.options.join('\n'), 4));
      h += q.type === 'mcq' ? f('رقم الخيار الصحيح (1..)', inp('correct', sp.correct + 1)) : f('أرقام الخيارات الصحيحة (مثل 1,2,4)', inp('correctMulti', sp.correct.map(function (i) { return i + 1; }).join(',')));
    } else if (q.type === 'tf') {
      h += f('الإجابة الصحيحة', '<select onchange="A.qe(\'tf\',this.value)"><option value="true"' + (sp.correct ? ' selected' : '') + '>صح</option><option value="false"' + (!sp.correct ? ' selected' : '') + '>خطأ</option></select>');
    } else if (q.type === 'fill') {
      h += f('الإجابات المقبولة: سطر لكل فراغ، والبدائل مفصولة بـ |', ta('accepted', sp.accepted.map(function (a) { return a.join('|'); }).join('\n'), 3));
    } else if (q.type === 'short') {
      h += f('نوع الإجابة', '<select onchange="A.qe(\'kind\',this.value)"><option value="numeric"' + (sp.kind === 'numeric' ? ' selected' : '') + '>رقمية</option><option value="text"' + (sp.kind === 'text' ? ' selected' : '') + '>نصية</option></select>');
      if (sp.kind === 'numeric') h += '<div class="row">' + f('القيمة', inp('value', sp.value)) + f('السماحية', inp('tol', sp.tol)) + f('نوع السماحية', '<select onchange="A.qe(\'tolType\',this.value)"><option value="abs"' + (sp.tolType !== 'pct' ? ' selected' : '') + '>± مطلق</option><option value="pct"' + (sp.tolType === 'pct' ? ' selected' : '') + '>± %</option></select>') + f('الوحدة', inp('unit', b.unit || '')) + '</div>';
      else h += f('الإجابات المقبولة (سطر لكل إجابة)', ta('acceptedText', (sp.accepted || []).join('\n'), 3));
    } else if (q.type === 'essay') {
      h += f('الإجابة النموذجية', ta('modelAnswer', sp.modelAnswer, 3)) + f('الكلمات المفتاحية (مفصولة بفاصلة)', inp('keywords', sp.keywords.join(',')));
    } else if (q.type === 'math_steps') {
      h += f('الإجابة النهائية (البدائل مفصولة بـ |)', inp('finals', sp.finalAnswers.join('|'))) + '<p class="small">تصحيح الخطوات في الإصدار 2</p>';
    }
    h += '<div class="row">' + f('الصعوبة', '<select onchange="A.qe(\'difficulty\',this.value)">' + Object.keys(DIFF).map(function (d) { return '<option value="' + d + '"' + (d === q.difficulty ? ' selected' : '') + '>' + DIFF[d] + '</option>'; }).join('') + '</select>') +
      f('الهدف', '<select onchange="A.qe(\'objectiveId\',this.value)">' + l.objectives.map(function (o) { return '<option value="' + o.id + '"' + (o.id === q.objectiveId ? ' selected' : '') + '>' + esc(o.text) + '</option>'; }).join('') + '</select>') + f('الدرجة', inp('maxScore', q.maxScore)) + '</div>';
    h += f('الشرح (يظهر بعد الإجابة ويُمرَّر للمساعد)', ta('explanation', q.explanation, 2));
    h += '<button class="btn primary" onclick="A.qeSave()">' + (q.validationStatus === 'rejected' ? 'حفظ وإعادة الإرسال للمراجعة' : 'حفظ') + '</button></div>';
    h += '<div class="card"><b>معاينة الطالب + جرّب الإجابة</b><div id="qe-preview">' + qePreview() + '</div></div></div>';
    if (q.id) h += '<h3>السجل</h3>' + history(q.id);
    return h;
  }
  function qePreview() {
    try { return qBlock(QE, null, false, 'qp') + '<button class="btn" onclick="A.qeTry()">جرّب الإجابة</button><div id="qe-result"></div><div class="small">الإجابة الصحيحة: ' + esc(correctText(QE)) + '</div>'; }
    catch (e) { return '<p class="small">أكمل الحقول لعرض المعاينة.</p>'; }
  }

  function vAdminBlueprints() {
    var h = '<h2>نماذج الامتحانات</h2><p class="small">لا يمكن حفظ نموذج إذا كان عدد الأسئلة القابلة للعرض غير كافٍ لأي نوع.</p>';
    S.subjects.forEach(function (s) {
      h += '<h3>' + esc(s.name) + '</h3>' + bpEditor(find('blueprints', s.defaultBlueprintId), servableWhere(inSubject(s.id)), 'النموذج الافتراضي للمادة');
      subjectUnits(s.id).forEach(function (u) {
        var bp = find('blueprints', u.blueprintId);
        h += bp ? bpEditor(bp, servableWhere(inUnits([u.id])), 'وحدة: ' + u.name) : '<div class="card">وحدة: ' + esc(u.name) + ' — تستخدم النموذج الافتراضي. <button class="btn sm" onclick="A.bpCreate(' + u.id + ')">إنشاء نموذج للوحدة</button></div>';
      });
    });
    return h;
  }
  function bpEditor(bp, pool, title) {
    var sf = shortfall(pool, bp.typeCounts);
    return '<div class="card" id="bp-' + bp.id + '"><b>' + esc(title) + '</b><table><tr><th>النوع</th><th>العدد المطلوب</th><th>المتاح</th></tr>' + TYPES.map(function (t) {
      var have = pool.filter(function (q) { return q.type === t; }).length, need = +bp.typeCounts[t] || 0;
      return '<tr' + (need > have ? ' class="short"' : '') + '><td>' + TYPE[t] + '</td><td><input type="number" min="0" data-t="' + t + '" value="' + need + '" style="width:60px"></td><td>' + have + '</td></tr>';
    }).join('') + '</table><div class="row"><label>الزمن (دقيقة) <input type="number" class="bp-time" value="' + (bp.timeLimitMin || '') + '" style="width:70px"></label><label>درجة النجاح <input type="number" class="bp-pass" value="' + bp.passMark + '" style="width:70px"></label>' +
      '<button class="btn primary" onclick="A.bpSave(\'' + bp.id + '\')">حفظ</button></div>' + (sf.length ? '<div class="warn small">عجز حالي: ' + shortfallMsg(sf) + '</div>' : '') + '<div class="small" id="bpmsg-' + bp.id + '"></div></div>';
  }

  function vAdminUsers() {
    var h = '<h2>المستخدمون</h2><h3>الطلاب</h3><table><tr><th>الاسم</th><th>الباقة</th><th>الحالة</th><th>المحاولات</th><th>الإتقان</th><th></th></tr>';
    S.users.filter(function (u) { return u.role === 'student'; }).forEach(function (u) {
      var m = attMap(u.id);
      h += '<tr><td>' + esc(u.name) + '</td><td>' + planLabel(u.id) + '</td><td>' + (u.status === 'active' ? 'نشط' : 'موقوف') + '</td><td>' + S.attempts.filter(function (a) { return a.studentId === u.id; }).length + '</td><td>' + pct(mastery(m, servableWhere())) + '</td>' +
        '<td><a class="btn sm" href="#/admin/student/' + u.id + '">عرض التقدم</a><button class="btn sm" onclick="A.toggleUser(\'' + u.id + '\')">' + (u.status === 'active' ? 'إيقاف' : 'تفعيل') + '</button>' +
        (hasPlan(u.id, 'base') ? '' : '<button class="btn sm" onclick="A.grant(\'' + u.id + '\',\'base\')">منح الأساسية</button>') + (hasPlan(u.id, 'base') && !hasPlan(u.id, 'ask') ? '<button class="btn sm" onclick="A.grant(\'' + u.id + '\',\'ask\')">منح اسأل معلّم</button>' : '') + '</td></tr>';
    });
    h += '</table><h3>المعلّمون <button class="btn sm" onclick="A.invite(\'teacher\')">+ دعوة معلّم</button></h3><table><tr><th>الاسم</th><th>المواد المسندة</th><th>الحالة</th><th></th></tr>';
    S.users.filter(function (u) { return u.role === 'teacher'; }).forEach(function (u) {
      h += '<tr><td>' + esc(u.name) + '</td><td>' + S.subjects.map(function (s) { return '<label><input type="checkbox" ' + (teacherCan(u, s.id) ? 'checked' : '') + ' onchange="A.assign(\'' + u.id + '\',' + s.id + ',this.checked)"> ' + esc(s.name) + '</label>'; }).join(' ') + '</td><td>' + (u.status === 'active' ? 'نشط' : 'معطّل') + '</td><td><button class="btn sm" onclick="A.toggleUser(\'' + u.id + '\')">' + (u.status === 'active' ? 'تعطيل' : 'تفعيل') + '</button></td></tr>';
    });
    var admins = S.users.filter(function (u) { return u.role === 'admin'; }), activeAdmins = admins.filter(function (u) { return u.status === 'active'; }).length;
    h += '</table><h3>المديرون <button class="btn sm" onclick="A.invite(\'admin\')">+ دعوة مدير</button></h3><table><tr><th>الاسم</th><th>الحالة</th><th></th></tr>' + admins.map(function (u) {
      var lastOne = u.status === 'active' && activeAdmins <= 1;
      return '<tr><td>' + esc(u.name) + '</td><td>' + (u.status === 'active' ? 'نشط' : 'معطّل') + '</td><td><button class="btn sm" ' + (lastOne ? 'disabled title="يجب أن يبقى مدير نشط واحد على الأقل"' : '') + ' onclick="A.toggleUser(\'' + u.id + '\')">' + (u.status === 'active' ? 'تعطيل' : 'تفعيل') + '</button>' + (lastOne ? ' <span class="small">آخر مدير نشط</span>' : '') + '</td></tr>';
    }).join('') + '</table>';
    return h;
  }
  var AF = '';
  function vAdminAudit() {
    var rows = S.audit.filter(function (a) { return !AF || a.entity === AF; }).slice().sort(function (a, b) { return b.at - a.at; });
    var ents = []; S.audit.forEach(function (a) { if (ents.indexOf(a.entity) < 0) ents.push(a.entity); });
    return '<h2>سجل التدقيق (' + rows.length + ')</h2><select onchange="A.af(this.value)"><option value="">كل الكيانات</option>' + ents.map(function (e) { return '<option' + (AF === e ? ' selected' : '') + '>' + e + '</option>'; }).join('') + '</select>' +
      '<table><tr><th>الوقت</th><th>المنفّذ</th><th>الإجراء</th><th>الكيان</th><th>المعرّف</th><th>التفاصيل</th></tr>' + rows.slice(0, 300).map(function (a) {
        return '<tr><td>' + fmt(a.at) + '</td><td>' + esc(uname(a.actorId)) + '</td><td>' + esc(a.action) + '</td><td>' + esc(a.entity) + '</td><td>' + esc(a.entityId) + '</td><td class="small">' + esc(a.diff ? JSON.stringify(a.diff) : '') + '</td></tr>';
      }).join('') + '</table>';
  }
  function vAdminExport() {
    return '<h2>تصدير بيانات التدريب (JSONL)</h2><div class="card"><p>كل ملف يحذف البيانات الشخصية: معرّف الطالب مُجزّأ (hash) ولا تُصدَّر الأسماء أو الهواتف أو البريد.</p>' +
      '<div class="row"><button class="btn" onclick="A.exportJsonl(\'ask\')">اسأل معلّم (' + S.threads.length + ')</button><button class="btn" onclick="A.exportJsonl(\'avatar\')">المساعد (' + S.avatarConvs.length + ')</button><button class="btn" onclick="A.exportJsonl(\'attempts\')">المحاولات (' + S.attempts.length + ')</button></div>' +
      '<p class="small">سجل التدريب لاسأل معلّم نصّي فقط (نص السؤال + السياق + التفريغ النهائي + التقييم).</p></div>';
  }
  function notFound() { return '<div class="card warn">الصفحة غير موجودة أو غير متاحة. <a href="#/' + role() + '">العودة</a></div>'; }

  /* ================= 11. ACTIONS ================= */
  function rerender() { save(); render(); }
  function paywall(msg) {
    modal('<h3>' + (msg ? 'ميزة للمشتركين' : 'انتهى الحد المجاني') + '</h3><p>' + (msg || 'وصلت إلى الحد اليومي للباقة المجانية (' + FREE_DAILY + ' أسئلة تدريب). اشترك في الباقة الأساسية للتدريب بلا حدود.') + '</p>' +
      '<div class="row"><button class="btn primary" onclick="A.checkout(\'base\')">اشترك</button><button class="btn" onclick="A.closeModal()">لاحقًا</button></div>');
  }
  var A = window.A = {
    reset: function () { if (!confirm('إعادة جميع البيانات إلى الحالة الأولية؟')) return; S = clone(window.SEED); QE = null; save(); location.hash = '#/student'; render(); },
    setRole: function (r) { S.ui.role = r; save(); location.hash = '#/' + r; },
    setUser: function (id) { S.ui.users[role()] = id; S.ui.avatar.open = false; save(); location.hash = '#/' + role(); render(); },
    closeModal: function () { $('modal').innerHTML = ''; },

    // --- quiz
    startQuiz: function (lid, n) {
      var sid = me().id, l = find('lessons', lid);
      if (me().status === 'suspended') return alert('الحساب موقوف');
      if (lessonLocked(sid, l)) return paywall('هذا الدرس متاح للمشتركين فقط.');
      if (isFree(sid) && quizUsedToday(sid) >= FREE_DAILY) return paywall();
      var qs = selectQuiz(sid, servableWhere(inLesson(lid)), n);
      if (!qs.length) return alert('لا توجد أسئلة متاحة في هذا الدرس بعد.');
      var ses = { id: uid('ses'), kind: 'quiz', studentId: sid, subjectId: l.subjectId, lessonId: lid, questionIds: qs.map(function (q) { return q.id; }), idx: 0, results: {}, startedAt: Date.now(), submittedAt: null };
      S.sessions.push(ses); save(); location.hash = '#/student/quiz/' + ses.id;
    },
    quizCheck: function (id) {
      var ses = find('sessions', id), q = find('questions', ses.questionIds[ses.idx]), sid = ses.studentId;
      if (isFree(sid) && quizUsedToday(sid) >= FREE_DAILY) return paywall();
      var a = readAns(q, 'qz'); if (isEmpty(q, a)) return alert('أدخل إجابتك أولًا');
      var g = grade(q, a);
      ses.results[q.id] = { answer: a, score: g.score, normalised: g.normalised, feedback: g.feedback };
      recordAttempt(ses, q, a, g, Date.now() - qShownAt); ses.scorePct = sessionScore(ses); rerender();
    },
    quizNext: function (id) { var ses = find('sessions', id); ses.idx++; if (ses.idx >= ses.questionIds.length) { ses.submittedAt = Date.now(); ses.scorePct = sessionScore(ses); save(); location.hash = '#/student/quiz-result/' + id; } else rerender(); },
    quizFinish: function (id) { var ses = find('sessions', id); ses.questionIds = ses.questionIds.filter(function (q) { return ses.results[q]; }); ses.idx = ses.questionIds.length; ses.submittedAt = Date.now(); ses.scorePct = sessionScore(ses); save(); location.hash = '#/student/quiz-result/' + id; },

    // --- exams
    startExam: function (unitIds, size) {
      var sid = me().id;
      if (isFree(sid)) return paywall('الامتحانات متاحة في الباقة الأساسية.');
      var ex = activeExam(sid); if (ex) { alert('لديك امتحان جارٍ بالفعل.'); location.hash = '#/student/exam/' + ex.id; return; }
      var multi = unitIds.length > 1, u0 = find('units', unitIds[0]), bp = multi ? find('blueprints', find('subjects', u0.subjectId).defaultBlueprintId) : blueprintForUnit(u0);
      var counts = multi ? mergeBlueprints(unitIds, size) : bp.typeCounts, pool = servableWhere(inUnits(unitIds));
      var g = genExam(sid, pool, counts);
      if (g.shortfall.length) { modal('<h3>لا يمكن إنشاء الامتحان</h3><p>' + shortfallMsg(g.shortfall) + '</p><button class="btn" onclick="A.closeModal()">حسنًا</button>'); return; }
      var ses = { id: uid('ses'), kind: multi ? 'multi' : 'unit', studentId: sid, subjectId: u0.subjectId, unitIds: unitIds, questionIds: g.questions.map(function (q) { return q.id; }), answers: {}, results: {},
        startedAt: Date.now(), submittedAt: null, timeLimitMin: multi ? Math.round(size * 2) : bp.timeLimitMin, passMark: bp.passMark, size: size,
        title: multi ? 'امتحان متعدد: ' + unitIds.map(function (i) { return find('units', i).name; }).join(' + ') : 'امتحان ' + u0.name };
      S.sessions.push(ses); save(); location.hash = '#/student/exam/' + ses.id;
    },
    examSave: function (id, qid) { var ses = find('sessions', id); if (!ses || ses.submittedAt) return; ses.answers[qid] = readAns(find('questions', qid), 'ex'); save(); var s = $('saved'); if (s) s.textContent = 'تم الحفظ ' + new Date().toLocaleTimeString('ar-EG-u-nu-latn'); },
    examSubmit: function (id) {
      var ses = find('sessions', id), blank = ses.questionIds.filter(function (q) { return isEmpty(find('questions', q), ses.answers[q]); }).length;
      if (!confirm('تسليم الامتحان؟' + (blank ? ' (' + blank + ' سؤال بدون إجابة)' : ''))) return;
      clearInterval(TIMER); submitExam(ses); location.hash = '#/student/exam-result/' + id;
    },
    retake: function (id) { var s = find('sessions', id); A.startExam(s.unitIds, s.size || 10); },
    mxSubject: function (v) { MX.subject = +v; MX.units = []; location.hash = '#/student/multi-exam/' + v; },
    mxUnit: function (id, on) { MX.units = MX.units.filter(function (x) { return x !== id; }); if (on) MX.units.push(id); render(); },
    mxSize: function (n) { MX.size = n; render(); },
    histFilter: function (v) { S.ui.histFilter = v; rerender(); },

    // --- avatar
    avatarOpen: function (ctx) {
      var sid = me().id, ses = ctx.sessionId ? find('sessions', ctx.sessionId) : null;
      if (ses && ses.results && ses.results[ctx.questionId]) { ctx.answer = ses.results[ctx.questionId].answer; ctx.hasAnswer = true; }
      var conv = { id: uid('av'), studentId: sid, context: ctx, model: 'avatar-sim-0', promptVersion: 'p-v1', startedAt: Date.now(), messages: [] };
      conv.messages.push({ role: 'assistant', text: ctx.exam || activeExam(sid) ? avatarReply(conv, '') : ctx.questionId ? avatarReply(conv, '') : ctx.lessonId ? 'أهلًا! أنا مساعدك في درس "' + find('lessons', ctx.lessonId).name + '". اسألني عن أي جزء في الشرح أو الأهداف.' : 'أهلًا! اختر درسًا لأبدأ.', at: Date.now() });
      S.avatarConvs.push(conv); S.ui.avatar = { open: true, convId: conv.id }; rerender();
    },
    avatarLesson: function (lid) { var c = find('avatarConvs', S.ui.avatar.convId); c.context.lessonId = lid; c.messages.push({ role: 'assistant', text: 'تم اختيار درس "' + find('lessons', lid).name + '". ما سؤالك؟', at: Date.now() }); rerender(); },
    avatarSend: function () {
      var inEl = $('avin'), t = inEl.value.trim(); if (!t) return;
      var c = find('avatarConvs', S.ui.avatar.convId);
      c.messages.push({ role: 'user', text: t, at: Date.now() });
      c.messages.push({ role: 'assistant', text: avatarReply(c, t), at: Date.now() });
      rerender(); var m = $('avmsgs'); if (m) m.scrollTop = m.scrollHeight;
    },
    avatarClose: function () { S.ui.avatar.open = false; rerender(); },

    // --- ask a teacher
    askSubmit: function (lid, qid) {
      var sid = me().id, text = $('ask-text').value.trim(); if (!text) return alert('اكتب سؤالك');
      if (!lid) lid = +$('ask-lesson').value;
      var l = find('lessons', lid), now = Date.now();
      var th = { id: uid('th'), studentId: sid, teacherId: null, subjectId: l.subjectId, context: { unitId: l.unitId, lessonId: lid, questionId: qid }, status: 'open', followupUsed: false, attachedImage: $('ask-img').checked,
        submittedAt: now, slaDueAt: now + 24 * H, closedAt: null, rating: null, messages: [{ id: uid('m'), senderId: sid, kind: 'text', text: text, at: now }] };
      S.threads.push(th); save(); location.hash = '#/student/thread/' + th.id;
    },
    askFollowup: function (id) { var th = find('threads', id), t = $('fu-text').value.trim(); if (!t) return; th.messages.push({ id: uid('m'), senderId: th.studentId, kind: 'text', text: t, at: Date.now() }); th.followupUsed = true; th.status = 'open'; th.slaDueAt = Date.now() + 24 * H; rerender(); },
    askRate: function (id, n) { var th = find('threads', id); th.rating = n; th.status = 'closed'; th.closedAt = Date.now(); rerender(); },
    claim: function (id) { var th = find('threads', id); if (th.teacherId) return alert('تم استلامه بالفعل'); th.teacherId = me().id; audit('claim', 'thread', id); rerender(); },
    replyKind: function (k) { $('voicebox').className = k === 'voice' ? '' : 'hidden'; $('reply-text').placeholder = k === 'voice' ? 'نص التفريغ الصوتي' : 'اكتب ردك'; },
    fakeRecord: function () { $('fakeaudio').textContent = '▶ ━━━━━━━━━━ 0:35 (تسجيل - محاكاة)'; var t = $('reply-text'); if (!t.value) t.value = '(تفريغ تلقائي) أهلًا، خلّينا نراجع الفكرة خطوة بخطوة: ارجع لملخص الدرس وطبّق القانون على المثال.'; },
    reply: function (id) {
      var th = find('threads', id), t = $('reply-text').value.trim(); if (!t) return alert('اكتب الرد أو التفريغ');
      var voice = document.querySelector('input[name=rk]:checked').value === 'voice';
      th.messages.push({ id: uid('m'), senderId: me().id, kind: voice ? 'voice' : 'text', text: t, audioSec: voice ? 35 : null, at: Date.now() });
      th.status = th.followupUsed ? 'answered-final' : 'answered';
      audit('reply', 'thread', id, { kind: voice ? 'voice' : 'text' }); rerender();
    },
    inboxF: function (f) { INBOX_F = f; render(); },

    // --- subscription (fake Paymob)
    checkout: function (plan) {
      var sid = me().id;
      if (plan === 'ask' && !hasPlan(sid, 'base')) return alert('إضافة "اسأل معلّم" تتطلب الباقة الأساسية.');
      modal('<h3>Paymob checkout (محاكاة)</h3><p>الباقة: ' + PLAN[plan] + ' — المبلغ: ' + PRICES[plan] + ' ج.م</p><div class="fake-card">رقم البطاقة: •••• •••• •••• ••••<br>أو محفظة إلكترونية</div>' +
        '<p class="small">في النظام الحقيقي لا يفعّل العميل الاشتراك؛ Paymob يرسل Webhook موقّع (HMAC) للخادم.</p><div class="row"><button class="btn primary" onclick="A.pay(\'' + plan + '\',true)">نجاح الدفع</button><button class="btn danger" onclick="A.pay(\'' + plan + '\',false)">فشل الدفع</button><button class="btn" onclick="A.closeModal()">إلغاء</button></div>');
    },
    pay: function (plan, ok) {
      var sid = me().id, now = Date.now(), txn = 'txn_' + Math.floor(Math.random() * 1e6);
      var p = { id: uid('pay'), studentId: sid, plan: plan, amount: PRICES[plan], currency: 'EGP', status: ok ? 'success' : 'failed', paymobTxnId: txn, rawWebhook: { type: 'TRANSACTION', success: ok, amount_cents: PRICES[plan] * 100, hmac: 'verified (simulated)' }, at: now };
      S.payments.push(p);
      // simulated webhook handler = the only place entitlement changes
      if (ok) S.subscriptions.push({ id: uid('sub'), studentId: sid, plan: plan, status: 'active', startedAt: now, periodEnd: now + 30 * DAY, paymobRef: txn });
      audit(ok ? 'webhook_payment_success' : 'webhook_payment_failed', 'payment', p.id, { plan: plan, txn: txn }, 'paymob-webhook');
      A.closeModal(); rerender();
      modal('<h3>' + (ok ? 'تم الدفع بنجاح' : 'فشل الدفع') + '</h3><p>' + (ok ? 'استقبلنا Webhook من Paymob وتم تفعيل باقة ' + PLAN[plan] + '.' : 'لم يتم تفعيل الاشتراك.') + '</p><button class="btn" onclick="A.closeModal()">حسنًا</button>');
    },
    cancelSub: function (id) { if (!confirm('إلغاء الاشتراك؟')) return; var s = find('subscriptions', id); s.status = 'cancelled'; s.cancelledAt = Date.now(); if (s.plan === 'base') activeSubs(s.studentId).forEach(function (x) { if (x.plan === 'ask') { x.status = 'cancelled'; x.cancelledAt = Date.now(); } }); audit('cancel', 'subscription', id); rerender(); },

    // --- teacher validation (Rule 3)
    tf: function (k, v) { TF[k] = v; render(); },
    approve: function (id) {
      var t = me(), q = find('questions', id);
      if (t.role !== 'teacher' || !teacherCan(t, q.subjectId)) return alert('غير مسموح');
      var d = $('t-diff').value, diff = null;
      if (d !== q.difficulty) { diff = { difficulty: [q.difficulty, d] }; q.difficulty = d; }
      q.validationStatus = 'approved'; q.validatedBy = t.id; q.validatedAt = Date.now(); q.rejectionReason = null;
      audit('approve', 'question', id, diff); save(); location.hash = '#/teacher';
    },
    reject: function (id) {
      var t = me(), q = find('questions', id), r = $('t-reason').value.trim();
      if (t.role !== 'teacher' || !teacherCan(t, q.subjectId)) return alert('غير مسموح');
      if (!r) { alert('سبب الرفض إلزامي'); $('t-reason').focus(); return; }
      q.validationStatus = 'rejected'; q.validatedBy = t.id; q.validatedAt = Date.now(); q.rejectionReason = r;
      audit('reject', 'question', id, { reason: r }); save(); location.hash = '#/teacher';
    },

    // --- admin: dashboard / filters
    df: function (k, v) { DF[k] = v; render(); },
    qf: function (k, v) { QF[k] = v; render(); },
    af: function (v) { AF = v; render(); },

    // --- admin: content tree
    addSubject: function () { var n = prompt('اسم المادة'); if (!n) return; var id = Math.max.apply(null, S.subjects.map(function (s) { return s.id; })) + 1; var bpId = 'bp-s' + id;
      S.blueprints.push({ id: bpId, name: 'النموذج الافتراضي - ' + n, scope: 'subject', refId: id, typeCounts: { mcq: 0 }, timeLimitMin: 30, passMark: 50 });
      S.subjects.push({ id: id, name: n, order: S.subjects.length + 1, defaultBlueprintId: bpId }); audit('create', 'subject', id, { name: n }); rerender(); },
    addUnit: function (sid) { var n = prompt('اسم الوحدة'); if (!n) return; var id = Math.max.apply(null, S.units.map(function (u) { return u.id; })) + 1; S.units.push({ id: id, subjectId: sid, name: n, order: subjectUnits(sid).length + 1, blueprintId: null }); audit('create', 'unit', id, { name: n }); rerender(); },
    addLesson: function (uId) { var n = prompt('اسم الدرس'); if (!n) return; var u = find('units', uId), id = Math.max.apply(null, S.lessons.map(function (l) { return l.id; })) + 1;
      S.lessons.push({ id: id, unitId: uId, subjectId: u.subjectId, name: n, order: unitLessons(uId).length + 1, state: 'draft', explanation: '', objectives: [{ id: 'o' + id + '-1', text: 'هدف 1' }], summary: '', publishedAt: null }); audit('create', 'lesson', id, { name: n }); save(); location.hash = '#/admin/lesson/' + id; },
    rename: function (coll, id) { var o = find(coll, id), n = prompt('الاسم الجديد', o.name); if (!n || n === o.name) return; audit('rename', coll.slice(0, -1), id, { name: [o.name, n] }); o.name = n; rerender(); },
    move: function (coll, id, dir) {
      var o = find(coll, id), sibs = S[coll].filter(function (x) { return coll === 'subjects' || (coll === 'units' ? x.subjectId === o.subjectId : x.unitId === o.unitId); }).sort(byOrder);
      var i = sibs.indexOf(o), j = i + dir; if (j < 0 || j >= sibs.length) return;
      sibs.splice(i, 1); sibs.splice(j, 0, o); sibs.forEach(function (x, k) { x.order = k + 1; });
      audit('reorder', coll.slice(0, -1), id, { dir: dir }); rerender();
    },
    del: function (coll, id) {
      var has = coll === 'subjects' ? subjectUnits(id).length : coll === 'units' ? unitLessons(id).length : S.questions.filter(function (q) { return q.lessonId == id; }).length;
      if (has) return alert('لا يمكن الحذف لوجود محتوى تابع. ' + (coll === 'lessons' ? 'استخدم الأرشفة بدلًا من الحذف.' : 'احذف أو انقل المحتوى التابع أولًا.'));
      if (!confirm('حذف العنصر؟')) return;
      S[coll] = S[coll].filter(function (x) { return x.id != id; }); audit('delete', coll.slice(0, -1), id); rerender();
    },
    lessonState: function (id, st) { var l = find('lessons', id), old = l.state; l.state = st; if (st === 'published') l.publishedAt = Date.now(); audit(st === 'published' ? 'publish' : st === 'archived' ? 'archive' : 'unpublish', 'lesson', id, { state: [old, st] }); rerender(); },
    saveLesson: function (id) {
      var l = find('lessons', id), before = clone(l);
      l.name = $('le-name').value; l.explanation = $('le-exp').value; l.summary = $('le-sum').value; l.videoUrl = $('le-video').value;
      var lines = $('le-obj').value.split('\n').map(function (s) { return s.trim(); }).filter(Boolean);
      l.objectives = lines.map(function (t, i) { var o = before.objectives[i]; return { id: o ? o.id : 'o' + id + '-' + Date.now().toString(36) + i, text: t }; });
      var changed = ['name', 'explanation', 'summary', 'videoUrl'].filter(function (k) { return before[k] !== l[k]; });
      if (JSON.stringify(before.objectives) !== JSON.stringify(l.objectives)) changed.push('objectives');
      audit('update', 'lesson', id, { fields: changed }); rerender(); alert('تم الحفظ');
    },
    retire: function (id) { var q = find('questions', id); q.retiredAt = q.retiredAt ? null : Date.now(); audit(q.retiredAt ? 'retire' : 'unretire', 'question', id); rerender(); },

    // --- admin: question editor (Rule 2)
    qeType: function (t) {
      QE.type = t; var dflt = { mcq: [{ options: ['', '', '', ''] }, { correct: 0 }], multi: [{ options: ['', '', '', ''] }, { correct: [0] }], tf: [{}, { correct: true }], fill: [{ blanks: 1 }, { accepted: [['']] }],
        short: [{ kind: 'numeric', unit: '' }, { kind: 'numeric', value: 0, tol: 0, tolType: 'abs' }], essay: [{}, { keywords: [], modelAnswer: '' }], math_steps: [{}, { finalAnswers: [''] }] }[t];
      QE.body = dflt[0]; QE.gradingSpec = dflt[1]; QE.maxScore = V2.indexOf(t) >= 0 ? 5 : 1; render();
    },
    qe: function (k, v) {
      var b = QE.body, sp = QE.gradingSpec, lines = function (s) { return s.split('\n'); };
      switch (k) {
        case 'options': b.options = lines(v); break;
        case 'correct': sp.correct = (parseInt(v, 10) || 1) - 1; break;
        case 'correctMulti': sp.correct = v.split(/[,،\s]+/).map(function (x) { return parseInt(x, 10) - 1; }).filter(function (x) { return x >= 0; }); break;
        case 'tf': sp.correct = v === 'true'; break;
        case 'accepted': sp.accepted = lines(v).map(function (l) { return l.split('|').map(function (s) { return s.trim(); }); }); b.blanks = sp.accepted.length; break;
        case 'kind': sp.kind = b.kind = v; if (v === 'text' && !sp.accepted) sp.accepted = ['']; if (v === 'numeric' && sp.value == null) { sp.value = 0; sp.tol = 0; sp.tolType = 'abs'; } render(); return;
        case 'value': sp.value = parseFloat(v); break;
        case 'tol': sp.tol = parseFloat(v) || 0; break;
        case 'tolType': sp.tolType = v; break;
        case 'unit': b.unit = v; break;
        case 'acceptedText': sp.accepted = lines(v).map(function (s) { return s.trim(); }).filter(Boolean); break;
        case 'modelAnswer': sp.modelAnswer = v; break;
        case 'keywords': sp.keywords = v.split(/[,،]/).map(function (s) { return s.trim(); }).filter(Boolean); break;
        case 'finals': sp.finalAnswers = v.split('|').map(function (s) { return s.trim(); }); break;
        case 'maxScore': QE.maxScore = parseFloat(v) || 1; break;
        default: QE[k] = v;
      }
      var pv = $('qe-preview'); if (pv) pv.innerHTML = qePreview();
    },
    qeTry: function () { var a = readAns(QE, 'qp'), g = grade(QE, a); $('qe-result').innerHTML = feedbackBox(QE, g); },
    qeSave: function () {
      var q = QE, now = Date.now();
      if (!q.stem.trim()) return alert('نص السؤال مطلوب');
      if (q.type === 'fill' && q.stem.split(/_{3,}/).length - 1 !== q.gradingSpec.accepted.length) return alert('عدد الفراغات في النص لا يساوي عدد أسطر الإجابات المقبولة');
      var clean = clone(q); delete clean._key;
      if (!q.id) {
        clean.id = Math.max.apply(null, S.questions.map(function (x) { return x.id; })) + 1;
        Object.assign(clean, { version: 1, validationStatus: 'pending', validatedBy: null, validatedAt: null, rejectionReason: null, tags: [], createdAt: now, retiredAt: null });
        S.questions.push(clean); audit('create', 'question', clean.id, { version: 1 });
      } else {
        var old = find('questions', q.id);
        var sig = function (x) { return JSON.stringify([x.stem, x.body, x.gradingSpec, x.maxScore]); }; // content fields only (tags/difficulty/explanation do not reset)
        var contentChanged = sig(old) !== sig(clean), wasRejected = old.validationStatus === 'rejected', diff = {};
        ['difficulty', 'objectiveId'].forEach(function (k) { if (old[k] !== clean[k]) diff[k] = [old[k], clean[k]]; });
        if (contentChanged) {
          S.revisions.push({ questionId: old.id, version: old.version, snapshot: clone(old), editedBy: me().id, editedAt: now });
          diff.content = 'changed'; diff.version = [old.version, old.version + 1]; clean.version = old.version + 1;
        }
        if (contentChanged || wasRejected) {
          if (old.validationStatus !== 'pending') diff.status = [old.validationStatus, 'pending'];
          clean.validationStatus = 'pending'; clean.validatedBy = null; clean.validatedAt = null; clean.createdAt = now;
          clean.rejectionReason = null;
        }
        Object.assign(old, clean);
        audit(wasRejected ? 'edit_and_resubmit' : 'update', 'question', old.id, diff);
        clean = old;
      }
      QE = null; save(); location.hash = '#/admin/question/' + clean.id; render();
      alert(clean.validationStatus === 'pending' ? 'تم الحفظ — السؤال الآن قيد المراجعة (v' + clean.version + ')' : 'تم الحفظ');
    },

    // --- admin: blueprints (Rule 7 / §10.2)
    bpSave: function (id) {
      var bp = find('blueprints', id), el = $('bp-' + id), counts = {};
      Array.prototype.forEach.call(el.querySelectorAll('input[data-t]'), function (i) { var n = parseInt(i.value, 10) || 0; if (n) counts[i.getAttribute('data-t')] = n; });
      var pool = bp.scope === 'subject' ? servableWhere(inSubject(bp.refId)) : servableWhere(inUnits([bp.refId])), sf = shortfall(pool, counts);
      if (sf.length) { $('bpmsg-' + id).innerHTML = '<span class="red">لم يتم الحفظ — عجز: ' + shortfallMsg(sf) + '</span>'; return; }
      if (!Object.keys(counts).length) { $('bpmsg-' + id).innerHTML = '<span class="red">يجب تحديد سؤال واحد على الأقل</span>'; return; }
      var before = clone(bp);
      bp.typeCounts = counts; bp.timeLimitMin = parseInt(el.querySelector('.bp-time').value, 10) || null; bp.passMark = parseInt(el.querySelector('.bp-pass').value, 10) || 50;
      audit('update', 'blueprint', id, { before: before.typeCounts, after: counts }); rerender();
    },
    bpCreate: function (uId) { var u = find('units', uId), id = 'bp-u' + uId; S.blueprints.push({ id: id, name: 'امتحان ' + u.name, scope: 'unit', refId: uId, typeCounts: { mcq: 1 }, timeLimitMin: 20, passMark: 50 }); u.blueprintId = id; audit('create', 'blueprint', id); rerender(); },

    // --- admin: users
    toggleUser: function (id) { var u = find('users', id); if (u.role === 'admin' && u.status === 'active' && S.users.filter(function (x) { return x.role === 'admin' && x.status === 'active'; }).length <= 1) return alert('يجب أن يبقى مدير نشط واحد على الأقل');
      u.status = u.status === 'active' ? 'suspended' : 'active'; audit(u.status === 'active' ? 'activate' : 'suspend', 'user', id); rerender(); },
    grant: function (id, plan) { var now = Date.now(); S.subscriptions.push({ id: uid('sub'), studentId: id, plan: plan, status: 'active', startedAt: now, periodEnd: now + 30 * DAY, paymobRef: 'complimentary' }); audit('grant_complimentary', 'subscription', id, { plan: plan }); rerender(); },
    assign: function (tid, sid, on) { var u = find('users', tid); u.subjects = u.subjects.filter(function (x) { return x !== sid; }); if (on) u.subjects.push(sid); audit(on ? 'assign_subject' : 'unassign_subject', 'user', tid, { subjectId: sid }); rerender(); },
    invite: function (r) { var n = prompt('اسم ' + ROLE[r]); if (!n) return; var id = r[0] + Date.now().toString(36); S.users.push({ id: id, role: r, name: n, subjects: r === 'teacher' ? [] : undefined, status: 'active', joinedAt: Date.now() }); audit('invite', 'user', id, { role: r }); rerender(); },

    // --- admin: training export (§13)
    exportJsonl: function (kind) {
      var rows = [];
      if (kind === 'ask') S.threads.forEach(function (t) { rows.push({ thread_id: t.id, student: hashId(t.studentId), subject_id: t.subjectId, context: t.context, submitted_at: new Date(t.submittedAt).toISOString(), messages: t.messages.map(function (m) { return { from: m.senderId === t.studentId ? 'student' : 'teacher', kind: m.kind, text: m.text, at: new Date(m.at).toISOString() }; }), rating: t.rating }); });
      if (kind === 'avatar') S.avatarConvs.forEach(function (c) { rows.push({ conversation_id: c.id, student: hashId(c.studentId), context: c.context, model: c.model, prompt_version: c.promptVersion, messages: c.messages.map(function (m) { return { role: m.role, text: m.text, at: new Date(m.at).toISOString() }; }) }); });
      if (kind === 'attempts') S.attempts.forEach(function (a) { var q = find('questions', a.questionId); rows.push({ attempt_id: a.id, student: hashId(a.studentId), session_kind: a.kind, question_id: a.questionId, question_version: a.questionVersion, type: q && q.type, subject_id: q && q.subjectId, lesson_id: q && q.lessonId, answer: a.answer, score: a.score, normalised: a.normalised, graded_by: a.gradedBy, time_ms: a.timeMs, at: new Date(a.at).toISOString() }); });
      var blob = new Blob([rows.map(function (r) { return JSON.stringify(r); }).join('\n') + '\n'], { type: 'application/x-ndjson' });
      var a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = 'elmanhg-' + kind + '-' + dayKey(Date.now()) + '.jsonl';
      document.body.appendChild(a); a.click(); setTimeout(function () { URL.revokeObjectURL(a.href); a.remove(); }, 500);
      audit('export', 'training_data', kind, { rows: rows.length }); save();
    }
  };

  /* ================= 12. ROUTER & BOOT ================= */
  var ROUTES = [
    ['student', vStudentHome], ['student/subject/:id', vSubject], ['student/unit/:id', vUnit], ['student/lesson/:id', vLesson], ['student/lesson/:id/:tab', vLesson],
    ['student/quiz/:id', vQuiz], ['student/quiz-result/:id', vQuizResult], ['student/exam-start/:id', vExamStart], ['student/exam/:id', vExam], ['student/exam-result/:id', vExamResult],
    ['student/multi-exam', vMultiExam], ['student/multi-exam/:id', vMultiExam], ['student/progress', vProgress], ['student/ask', vAskList], ['student/ask-new', vAskNew], ['student/ask-new/:lid', vAskNew], ['student/ask-new/:lid/:qid', vAskNew],
    ['student/thread/:id', vThreadStudent], ['student/subscription', vSubscription],
    ['teacher', vTeacherQueue], ['teacher/q/:id', vTeacherQuestion], ['teacher/inbox', vTeacherInbox], ['teacher/thread/:id', vTeacherThread], ['teacher/stats', function () { return '<h2>إحصائياتي</h2>' + statsCard(me()); }],
    ['admin', vAdminDash], ['admin/content', vAdminContent], ['admin/lesson/:id', vAdminLesson], ['admin/questions', vAdminQuestions], ['admin/question/new/:lid', vAdminQuestion], ['admin/question/:id', vAdminQuestion],
    ['admin/blueprints', vAdminBlueprints], ['admin/users', vAdminUsers], ['admin/student/:id', function (p) { return crumbs([['المستخدمون', '#/admin/users'], ['تقدّم الطالب']]) + vProgress(p, p.id); }],
    ['admin/audit', vAdminAudit], ['admin/export', vAdminExport]
  ];
  function match(hash) {
    var parts = hash.replace(/^#\/?/, '').split('/').filter(Boolean);
    for (var i = 0; i < ROUTES.length; i++) {
      var pat = ROUTES[i][0].split('/'), p = {}, ok = pat.length === parts.length;
      for (var j = 0; ok && j < pat.length; j++) { if (pat[j][0] === ':') p[pat[j].slice(1)] = decodeURIComponent(parts[j]); else if (pat[j] !== parts[j]) ok = false; }
      if (ok) return { view: ROUTES[i][1], params: p };
    }
    return null;
  }
  function render() {
    clearInterval(TIMER);
    var hash = location.hash || '';
    var r = hash.replace(/^#\/?/, '').split('/')[0];
    if (!ROLE[r]) { location.replace('#/' + role()); return; }
    if (r !== role()) { S.ui.role = r; save(); } // keeps browser Back working across role switches
    if (!/^#\/admin\/question\//.test(hash)) QE = null;
    var m = match(hash), body;
    if (role() === 'student' && studentGuard()) body = studentGuard();
    else body = m ? m.view(m.params) : notFound();
    $('app').innerHTML = header() + '<main>' + body + '</main>' + (role() === 'student' && !S.ui.avatar.open ? '<button class="fab" onclick="A.avatarOpen({})">المساعد</button>' : '') + avatarPanel();
    var av = $('avmsgs'); if (av) av.scrollTop = av.scrollHeight;
  }
  window.addEventListener('hashchange', function () { $('modal').innerHTML = ''; render(); window.scrollTo(0, 0); });
  window.addEventListener('DOMContentLoaded', function () { load(); if (!location.hash) location.replace('#/' + role()); render(); });
  // Exposed for console testing
  window.ElmanhgTest = { grade: grade, normAr: normAr, servable: servable, selectQuiz: selectQuiz, mergeBlueprints: mergeBlueprints, state: function () { return S; } };
})();
