---
name: flutter-feature
description: Style guide for every change in this repo's Flutter app. The planner, style-checker, reviewer and OpenCode judge mobile/ code against this file and nothing else. Team default; tune the sections, keep the numbering.
---

# Flutter feature handbook

Stack: **Flutter 3.x · Dart 3 · Riverpod · go_router · Dio + Retrofit · freezed + json_serializable · flutter_localizations (AR/EN, RTL)**
  → 2026-09 update: Stack is **Flutter 3.47 stable · Dart 3.12+ · Riverpod 3 + riverpod_generator · go_router 18 + go_router_builder · Dio (one client) + generated OpenAPI client (§11) · freezed 4 + json_serializable · flutter_localizations + gen-l10n (AR/EN, RTL) · flutter_secure_storage 11 · very_good_analysis 11**. Retrofit stays only where the repo already uses it.
Design: every visual value comes from `.claude/design-system.md` through `AppTheme` / `AppTokens`. Every screen comes from a Figma frame.

## 1. Layout

- `lib/features/<feature>/` with `data/` (dto, api, repository), `domain/` (entity, use case), `presentation/` (screens, widgets, providers).
- Shared widgets only in `lib/core/widgets/`. Theme and tokens only in `lib/core/theme/`.
- One widget class per file. No file over 250 lines. A `build` method over 60 lines gets split into private widgets, not private methods.
- Generated code: `*.g.dart`, `*.freezed.dart` committed; generated OpenAPI client in a local package (`packages/api_client`) or `lib/gen/`. Never hand-edit; CI runs `dart run build_runner build -d` + `git diff --exit-code`.
- Design tokens: one generated file (e.g. `lib/core/theme/app_tokens.g.dart`) exposed as `ThemeExtension<AppTokens>`. Never hand-edit.
- `test/` mirrors `lib/`; `integration_test/` for device flows; goldens under `test/goldens/` (or `test/**/goldens/`).
- Flavors: `lib/main_dev.dart`, `lib/main_prod.dart` (+ `main_e2e.dart` for web E2E if the repo uses one).

## 2. Naming

- Files `snake_case.dart`. Classes `PascalCase`. Screens end in `Screen`, widgets in `Widget` only when ambiguous, providers in `Provider`.
- DTOs end in `Dto`, entities have no suffix, use cases are verbs (`GetOrders`, `SubmitRefund`).
- Localisation keys in `lib/l10n/app_ar.arb` and `app_en.arb`, both always; access via `context.l10n.key`.

## 3. Forbidden

- `setState` for anything beyond local widget UI state. State lives in Riverpod providers.
- `Navigator.push` directly. Routing goes through go_router routes.
- Raw `Color(0xFF…)`, raw `TextStyle(fontSize: …)`, raw `EdgeInsets.all(13)`. Tokens from `AppTokens` only.
- `print`/`debugPrint` in committed code. `// TODO`. Commented-out code.
- `dynamic`. Nullable without a reason. `!` on values that can be null.
- Hard-coded user-visible strings. `EdgeInsets.only(left:, right:)` (breaks RTL); use `EdgeInsetsDirectional`.
- Business logic in a widget.
- `StateProvider`, `StateNotifierProvider`, `ChangeNotifierProvider`, `package:riverpod/legacy.dart` imports (Riverpod 3 legacy).
- freezed `.when()`/`.map()`/`.maybeWhen()` (removed in freezed 3+). freezed classes without `sealed`/`abstract`.
- `WillPopScope`, `MaterialStateProperty`, `textScaleFactor`, `Color.withOpacity`, `ButtonBar`, `useMaterial3: false`.
- `Alignment.centerLeft`, `TextAlign.left/right`, `Positioned(left:)` in features (use directional versions).
- Tokens in `SharedPreferences`/Hive/sqflite. `encryptedSharedPreferences` option (removed in flutter_secure_storage 11).
- `BuildContext` used after `await` without `if (!context.mounted) return;`; notifier state set after `await` without `if (!ref.mounted) return;`.
- `GestureDetector` as a button. Helper methods returning widgets (`Widget _buildX()`).
- `badCertificateCallback` returning `true`; `LogInterceptor` in release builds.

## 4. Required

- Every screen handles loading, empty, error, and data states with `AsyncValue.when`, matching the Figma frame for each.
  → 2026-09 update: new code uses an exhaustive Dart 3 `switch` on `AsyncValue` (`AsyncData(:final value)`, `AsyncError(:final error)`, `_` loading). `AsyncValue.when` still compiles and is not a finding in existing code; freezed unions have no `when`/`map`.
