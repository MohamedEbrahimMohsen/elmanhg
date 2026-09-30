# Math final-answer check (CAS, #122)

## Purpose

A math-with-steps question (`QuestionType.MathSteps`, [question-schemas.md](question-schemas.md)) is graded on its **final answer** by a computer algebra system: SymPy, in the AI service. The student's steps are graded by the step grader (#123, [math-step-grading.md](math-step-grading.md)) when the question has a steps weight above 0; the final answer's verdict and the step credit are then combined. For a final-only question (steps weight 0) the score is the final answer's verdict: 1 when it is equivalent to an accepted answer in the required form, else 0 (PRD §6).

Contract: `POST /v1/math-checks` in the AI service ([ai-service.md](ai-service.md)). The .NET port is `IAiMathCheckClient` (`Fake` or `Http` by `AiService:Provider`).

## Notation

The parser accepts the LaTeX the web math input produces ([math-input.md](math-input.md)) and the Unicode glyphs of its keypad.

| Written | Read as |
|---|---|
| Digits, `٠`–`٩`, `۰`–`۹`, `٫` | numbers; Arabic-Indic digits become Latin, `٫` is the decimal point. A number is exact (`0.1` is 1/10); a trailing `.` is unreadable |
| `x`, `y`, `x_1`, `x_{ab}` | a symbol: one ASCII letter with an optional one-character or `{…}` (1 to 5 alphanumerics) subscript |
| `\alpha \beta \gamma \theta \lambda \mu \phi \omega` | Greek symbols |
| `e`, `\pi`, `π` | Euler's number and π |
| `+ - − – * / ^ = < > ( ) [ ] { }` | operators and brackets |
| `\times × \cdot · ⋅` / `\div ÷` | multiply / divide |
| `\le \leq ≤`, `\ge \geq ≥`, `\ne \neq ≠`, `\lt`, `\gt` | relations |
| `\frac{a}{b}` (`\dfrac`, `\tfrac`, `\frac12`) | a fraction; a group is `{…}` or one digit or name |
| `\sqrt{x}`, `\sqrt[3]{x}`, `√` | square and n-th roots |
| `\sin \cos \tan \cot \sec \csc`, `\sin^2 x` | functions; the argument is `(…)`, `{…}` or one primary, so `\sin 2x` is `sin(2)·x` |
| `\ln x`, `\log x`, `\log_2 8`, `\log_{b} x` | natural log, base-10 log, base-b log |
| `\pm ±` | plus-or-minus (one per element) |
| `\left \right \quad \qquad \displaystyle`, `\, \; \: \! \ `, `~`, spaces | ignored |
| `\text{…}` | removed (units such as `\text{ سم}`) |

- `x^23` is `x²·3`: TeX takes one token as the exponent, and the remaining digits multiply.
- Implicit multiplication happens before a name, a command such as `\frac` or `\pi`, or `(`, `[`, `{`, but never before a number (`x2` is unreadable).
- Anything else (another command, `_` outside a subscript, `;`, Arabic letters outside `\text{}`) makes the answer unreadable.

## Lists, `\pm` and sets

- Top-level commas (and the Arabic comma `،`) separate an **unordered** list of at most `ELMANHG_AI_CAS_MAX_ELEMENTS` elements; a comma inside brackets is unreadable.
- One outer `\{…\}` is removed, so `\{2, -3\}` is the list `2, -3`.
- An element with one `\pm` becomes two elements (`+` and `-`); more than one `\pm` is unreadable. `x = \pm 3` equals `x = 3, x = -3`.
- The answer matches when it has the same number of elements and each expected element (in order) takes the first unused answer element that matches it.

## Relations and assignments

- An **assignment** is `=` with a bare symbol on one side (the left wins): `x = 2` and `2 = x` both assign 2 to x.
- Expected **expression**: the answer may be an expression or an assignment; its value is compared. `2` matches `x = 2`.
- Expected **assignment** `s = v`: the answer is `v` alone or an assignment of the same `s`. `2x = 4` does **not** match `x = 2`: a final answer must isolate the unknown.
- Expected **other relation**: the answer must use the same operator after `>` → `<` and `≥` → `≤` by swapping sides, and `simplify((La − Ra) / (Le − Re))` must be a nonzero number (for `=` and `≠`) or a positive number (for `<` and `≤`). So `2x + 4 = 0` matches `x + 2 = 0`, and `x ≥ 3` does not match `x > 3`.

## Equivalence

For two values `a` (answer) and `b` (expected):

1. With a tolerance and no symbols on either side, the tolerance decides (below).
2. `a − b` is 0 after SymPy's automatic simplification → equivalent.
3. `simplify(a − b) = 0` → equivalent.
4. Otherwise the values are sampled at random points: a fixed seed (1729), signs alternating +, −, magnitudes `randint(50, 300)/100`, up to 24 points. Points where either side is not real and finite are skipped. Any real point with a relative gap above 10⁻¹² × max(1, |b|) means not equivalent; 5 agreeing points (1 when there are no symbols) mean equivalent. The fixed seed makes grading repeatable; alternating signs catch `√(x²)` ≠ `x`.

