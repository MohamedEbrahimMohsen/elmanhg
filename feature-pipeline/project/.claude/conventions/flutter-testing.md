# Testing convention — flutter

<!-- Framework, where tests live, naming pattern (file + method), what every test must assert, what a vacuous test looks like here. The planner writes the Test plan from this; the reviewer scores tests against it. -->

## Framework

| Level | Tool | Notes |
|---|---|---|
| Unit | `flutter_test` / `package:test`, `mocktail` | repositories, notifiers (`ProviderContainer.test()`), mappers, validators |
| Widget (the bulk) | `flutter_test` `testWidgets` + `mocktail` | every screen state |
| Golden | `alchemist` (CI goldens, Ahem font, platform-agnostic) | tag `golden` |
| A11y | `flutter_test` guidelines (`androidTapTargetGuideline`, `iOSTapTargetGuideline`, `labeledTapTargetGuideline`, `textContrastGuideline`) | in widget tests |
| Integration / native | `integration_test` (+ Patrol for native dialogs/permissions, if the repo uses it) | device/emulator |
| Bloc (only if repo uses Bloc) | `bloc_test` | `blocTest<C, S>(build:, act:, expect:)` |
| Coverage | `flutter test --coverage` + `lcov` | — |
## Location and naming

- `test/` mirrors `lib/`: `lib/features/invoices/data/invoices_repository.dart` → `test/features/invoices/data/invoices_repository_test.dart`.
- Files end in `_test.dart`. Goldens in `test/**/goldens/`. Shared helpers in `test/helpers/` (`pump_app.dart`, `fakes.dart`, `mocks.dart`).
- `group('<ClassName>', …)`; `test('<does observable thing> when <condition>')` / `testWidgets('shows retry button when load fails')`.
## Every test must

- Assert observable output: rendered text/semantics, provider state value, returned result — not private calls.
- Pump screens through `pumpApp` (wraps `ProviderScope(overrides:)`, `MaterialApp` with l10n delegates, theme with `AppTokens`).
- Find widgets in this priority: `find.bySemanticsLabel` → `find.text(l10n.x)` → `find.widgetWithText` → `find.byType` → `find.byKey` (last resort, with a comment why).
- Override providers at the repository boundary (`invoicesRepositoryProvider.overrideWithValue(mockRepo)`); register fallback values for `any()` of custom types (`registerFallbackValue`).
- Use `pump(duration)` when an infinite animation is on screen (skeleton shimmer); `pumpAndSettle()` only when none is.
- Dispose the semantics handle from `tester.ensureSemantics()`.
## Never

- Real network or real Dio in widget tests; mocking Dio inside a widget test.
- `Future.delayed`/`sleep` waits; depending on test order; real clock (use `package:clock` `withClock`).
- `skip: true`, `@Skip`, commenting out a test, or deleting one to pass a pipeline.
- Editing or deleting an existing test the plan's `### Tests` table does not list as `modify`/`delete`.
- `--update-goldens` in a feature commit (goldens update only in a dedicated commit, on the pinned CI image).
- Vacuous tests: pump with no `expect`, `expect(find.byType(Scaffold), findsOneWidget)` as the only assertion, asserting a stub's canned value.

## What to test at which level

| Code | Level | Must cover |
|---|---|---|
| Validator / mapper / use case | Unit | valid + each failure; DTO → domain mapping incl. unknown enum |
| Repository | Unit | success, each `ApiException` mapping (401, 422 fieldErrors, 404, network, 500) |
| Notifier / AsyncNotifier | Unit (`ProviderContainer.test`) | loading → data, error, invalidation after mutation |
| Screen | Widget | loading, data, empty, error + retry, form validation, `Locale('ar')` with no overflow (`tester.takeException()` is null) |
| Screen a11y | Widget | tap target + labeled tap target + text contrast guidelines pass; `TextScaler.linear(2.0)` no overflow |
| DS component / key screen | Golden (`@Tags(['golden'])`) | light/dark × en/ar × 390-wide phone |
| Router redirects | Unit | unauthenticated → `/login`, authenticated deep link kept |
| Native-only flow | `integration_test`/Patrol | only when the plan lists it |

## Skeletons

```dart
class MockInvoicesRepository extends Mock implements InvoicesRepository {}

void main() {
  late MockInvoicesRepository repo;
  setUp(() => repo = MockInvoicesRepository());

  test('invoicesProvider returns repository data', () async {
    when(() => repo.list(any())).thenAnswer((_) async => [fakeInvoice]);
    final c = ProviderContainer.test(overrides: [invoicesRepositoryProvider.overrideWithValue(repo)]);
    expect(await c.read(invoicesProvider(filter: InvoiceFilter.all).future), [fakeInvoice]);
  });

  testWidgets('shows retry when load fails', (tester) async {
    when(() => repo.list(any())).thenThrow(const ApiException.server());
    await tester.pumpApp(const InvoicesScreen(), overrides: [invoicesRepositoryProvider.overrideWithValue(repo)]);
    await tester.pump();
    expect(find.text(tester.l10n.retry), findsOneWidget);
  });

  testWidgets('meets a11y guidelines', (tester) async {
    final handle = tester.ensureSemantics();
    when(() => repo.list(any())).thenAnswer((_) async => [fakeInvoice]);
    await tester.pumpApp(const InvoicesScreen(), overrides: [invoicesRepositoryProvider.overrideWithValue(repo)]);
    await tester.pump();
    await expectLater(tester, meetsGuideline(androidTapTargetGuideline));
    await expectLater(tester, meetsGuideline(labeledTapTargetGuideline));
    await expectLater(tester, meetsGuideline(textContrastGuideline));
    handle.dispose();
  });

  testWidgets('renders RTL without overflow', (tester) async {
    when(() => repo.list(any())).thenAnswer((_) async => [fakeInvoice]);
    await tester.pumpApp(const InvoicesScreen(), locale: const Locale('ar'),
        overrides: [invoicesRepositoryProvider.overrideWithValue(repo)]);
    await tester.pump();
    expect(tester.takeException(), isNull);
  });
}
```
`pumpApp` and `tester.l10n` are team helpers in `test/helpers/`; create them once if missing, never per test.

## Coverage

```bash
flutter test --coverage --test-randomize-ordering-seed random
lcov --remove coverage/lcov.info '**/*.g.dart' '**/*.freezed.dart' 'lib/gen/*' 'packages/api_client/*' -o coverage/lcov.info
```
- Gate: 80 % lines on `lib/features/*/data`, `domain`, and notifiers.
- Goldens run separately: `flutter test --tags golden` on the pinned Linux runner.

## Flakiness

- Random order in CI (`--test-randomize-ordering-seed random`); fixed clock (`withClock`), fixed locale.
- No real timers; skeleton animations advanced with `pump(duration)`.
- A test that flakes twice is quarantined by a human with a linked ticket — never by the implementer.

## Test integrity

- Never edit, skip, or delete a test to make it pass. The orchestrator diffs test files after every implement/rework/fix; unlisted changes are a blocking "test integrity" finding.
- Test looks wrong → stop and write `BLOCKED: <test> — <why>` in the report.