- Every network call goes through a repository returning a `Result`/`Either`, never a raw `Response` into presentation.
- freezed for every model; `copyWith`, equality, and JSON come from generation, never by hand.
- `const` constructors wherever possible. Keys on list items.
- Accessible: `Semantics` labels on icon-only buttons, tap targets ≥ 48dp.
- Every `AsyncValue` handled in an exhaustive `switch` (`AsyncData`, `AsyncError`, loading default) — loading, error, empty, data all rendered.
- `ref.watch` in `build`, `ref.read` in callbacks, `ref.listen` for side effects (snackbar, navigation).
- Directional APIs only: `EdgeInsetsDirectional`, `AlignmentDirectional`, `PositionedDirectional`, `BorderRadiusDirectional`, `TextAlign.start/end`.
- Text containers never have a fixed height (text scaling to 200 % must not overflow).
- Web build used for E2E calls `SemanticsBinding.instance.ensureSemantics()` when `kIsWeb` (§22).

## 5. Tests

- Unit tests for use cases and providers (`test/features/<feature>/`), widget tests for every screen state, golden tests only when the design-system team asks.
- Mock at the repository boundary with mocktail. Never mock Dio inside a widget test.
- Every use case error path has a test.
- Full rules: `.claude/conventions/flutter-testing.md`. It wins on test detail.
- Every screen: widget tests for loading, data, empty, error, plus one `Locale('ar')` pump with no overflow exception.
- Never edit, skip (`skip: true`, `@Skip`), or delete an existing test to make it pass. A wrong test → `BLOCKED: <test> — <why>` in the report.

## 6. DO / DON'T catalog (every DON'T is a blocking review finding)

| # | DON'T | DO |
|---|-------|----|
| 6.1 | `Color(0xFF4B2A8A)` in a widget | `AppTokens.color.primary` |
| 6.2 | `EdgeInsets.only(left: 16)` | `EdgeInsetsDirectional.only(start: 16)` via `AppTokens.space` |
| 6.3 | `setState` holding server data | `AsyncNotifier` provider |
| 6.4 | `Navigator.push(MaterialPageRoute…)` | `context.go('/route')` |
| 6.4 → 2026-09 update | `context.go('/route')` string path in new code | Typed route from go_router_builder: `const InvoiceRoute(id: '42').go(context)` |
| 6.5 | Skip the empty/error state | Implement every state in the Figma frame |
| 6.6 | `Text('Submit')` | `Text(context.l10n.submit)` with both `.arb` files updated |
| 6.7 | One 300-line screen file | Screen + private widgets in separate files |
| 6.8 | Hand-written `fromJson` | freezed + json_serializable |
| 6.9 | `StateNotifierProvider` / `StateProvider` | `@riverpod class X extends _$X` (Notifier / AsyncNotifier) |
| 6.10 | `invoice.when(paid: …, draft: …)` (freezed) | `switch (invoice) { Paid() => …, Draft() => … }` |
| 6.11 | `Color.withOpacity(0.5)` | `color.withValues(alpha: 0.5)` |
| 6.12 | `WillPopScope` | `PopScope` + `onPopInvokedWithResult` |
| 6.13 | `MaterialStateProperty` | `WidgetStateProperty` |
| 6.14 | `Alignment.centerLeft` | `AlignmentDirectional.centerStart` |
| 6.15 | `SharedPreferences` for tokens | `flutter_secure_storage` 11 |
| 6.16 | `GestureDetector(onTap:)` as button | `InkWell`/Material button + `Semantics` |
| 6.17 | `Widget _buildHeader()` | `class _Header extends StatelessWidget` with `const` |

## 7. New project from scratch

Use only when the plan says "new app" (see `momenta-greenfield-bootstrap`).

```bash
flutter --version                       # must print 3.47.x stable, Dart 3.12+
flutter create --org com.acme --platforms=android,ios,web --empty mobile
cd mobile
flutter pub add flutter_riverpod riverpod_annotation go_router dio freezed_annotation json_annotation \
  flutter_secure_storage intl
flutter pub add flutter_localizations --sdk=flutter
flutter pub add dev:build_runner dev:riverpod_generator dev:freezed dev:json_serializable \
  dev:go_router_builder dev:very_good_analysis dev:mocktail dev:alchemist dev:riverpod_lint
dart run build_runner build -d
```
- Web platform is included so the Agy E2E step can run against `flutter build web`.
- Material: opt into the standalone `material_ui` package only if the plan says so (1.0 in Flutter 3.47; go_router 18 already depends on it). Verify the package on pub.dev (§21) and flags with `flutter pub add --help` first.
- State management: Riverpod 3 with codegen. Bloc (`flutter_bloc` 9 + `bloc_test`) only if the repo already uses it. Never both in one app.
- iOS minimum 15; SwiftPM is the default plugin integration (no new CocoaPods-only plugins).