## Tolerance

Optional, per question, only with the `equivalent` form. It applies when both values are constants. `absolute` accepts `|a − b| ≤ tolerance`; `percent` accepts `|a − b| ≤ |b| × tolerance / 100`. Both bounds are inclusive. Two rationals compare exactly; otherwise both sides are evaluated to 30 digits.

## Forms

The form rule runs only on an answer that is already equivalent, and only on the answer.

| Form | Rule |
|---|---|
| `equivalent` | none |
| `exact` | the answer has no decimal literal (`0.5` fails, `\frac{1}{2}` passes) |
| `expanded` | `expand(value) = value` and the written answer has no product containing a sum with symbols (`2(x+1)` fails) |
| `factored` | every factor of the written top-level product is irreducible: `factor_list` gives one non-constant factor of multiplicity 1 and a content of ±1 (`x^2+2x+1` and `2x+2` fail; `(x+1)^2`, `(x+1)(x+1)` and `x(x+1)` pass) |
| `simplified` | no unreduced integer fraction (`\frac{6}{4}`, `\frac{5}{1}`), and the written answer has no more operations (`count_ops`) than the written accepted answer (`2+3` fails against `5`) |

Known limitation: `simplified` compares operation counts, so `x + x` passes against `2x`.

## Verdicts

| Verdict | Meaning | Score and feedback |
|---|---|---|
| `equivalent` | matches accepted answer `matchedIndex` in the required form | 1, «صُحّحت الإجابة النهائية فقط.» (final-only questions; step-graded questions combine it with the step credit) |
| `notEquivalent` | readable, matches no accepted answer | 0, the same line |
| `wrongForm` | equivalent to an accepted answer, but not in the required form | 0, «اكتب الإجابة النهائية بالصورة التي يطلبها السؤال.» |
| `unreadable` | the answer failed to parse or passed a size limit | 0, «تعذّرت قراءة الإجابة النهائية…» |
| `unchecked` | no accepted answer parses, the check timed out or a worker failed, or (on the .NET side) the AI service could not be reached | pending: the answer is deferred to a `MathStepGrade` and retried in the background; after the retries it goes to review (not an attempt). `grade-draft` still returns a provisional 0 with «تعذّر التحقق من الإجابة النهائية آليًا، وسيراجعها معلمك.» |

`invalidExpected` lists the indexes of accepted answers that do not parse; they are skipped, and the .NET client logs a warning. Accepted answers are not parsed when a question is saved, so authoring never depends on the AI service; the teacher validates them with the rules view.

A blank final answer is Unanswered (0) and the AI service is never called, even when steps were written.

## When the AI service is down

Only MathSteps answers call the AI service; every other type grades locally. An outage never loses an answer or blocks a submit:

- `HttpAiMathCheckClient` turns a transport failure, a timeout, a non-2xx reply or an unreadable reply into an `unchecked` verdict (and logs an Error).
- **Quiz and exam (#123):** an `unchecked` answer is not an attempt. It is saved on the item and requests a `MathStepGrade` with no verdict; the `math-step-grading` worker retries the check with backoff and, once checked, grades and applies it as a normal attempt with mastery. After `MathStepGrading:MaxAttempts` failed checks the grade is `InReview` with reason `FinalAnswerUnchecked` for the teacher (#128). The student sees «جارٍ تصحيح إجابتك…», then «قيد المراجعة» or the verdict ([math-step-grading.md](math-step-grading.md)). The quiz answer is saved and the exam is submitted as usual.
- **Legacy attempts:** `unchecked` attempts written before #123 keep their provisional score of 0 and their `mathUnchecked` feedback kind, stay **out of mastery** (`QuestionGrade.AwaitsReview`), and are **not** listed in the teacher review queue (#128): resolving an append-only attempt would need an override record honoured by every score reader, and these rows exist only in development databases (no live deploy). Only `MathStepGrade` rows in review are reviewed ([grade-review.md](grade-review.md)). Their attempt result carries `awaitsReview: true`, and the quiz feedback, the quiz result and the exam result still show them as «قيد المراجعة» / "Under review" with «الدرجة معلّقة حتى يراجعها معلمك.», hiding the correct answer and the explanation.
- **`grade-draft`** has no worker: an `unchecked` check still returns the provisional 0 with `mathUnchecked` feedback.
- **Rate limit (#123):** quiz MathSteps answers with a final answer are limited per student (`MathStepGrading:CheckPermitLimit` per `MathStepGrading:CheckWindowSeconds`, 10 per 60 s by default); a denied answer is `429 TOO_MANY_REQUESTS` and stores nothing. A replay never calls the CAS.
- The .NET fake refuses in Production with `503 MATH_CHECK_UNAVAILABLE` (a misconfiguration, not an outage), like the other fakes.

## Safety

Student and author input is untrusted:

- **No string evaluation.** Our own tokenizer (`cas/lexer.py`) and recursive-descent parser (`cas/parser.py`) build SymPy objects directly (`Integer`, `Rational`, `Symbol`, `Add`, `Mul`, `Pow`, `sin`, …) with `evaluate=False`. `sympify`, `parse_expr`, `parse_latex`, `S("…")`, `lambdify`, `eval` and `exec` are never used; `tests/unit/test_cas_sources_safe.py` fails if any appears in `cas/`.
- **Whitelist grammar.** Only the notation above is accepted; everything else is unreadable.
- **Limits** (settings, checked while parsing):

| Setting | Default | Limit |
|---|---|---|
| `ELMANHG_AI_CAS_MAX_ANSWER_CHARS` | 500 | answer length (400 `TOO_LONG`) |
| `ELMANHG_AI_CAS_MAX_EXPECTED` / `ELMANHG_AI_CAS_MAX_EXPECTED_CHARS` | 20 / 500 | accepted answers and their length (400) |
| `ELMANHG_AI_CAS_MAX_ELEMENTS` | 10 | list elements |
| `ELMANHG_AI_CAS_MAX_TOKENS` | 300 | tokens per text |
| `ELMANHG_AI_CAS_MAX_DEPTH` | 30 | bracket and command nesting |
| `ELMANHG_AI_CAS_MAX_NUMBER_DIGITS` | 30 | digits in one number |
| `ELMANHG_AI_CAS_MAX_EXPONENT` | 1000 | the absolute value of a numeric exponent, checked bottom-up, so `2^{2^{2^{10}}}` stops before it is built. A root `\sqrt[n]{x}` is the power `x^{1/n}`, so its exponent is `1/n`: an index of 0, or one smaller than 1/1000 (such as `\sqrt[0.0001]{x}`), is rejected as `unreadable` |
| `ELMANHG_AI_CAS_MAX_MAGNITUDE` | 10000 | an upper bound on the decimal digits of every power and every root, estimated from the unevaluated tree (a number counts its digits, a symbol 1, a sum or product adds its parts, a power or root multiplies its base by the exponent, at least 1). It stops compounded powers such as `((9^{999})^{999})^{999}` and nested roots such as `\sqrt[0.001]{\sqrt[0.001]{\sqrt[0.001]{9}}}`, whose exponents each pass the exponent limit, as `unreadable` before any evaluation. Exponentials (`e^{...}`) and powers of functions (`\sin^{n}`) take the same path; there is no factorial |
| `ELMANHG_AI_CAS_MAX_EXPANSION_TERMS` | 500 | (#123) an upper bound on the terms of the fully expanded answer, from the unevaluated tree: a symbol or number counts 1, a sum adds its parts, a product multiplies them, and a power with a numeric exponent n ≥ 2 over a base of t > 1 terms counts C(n + t − 1, t − 1). It is checked for every power and each side of an element, so `(x+y+z+w)^{999}` (about 1.7 × 10⁸ terms) or `(x+y+z)^{30}(x+y+z)^{30}` is `unreadable` before SymPy expands anything. The default is set from measurement so an answer that passes finishes well inside `ELMANHG_AI_CAS_TIMEOUT_SECONDS`: at about 500 terms (`x=(x+y+z)^{30}`, `(a+b+c+d+e)^{8}`) a check took under 1 s on a laptop, while `x=(x+y+z+w)^{20}` (1771 terms) took about 4 s and `x=(x+y+z)^{90}` (4186 terms) 13 s, past the 5 s timeout. School answers stay far below the bound (`(a+b)^{100}` is 101 terms) |

- **Worker slots.** Evaluation runs in `ELMANHG_AI_CAS_WORKERS` worker slots, each its own single-process `multiprocessing` pool (`forkserver` where available, else `spawn`), recycled after `ELMANHG_AI_CAS_MAX_TASKS_PER_WORKER` checks and capped at `ELMANHG_AI_CAS_WORKER_MEMORY_MB` of address space (`RLIMIT_AS`, POSIX only). A check first waits for a free slot, and only then does its `ELMANHG_AI_CAS_TIMEOUT_SECONDS` clock start, so queue time is never charged to it. A check that runs past the timeout returns `unchecked` and only its own worker process is killed; the slot is restarted and warmed in the background before it takes the next check, and checks in the other slots are untouched. Threads cannot interrupt CPU-bound SymPy, so killing the process is the only hard stop. Any other worker exception (#123) also returns `unchecked` and recycles the slot, because the process may be unhealthy. The lifespan warms every slot at startup (`ELMANHG_AI_CAS_WARM_ON_START`, default on), so the first check does not pay the SymPy import; a failed warm-up stops the service from starting.
- The endpoint never returns 5xx for CAS work. The answer and the accepted answers are never logged (`math_check.completed` logs the verdict, form, counts, answer length and latency).

## The .NET fake

`FakeAiMathCheckClient` (the default provider, used by tests and CI) maps Arabic-Indic digits to Latin, removes `\left`, `\right` and all whitespace, and compares the strings exactly with each accepted answer: `equivalent` with the matched index, else `notEquivalent`. It never calls the AI service.
