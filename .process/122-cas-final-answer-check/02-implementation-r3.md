# Implementation r3: CAS final answer check (rework of 03-review-r2.md)

## Findings addressed
| # | What I changed | file:line |
|---|---|---|
| 4 | `root` now takes `limits` and builds its value through `power(radicand, 1/index, limits)`. This means `check_exponent` and the `magnitude` bound both apply to roots. The rule: `\sqrt[n]{x}` is `x^(1/n)`. An index of 0 has a non-finite exponent, and an index with \|n\| < 1/max_exponent (1/1000 by default) is over the exponent limit. Both are rejected as "exponent too large", which gives `unreadable`. Nested roots and roots of large powers are rejected as "power too large". The parser passes `self.limits`. | `ai/src/elmanhg_ai/cas/nodes.py:58-61`, `ai/src/elmanhg_ai/cas/parser.py:195` |
| 4 | Tests: I added three cases to `test_parse_compounded_power_raises`: `nested-root` (the reproducer), `fractional-index-root` (the `\frac{1}{999}` index tower) and `root-of-power` (`\sqrt[0.001]{9^{999}}`). I also added the new `test_parse_root_with_zero_or_tiny_index_raises` (cases `\sqrt[0]{9}` and `\sqrt[0.0001]{9}`). | `ai/tests/unit/test_cas_parser.py:104-133` |
| 4 | Audit of the other node constructors: `Pow` is built only in `power`, `root` and `reciprocal`. `reciprocal` has exponent -1, and `magnitude` already counts the digits of both the numerator and the denominator of a rational, so dividing by tiny numbers adds nothing that is not already bounded. That size is multiplied again only when the result is raised through `power`. `e^{...}`, `\sin^{n}` and `\log` powers all go through `power`. `FUNCTIONS` has no `exp`, and the lexer has no factorial token. No other gap was found. | none |
| 5 | Both settings rows now say that on a timeout only the offending worker slot is killed and restarted in the background, and that the other slots are untouched. | `docs/ai-service.md:291`, `docs/deployment.md:271` |
| 5 | `math-cas.md`: the exponent row now states the root-index rule. The magnitude row now covers every power and every root, and names the nested-root example. It also notes that exponentials and function powers take the same path and that there is no factorial. | `docs/math-cas.md:114-115` |

## Files created
None.

## Files modified
| Path | Change |
|---|---|
| `ai/src/elmanhg_ai/cas/nodes.py` | `root(radicand, index, limits)` delegates to `power`. |
| `ai/src/elmanhg_ai/cas/parser.py` | Passes `self.limits` to `nodes.root`. |
| `ai/tests/unit/test_cas_parser.py` | Adds the root cases and the new zero/tiny-index test. |
| `docs/ai-service.md`, `docs/deployment.md`, `docs/math-cas.md` | Doc sync, as described above. |

## Deviations
None.

## Build & test
Run from `ai/` on Windows:
- `python -m uv run ruff format --check .`: 121 files already formatted
- `python -m uv run ruff check .`: All checks passed!
- `python -m uv run mypy src`: Success: no issues found in 68 source files
- `python -m uv run pytest -q`: 389 passed, 3 skipped in 13.04s

## Notes for review
- There is a behaviour change: `\sqrt[0.001]{9^{999}}`, which r2 accepted, is now rejected. It equals 9^999000, so rejecting it is correct.
- A single `\sqrt[0.001]{9}` (9^1000) is still accepted, the same as `9^{1000}`.
- A symbolic index such as `\sqrt[x]{9}` gets no numeric bound, the same as a symbolic exponent. This is the backstop case the r2 review already noted as non-blocking.
- I did not re-run the Docker/HTTP reproduction. The parse-level tests cover the reproducer.