Layout for a new app:

```
lib/
  main_dev.dart  main_prod.dart  main_e2e.dart
  app/        app.dart (MaterialApp.router), router.dart, bootstrap.dart
  core/       theme/ (app_tokens.g.dart, app_theme.dart), widgets/, network/ (dio_client.dart, auth_interceptor.dart, api_exception.dart), storage/, env.dart
  l10n/       app_en.arb, app_ar.arb
  features/<feature>/ data/ domain/ presentation/ (application/ for notifiers if split)
packages/api_client/   generated OpenAPI client
```

## 8. State & data fetching (Riverpod 3)

```dart
@riverpod
InvoicesRepository invoicesRepository(Ref ref) => InvoicesRepository(ref.watch(dioProvider));

@riverpod
Future<List<Invoice>> invoices(Ref ref, {required InvoiceFilter filter}) =>
    ref.watch(invoicesRepositoryProvider).list(filter);

@riverpod
class InvoiceEditor extends _$InvoiceEditor {
  @override
  FutureOr<Invoice?> build(String id) => ref.watch(invoicesRepositoryProvider).get(id);

  Future<void> save(Invoice draft) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() => ref.read(invoicesRepositoryProvider).update(draft));
    if (!ref.mounted) return;
    ref.invalidate(invoicesProvider);
  }
}
```
- One `Ref` type (Riverpod 3). No `XxxRef`, `AutoDisposeNotifier`, `FamilyNotifier`.
- Riverpod 3 auto-retries failed providers. Non-idempotent or 4xx-driven providers disable it: top-level `Duration? noRetry(int count, Object error) => null;` + `@Riverpod(retry: noRetry)` (verify signature against the installed riverpod version).
- Narrow rebuilds with `ref.watch(p.select((s) => s.field))`.
- Providers are top-level (generated). Never create a provider inside `build`.

## 9. Forms & validation

- `Form` + `TextFormField` with `validator` functions from `domain/` (pure, unit-tested) or a form-state notifier; one approach per feature.
- Every field: `InputDecoration(labelText: context.l10n.x, errorText: …)`; label always visible (no placeholder-only fields).
- Server `ValidationException.fieldErrors` mapped to per-field `errorText`; focus moves to the first invalid field (`FocusNode.requestFocus`).
- Submit button disabled only while submitting; shows loading state from the notifier.
- Keyboard: correct `keyboardType`, `textInputAction`, `autofillHints` (`AutofillHints.email`, `.password`, `.oneTimeCode`); paste allowed on auth fields.

## 10. Routing (go_router 18 + go_router_builder)

```dart
@TypedGoRoute<InvoiceRoute>(path: '/invoices/:id')
class InvoiceRoute extends GoRouteData with $InvoiceRoute {
  const InvoiceRoute({required this.id});
  final String id;
  @override
  Widget build(BuildContext context, GoRouterState state) => InvoiceScreen(id: id);
}

final routerProvider = Provider((ref) => GoRouter(
  refreshListenable: ref.watch(authListenableProvider),
  redirect: (context, state) => authRedirect(ref, state),   // the only auth guard
  routes: $appRoutes,
));
```
- IDs in path params, filters in query params, `extra` only for transient non-deep-linkable data.
- Bottom tabs: `StatefulShellRoute.indexedStack`.
- URLs are case-sensitive (go_router ≥ 15). Redirect logic has unit tests.
- Deep links validate params and never auto-perform an action (open the screen; the user confirms).

## 11. API client generation

- One generator per repo, CI-regenerated and diffed. Default:
```bash
npx @openapitools/openapi-generator-cli generate -i ../contracts/openapi.yaml -g dart-dio \
  -o packages/api_client --additional-properties=serializationLibrary=json_serializable,pubName=api_client
```
- Repo already on Retrofit (`retrofit` + `retrofit_generator`) or `swagger_parser`: keep it.
- Repositories wrap the client, map DTO → domain, and map `DioException` → sealed `ApiException` (`Unauthorized`, `Validation(fieldErrors)`, `NotFound`, `Network`, `Server`).
- Endpoint missing from the spec → backend adds it to OpenAPI first.

## 12. Loading / error / empty states

```dart
return switch (ref.watch(invoicesProvider(filter: f))) {
  AsyncData(:final value) when value.isEmpty => const InvoicesEmpty(),
  AsyncData(:final value) => InvoicesList(items: value),
  AsyncError(:final error) => ErrorView(error: error, onRetry: () => ref.invalidate(invoicesProvider)),
  _ => const InvoicesSkeleton(),
};
```
- Loading = skeleton matching the final layout. Error = localized message by `ApiException` type + Retry. Empty = Figma empty state with CTA; "no results for filter" offers "Clear filters".
- Mutation outcome: `SnackBar` via `ref.listen`; blocking form errors inline.

## 13. Auth token handling

- Tokens only in `flutter_secure_storage` 11 (Keychain / Keystore). iOS accessibility `KeychainAccessibility.first_unlock_this_device` (no iCloud sync). Access token cached in memory.
- Dio `QueuedInterceptorsWrapper` → single-flight refresh on 401 → retry once → on failure clear storage, invalidate auth provider, router redirects to `/login`.
- Release network rules: Android `network_security_config.xml` `cleartextTrafficPermitted="false"`; iOS ATS on, no `NSAllowsArbitraryLoads`.
- Certificate pinning only when the plan requires it: SPKI primary + backup pin, remotely rotatable.

## 14. i18n + RTL (Arabic)

```yaml
# l10n.yaml
arb-dir: lib/l10n
template-arb-file: app_en.arb
output-localization-file: app_localizations.dart
nullable-getter: false
```
- `pubspec.yaml`: `flutter: generate: true`. `MaterialApp.router(localizationsDelegates: AppLocalizations.localizationsDelegates, supportedLocales: AppLocalizations.supportedLocales)`.
- Arabic plurals in ARB with `zero/one/two/few/many/other`. Placeholders, never concatenation.
- RTL comes from locale. Directional widgets only (§4). Directional icons: `Icons.adaptive.arrow_back` or `Transform.flip(flipX: Directionality.of(context) == TextDirection.rtl)`. Never mirror logos, media controls, checkmarks.
- Dates/numbers via `intl` (`DateFormat.yMMMd(locale)`, `NumberFormat.currency(locale:)`).

## 15. Accessibility (WCAG 2.2 AA)

- Tap targets ≥ 48×48 dp (Android) / 44×44 pt (iOS). Custom tappables use buttons/`InkWell`, or `ConstrainedBox(minWidth: 48, minHeight: 48)`.
- Custom widgets: `Semantics(label:, button: true)` / `header: true`; `MergeSemantics` for label+value rows; `ExcludeSemantics` for decorative; images `semanticLabel` or `excludeFromSemantics: true`.
- Text scaling to 2.0 without overflow (tested, see testing convention).
- Keyboard (web/desktop): `FocusTraversalGroup`, `Shortcuts`/`Actions`, visible focus from theme `focusColor` token.
- Async results announced: `Semantics(liveRegion: true)` or `SemanticsService.announce`.
- Contrast comes from tokens (verified once in design-system.md).

## 16. Theming from design tokens

```dart
extension TokensX on BuildContext {
  AppTokens get tokens => Theme.of(this).extension<AppTokens>()!;
}
Padding(padding: EdgeInsetsDirectional.all(context.tokens.space.md), child: …);
```
- `ThemeData(colorScheme: …, textTheme: …, extensions: [AppTokens.light])` built from the generated tokens; dark theme = second token set; `ThemeMode.system`.
- CI grep fails on `Color\(0x`, `Colors\.[a-z]`, `EdgeInsets(Directional)?\.(all|only|symmetric)\([0-9]`, `fontSize: [0-9]` under `lib/features/`.

## 17. Responsive

- Phone first at 390 logical px wide; tablets/web at ≥ 840 use `LayoutBuilder`/`MediaQuery.sizeOf` breakpoints from tokens.
- No fixed widths for text containers; `Flexible`/`Expanded` over hard sizes.
- Keyboard insets: scrollable forms (`SingleChildScrollView` + `MediaQuery.viewInsetsOf`), submit reachable with keyboard open.

## 18. Performance

- `const` constructors; split widgets instead of helper methods.
- Lists: `ListView.builder`/`SliverList.builder`; `itemExtent`/`prototypeItem` when fixed height. No `shrinkWrap: true` list inside a scroll view for long lists.
- Images: `cached_network_image` with `memCacheWidth` sized to display (only if already a dependency or named in the plan).
- JSON parsing of large payloads in `Isolate.run`.
- Profile with `flutter run --profile` + DevTools before any manual optimization.

## 19. Security

- Anything in the binary (`--dart-define`, assets, `.env`) is public. No private API keys.
- Release: `flutter build appbundle --obfuscate --split-debug-info=build/symbols`; Android `android:allowBackup="false"` or backup rules; `FLAG_SECURE` for sensitive screens.
- WebView: no `JavaScriptMode.unrestricted` + `JavaScriptChannel` on untrusted pages; no `loadHtmlString` with user HTML.
- No logging of tokens/PII; `LogInterceptor` only in dev flavor with `requestHeader: false`.
- `pubspec.lock` committed; package publisher verified on pub.dev.

## 20. Tooling & CI commands

```bash
flutter pub get
dart run build_runner build -d && git diff --exit-code
dart format --set-exit-if-changed .
dart analyze --fatal-infos
flutter test --coverage --test-randomize-ordering-seed random
flutter test --tags golden                         # pinned Linux runner only
flutter build web --release --dart-define-from-file=env/e2e.json   # Agy E2E target
flutter build appbundle --flavor prod --obfuscate --split-debug-info=build/symbols
```
`analysis_options.yaml`: `include: package:very_good_analysis/analysis_options.yaml`; exclude `**/*.g.dart`, `**/*.freezed.dart`, `packages/api_client/**`; `strict-casts`, `strict-inference`, `strict-raw-types` true; `riverpod_lint` plugin.
Env via `--dart-define-from-file=env/<flavor>.json`, read in one `Env` class with `String.fromEnvironment`.

## 21. Dependencies (verify before adding)

- Add a package only if the plan names it with a version.
- Before `flutter pub add`: `curl -s https://pub.dev/api/packages/<pkg> | jq -r .latest.version` returns a version; publisher is verified; `pubspec.lock` updated in the same commit.
- Never guess a package name. Not found → `BLOCKED: dependency <pkg> not found`.

## 22. Web build for E2E (Agy)

```dart
void main() {
  if (kIsWeb) SemanticsBinding.instance.ensureSemantics();   // dev + e2e flavors at minimum
  runApp(const ProviderScope(child: App()));
}
```
- Without it the browser agent sees a canvas → every scenario is BLOCKED/INCONCLUSIVE.
- Key controls get `Semantics(identifier: 'invoice-save')` as a secondary automation hook; labels stay the primary locator.
- Native-only behavior (permissions, biometrics, deep links) is covered by `integration_test`/Patrol, and marked `web-verifiable: no` in the plan.

## 23. LLM mistakes to avoid

| Mistake | Correct |
|---|---|
| freezed `when`/`map`/`maybeMap`; non-`sealed` freezed class | Dart 3 `switch` patterns; `sealed class X with _$X` |
| Riverpod 2: `StateNotifierProvider`, `StateProvider`, `AutoDisposeFutureProviderRef` | `@riverpod` Notifier/AsyncNotifier, `Ref` |
| `ref.read` in `build` / `ref.watch` in callbacks | `ref.watch` in build, `ref.read` in callbacks |
| `WillPopScope`, `MaterialStateProperty`, `textScaleFactor`, `withOpacity` | `PopScope`, `WidgetStateProperty`, `textScaler`, `withValues(alpha:)` |
| `Navigator.push(MaterialPageRoute(...))` for app routes | Typed go_router route |
| `EdgeInsets.only(left:)`, `TextAlign.left` | `EdgeInsetsDirectional.only(start:)`, `TextAlign.start` |
| Tokens in `SharedPreferences`; `encryptedSharedPreferences: true` | `flutter_secure_storage` 11 defaults |
| Only `AsyncData` rendered | Loading/empty/error per §12 |
| `print` | `dart:developer` `log` behind flavor flag |
| Ad-hoc `http` package per feature | The one Dio client |
| `dynamic` JSON maps in UI | Typed models |
| Web E2E build without `ensureSemantics()` | §22 |

## 24. Definition of done

- [ ] `dart format --set-exit-if-changed .` and `dart analyze --fatal-infos` exit 0.
- [ ] `dart run build_runner build -d` produces no diff.
- [ ] `flutter test --coverage` exits 0; thresholds from `.claude/conventions/flutter-testing.md` met.
- [ ] Every screen renders loading, empty, error, data matching Figma frames.
- [ ] No literal colour/spacing/font size in `lib/features/` (grep from §16 clean).
- [ ] No non-directional insets/alignment; `ar` pump has no overflow.
- [ ] Every new string in both `app_en.arb` and `app_ar.arb`.
- [ ] Tap targets ≥ 48 dp; icon-only buttons have semantics labels.
- [ ] No new dependency outside the plan; `pubspec.lock` updated if one was added.
- [ ] No existing test edited/skipped/deleted unless the plan's `### Tests` table lists it.
- [ ] Report contains commands, output tails, exit codes; stuck → `BLOCKED: <reason>`.
