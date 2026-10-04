# Plan — Full-page AI assistant chat (#288, E20.S1)

## Goal
A student can open «المساعد» from the student nav (desktop top bar and the mobile «المزيد» page), or from a new «فتح في صفحة كاملة» icon link in the floating panel header, and chat with the AI assistant on a full page at `/student/assistant`. Past chats are listed beside the conversation. The student can start a new chat (optionally picking a subject and lesson), reopen a chat, or delete one, and can deep-link to a chat at `/student/assistant/$conversationId`. Endpoints, limits, citations, the exam refusal and the deletion flag are the same as in the panel. The floating `AvatarDock`/`AvatarPanel` keep their behaviour and look; the only change to them is the one header link.

## Scope
**In:** web only. Two routes, page, conversation list, chat column, context picker, page composer, nav entries, panel header link, hiding the floating dock on the assistant page, the on-demand `assistant` i18n namespace, the budget entry, tests and docs.
**Out:** any `api/` or `ai/` change; streaming (none today, see `docs/avatar.md` Streaming); a subject-only API context; the prototype (`prototype/app.js` has no full-page assistant, and we do not add one).
**Deferred:** nothing.

**Backend:** no change is needed. `GET /api/avatar/status`, `POST /api/avatar/messages`, `GET /api/avatar/my-conversations`, `GET /api/avatar/my-conversations/{id}` and `DELETE /api/avatar/my-conversations/{id}` cover the page. The picker reads `GET /api/mastery/overview` for subjects, `GET /api/browse/subjects/{id}` for units, and `GET /api/browse/units/{id}` for lessons with `isLocked`. All are existing generated hooks.
**Morabh:** not applicable. There are no `api/` changes, and every web file below is new, with no Morabh equivalent (Morabh is the .NET reference).

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | Routes | A layout route `src/routes/student/assistant.tsx` (`/student/assistant`) whose component renders `AssistantPage` with **no `<Outlet/>`**, plus a component-less child `src/routes/student/assistant.$conversationId.tsx` (`/student/assistant/$conversationId`). The parent reads the id with `useParams({ strict: false })`. | The parent match id stays the same when the URL changes between `/student/assistant` and `/student/assistant/X`, so the page never remounts. It keeps its reducer state when a new chat gets its id, and the URL updates with `replace`. Both `to` strings stay typed for `NavItem.to` and `Link`. Optional path params (`{-$id}`) would make `'/student/assistant'` an untyped `to`. |
| D2 | Page state | The page owns a **page-local controller**: `useReducer(assistantReducer)` rendered as `<AvatarControllerContext value={…}>` around the page. `assistantReducer` wraps the unchanged `avatarReducer` and adds `newChat` and `opening`, plus an `openingId` field. | Every shared hook and component (`useAvatar`, `useAvatarChat`, `useAvatarHistory`, `useDeleteAvatarConversation`, `AvatarConversation`, `AvatarMessageBubble`, `AvatarCitations`, `AvatarNotice`, `DeleteAvatarConversationDialog`) then works as it is, reading the nearest controller. The global `AvatarProvider` (the panel's state) is shadowed only inside the page. No shared file changes. |
| D3 | What is shared vs new | **Shared unchanged:** `AvatarConversation` (greeting, bubbles, citations, notices, «المساعد يكتب…», the `role="log"` live region), `DeleteAvatarConversationDialog`, `useAvatarChat`, `useAvatarHistory` (its `list`), `useDeleteAvatarConversation`, `avatarMessageSchema`, `toResumed`, `conversationTitle`, `avatarNoticeOf` (through `useAvatarChat`). **New:** `AssistantComposer`, which needs focus return after a reply and must invalidate the list after a send (the panel's composer does neither, and changing it would change the panel); `AssistantConversationItem` (a `Link` with an active state; `AvatarHistoryItem` is a button with no current state). | Satisfies "share where it doesn't change the panel's look". |
| D4 | Layout | `lg` and up: a grid of 3 columns, with the list in `lg:col-span-1` (start side) and the chat in `lg:col-span-2`. The page section fills the viewport height through two new `@utility` classes in `app.css` (`h-assistant`, `lg:h-assistant-desktop`), so the message log scrolls inside the card and the composer stays at its bottom. | A chat-app feel. Arbitrary values are forbidden (skill §3), and `@utility` is the repo's precedent (`bottom-above-tab-bar`). |
| D5 | Mobile | **Back navigation, not a drawer.** Below `lg` only one column shows: the chat by default. A «محادثاتي السابقة» toggle button (`aria-expanded`, `aria-controls`) in the page toolbar switches to the list. The list has «العودة إلى المحادثة» (`lg:hidden`), and choosing a chat switches back. Focus moves to the shown column's heading through `flushSync` and then `ref.focus()`. | This mirrors the panel's history/back pattern and strings. It needs no radix dialog in the chunk and no focus trap to get right. |
| D6 | Floating dock on the page | Hidden on `/student/assistant*` by a new `StudentAvatarDock` wrapper (`useMatch({ from: '/student/assistant', shouldThrow: false })`). The student route renders it instead of `<AvatarDock/>`. `AvatarDock` itself is untouched. | The pill (fixed bottom-start) would cover the list's bottom and the mobile composer, and a second chat surface on the same screen would compete with the page. The `AvatarDock` code and tests stay unchanged. |
| D7 | Context picker | Two labelled native `Select`s: «المادة» (subjects from mastery overview; first option «عام») and «الدرس» (disabled until a subject is chosen; `<optgroup>` per unit; locked lessons are `disabled`, labelled «{name} (للمشتركين)»). No lesson means `{ entryPoint: 'Global' }`; a lesson means `{ entryPoint: 'Lesson', lessonId, title: lessonName }`. A subject alone is a **filter only**, and the context stays Global. | The API has no subject entry point, and the Global bundle already holds every subject name. Using browse units for `isLocked` avoids a `403 LESSON_LOCKED` that would surface as the generic notice. No fieldset is used (this avoids #286's shared fieldset work). |
| D8 | When the picker shows | Only for a fresh chat: `conversationId === null && openingId === null && messages.length === 0`. Otherwise the chat shows the read-only line `avatar:panel.context` («السياق: {title}» / «عام»). Reopened chats keep their stored context through `toResumed`. | The context cannot change mid-conversation (the server returns `AVATAR_CONVERSATION_CONTEXT_MISMATCH`). |
| D9 | URL ↔ state | Algorithm in `useAssistantConversation` (see Domain behaviour). A new chat's first reply replaces the URL with `/student/assistant/{id}`. Opening a list item pushes the URL. A deleted or "gone" open chat replaces it with `/student/assistant`. A reload of `/student/assistant/X` reopens X (unlike the panel, where a reload starts fresh). | Deep links, back/forward and reloads work. |
| D10 | Exam in progress | When `status.examInProgress`: no list, no picker, no toolbar buttons, and no detail request. The chat column alone (full width) shows the context line, `AvatarConversation` (which renders the exam notice) and a disabled composer. | Same rule as the panel (`docs/avatar.md` Exam gate): the list and detail would return 403. |
| D11 | Plan limit display | Same as the panel: the Free-only line `avatar:panel.quota` («رسائل اليوم: X / N») above the log. The daily-limit notice and «اشترك» come from `AvatarConversation`/`AvatarNotice`. The composer is disabled at 0 remaining. The status refreshes after every send (`useAvatarChat`). | Parity. |
| D12 | Deletion flag | The delete icon is rendered only when `status.conversationDeletionEnabled`. The confirm dialog, toasts and error mapping come from the shared hook and dialog. Deleting the open chat clears it (`conversationDeleted`), and the URL goes to `/student/assistant`. | Parity, plus the URL rule. |
| D13 | Pending / streaming | No streaming. The student bubble shows at once, «المساعد يكتب…» (`role="status"`) shows while the reply is pending, and the composer is disabled. «محادثة جديدة» is disabled and list links do nothing (`preventDefault`, `aria-disabled`) while a send is in flight. | Otherwise a late reply would land in the wrong chat. |
| D14 | Errors and retry | Status load error: «تعذّر تحميل المساعد.» + «إعادة المحاولة» (avatar strings). List error: `avatar:history.error` + retry. Opening a chat: loading «جارٍ فتح المحادثة…»; `AVATAR_CONVERSATION_NOT_FOUND` gives «هذه المحادثة غير موجودة أو حُذفت.» + «محادثة جديدة»; any other error gives «تعذّر فتح المحادثة.» + retry. Picker error: «تعذّر تحميل المواد والدروس.» + retry, and chatting stays possible. Send failures stay inline notices, with no retry button (panel parity: the student re-sends). | |
| D15 | Accessibility | The message log is the shared `role="log" aria-live="polite"` region. After each send, focus returns to «سؤالك» once the composer is enabled again. There is no autofocus on page load (unlike the dialog). Headings: one `h1` (`avatar:panel.title`), an `h2` «محادثاتي السابقة» on the list and an `sr-only` `h2` «المحادثة» on the chat, both with `tabIndex={-1}` as focus targets. The current chat link has `aria-current="page"` (TanStack `Link`, `activeOptions={{ includeSearch: false }}`). | |
| D16 | Pagination | In the URL: `?page=N` (skill §4), validated by `assistantSearchSchema`. It is kept on every navigation the page makes and omitted when 1. | |
| D17 | Panel link | `Maximize2` icon-only ghost `Button asChild` + `Link`, `aria-label` = `avatar:panel.openFullPage` «فتح في صفحة كاملة». It renders under the **same condition as the history button** (`view === 'chat' && status !== undefined && !status.examInProgress`) and sits immediately before it. Target: `/student/assistant/$conversationId` when the panel has a `conversationId`, otherwise `/student/assistant`. `onClick` dispatches `close`. | This is the one allowed panel change. During an exam the link does not take the student off the exam screen. |
| D18 | i18n and bundle | Page-only strings go in a new **on-demand** namespace `assistant` (`i18n/assistant.{ar,en}.json`), registered by `registerAssistantLocales()` at the top of the page module (precedent: `avatarConversations`). Strings that are always loaded grow by three keys only: `shell:nav.student.assistant`, `avatar:panel.openFullPage`, and the capability. The page is imported only through a **second entry file** `features/avatar/assistant.ts`, never through `features/avatar/index.ts`. | The avatar barrel is on the student shell's critical path, and a module with a top-level side effect would be kept in that chunk. |
| D19 | Nav | New capability `avatarChat` (student only, mirrors the `Avatar.Chat` policy). Item `{ key: 'assistant', to: '/student/assistant', labelKey: 'nav.student.assistant', icon: Sparkles, capability: 'avatarChat' }` goes after `multiExam`. `topBarKeys` gains `'assistant'` after `'multiExam'`. `tabBarKeys` is unchanged, so the item lands in the mobile «المزيد» page automatically (`overflowItems`). | `Sparkles` is already in the student chunk (the dock). The desktop top bar has 6 pills; `overflow-x-auto` already handles narrow `lg` widths. |
| D20 | Budgets | New `assistant` entry. **No existing `maxKb` changes.** Allowed growth, measured on ea2e23e5 against this branch in brotli bytes: entry and landing at most +600 B, lesson and quiz at most +700 B (nav item, capability, three strings, `StudentAvatarDock`, and the new Tailwind utilities, which land in the global CSS). The implementer reports the 4-row delta table. | "must not grow beyond the nav entry"; Tailwind emits one global CSS file, so some new-class bytes are unavoidable. |

## Existing code touched
| File | Change |
|------|--------|
| `web/src/features/avatar/components/AvatarPanelHeader.tsx` | **Only panel change.** Import `Link` from `@tanstack/react-router` and `Maximize2` from `lucide-react`. Destructure `state` too: `const { state, dispatch } = useAvatar();`. Before the existing history-button block, add one block with the same condition (`view === 'chat' && status !== undefined && !status.examInProgress`): `<Button asChild variant="ghost" className="min-w-11 px-0">` wrapping `state.conversationId === null ? <Link to="/student/assistant" aria-label={t('panel.openFullPage')} onClick={() => { dispatch({ type: 'close' }); }}><Maximize2 aria-hidden className="size-5" /></Link> : <Link to="/student/assistant/$conversationId" params={{ conversationId: state.conversationId }} …same props…>…</Link>`. Nothing else in the file changes. |
| `web/src/features/avatar/i18n/ar.json` | `panel.openFullPage`: `"فتح في صفحة كاملة"` |
| `web/src/features/avatar/i18n/en.json` | `panel.openFullPage`: `"Open full page"` |
| `web/src/features/avatar/index.ts` | Add `export { StudentAvatarDock } from './components/StudentAvatarDock';` (keep every existing export). |
| `web/src/routes/student/route.tsx` | Import `StudentAvatarDock` instead of `AvatarDock`; `assistant={<StudentAvatarDock />}`. |
| `web/src/features/session/permissions.ts` | Add `'avatarChat'` to `capabilities` (after `'askTeacherSubmit'`) and to `roleCapabilities.student` (after `'askTeacherSubmit'`). |
| `web/src/features/shell/navConfig.ts` | Import `Sparkles`. Insert the D19 item after `multiExam`. `topBarKeys: ['home', 'progress', 'multiExam', 'assistant', 'ask', 'subscription']`. |
| `web/src/features/shell/i18n/ar.json` | `nav.student.assistant`: `"المساعد"` |
| `web/src/features/shell/i18n/en.json` | `nav.student.assistant`: `"Assistant"` |
| `web/src/styles/app.css` | After `@utility bottom-above-tab-bar`, add the two utilities shown below this table. |
| `web/scripts/perf/budgets.json` | Append `{ "name": "assistant", "entries": ["index.html", "src/routes/student/route.tsx?tsr-split=component", "src/routes/student/assistant.tsx?tsr-split=component"], "maxKb": <M> }`, with `M` = measured KB × 1.05, rounded up to the next 5 (`docs/performance.md` §3 rule). |
| `web/src/routeTree.gen.ts` | Regenerated by the router plugin (`npm run build`); commit the result. Never hand-edit it. |
| `web/src/features/shell/components/AppShell.test.tsx` | **modify** one test (row T47). |
| `web/src/features/shell/pages/MorePage.test.tsx` | **add** one test (row T48). |
| `web/src/features/session/permissions.test.ts` | **add** one test (row T49). |
| `docs/avatar.md`, `docs/PRD.md`, `docs/claude-design-prompt.md`, `docs/prototype.md`, `docs/performance.md`, `docs/design-system.md`, `.claude/design-system.md`, `docs/backlog.json` | See Docs. |

The two utilities for `web/src/styles/app.css`:

```css
@utility h-assistant {
  height: calc(100dvh - var(--ds-layout-bar) - var(--ds-space-1) * 30);
}

@utility h-assistant-desktop {
  height: calc(100dvh - var(--ds-layout-bar) - var(--ds-space-1) * 14);
}
```

They are the viewport minus the app bar and `main`'s padding: mobile `pt-6` + `pb-24` = 120 px, desktop `pt-6` + `lg:pb-8` = 56 px.

**Must not change** (proved by their tests passing untouched): `AvatarDock.tsx`, `AvatarPanel.tsx`, `AvatarConversation.tsx`, `AvatarComposer.tsx`, `AvatarMessageBubble.tsx`, `AvatarCitations.tsx`, `AvatarNotice.tsx`, `AvatarHistory.tsx`, `AvatarHistoryItem.tsx`, `DeleteAvatarConversationDialog.tsx`, `AvatarProvider.tsx`, `AskAvatarButton.tsx`, every file in `features/avatar/hooks/` and `api/` that exists today, `AvatarPanel.test.tsx`, `AvatarHistory.test.tsx`, `AvatarDock.test.tsx`, `AvatarDock.lazy.test.tsx`, `app/i18n.ts`, `AppShell.tsx`.

## Files to create
All paths are under `web/src/` unless absolute. TS strict, named exports, no default exports.

| # | Path | Type | Contract |
|---|------|------|----------|
| F1 | `routes/student/assistant.tsx` | route | `export const Route = createFileRoute('/student/assistant')({ validateSearch: assistantSearchSchema, component: AssistantRoute });` `function AssistantRoute() { const { conversationId } = useParams({ strict: false }); const { page } = Route.useSearch(); return <AssistantPage conversationId={conversationId} page={page ?? 1} />; }` Imports from `@/features/avatar/assistant`. |
| F2 | `routes/student/assistant.$conversationId.tsx` | route | `export const Route = createFileRoute('/student/assistant/$conversationId')({});` No component: the parent renders the page and no Outlet (D1). |
| F3 | `features/avatar/assistant.ts` | second public entry | `export { AssistantPage } from './pages/AssistantPage'; export { assistantSearchSchema, type AssistantSearch } from './schemas/assistantSearchSchema';` |
| F4 | `features/avatar/assistantLocales.ts` | locales | `export function registerAssistantLocales(): void` → `getI18n().addResourceBundle('ar', 'assistant', ar, true, true)` and the same for `'en'` (copy of `avatarConversations/locales.ts`). |
| F5 | `features/avatar/i18n/assistant.ar.json` | i18n | `{"newChat":"محادثة جديدة","picker":{"subject":"المادة","lesson":"الدرس","general":"عام","noLesson":"بدون درس محدد","locked":"{name} (للمشتركين)","hint":"اختيار المادة والدرس اختياري. بدونهما يجيبك المساعد بشكل عام.","loading":"جارٍ تحميل المواد والدروس…","error":"تعذّر تحميل المواد والدروس."},"open":{"loading":"جارٍ فتح المحادثة…","notFound":"هذه المحادثة غير موجودة أو حُذفت.","error":"تعذّر فتح المحادثة."}}` |
| F6 | `features/avatar/i18n/assistant.en.json` | i18n | Same keys: `"New chat"`, `"Subject"`, `"Lesson"`, `"General"`, `"No specific lesson"`, `"{name} (subscribers only)"`, `"Choosing a subject and lesson is optional. Without them the assistant answers in general."`, `"Loading subjects and lessons…"`, `"Subjects and lessons could not load."`, `"Opening the chat…"`, `"This chat does not exist or was deleted."`, `"The chat could not open."` |
| F7 | `features/avatar/schemas/assistantSearchSchema.ts` | schema | `export const assistantSearchSchema = z.object({ page: z.coerce.number().int().min(1).optional().catch(undefined) }); export type AssistantSearch = z.infer<typeof assistantSearchSchema>;` |
| F8 | `features/avatar/hooks/assistantReducer.ts` | reducer | See Domain behaviour. Exports `AssistantState`, `AssistantAction`, `globalContext`, `initialAssistantState`, `assistantReducer`. |
| F9 | `features/avatar/hooks/useAssistantConversation.ts` | hook | `export type AssistantOpeningPhase = 'idle' \| 'loading' \| 'notFound' \| 'error'; export interface AssistantOpening { phase: AssistantOpeningPhase; retry: () => void }` `export interface UseAssistantConversationArgs { routeConversationId: string \| undefined; page: number; state: AssistantState; dispatch: Dispatch<AssistantAction>; canLoad: boolean }` `export function useAssistantConversation(args): AssistantOpening`. Body: see Domain behaviour. Also `export function pageSearch(page: number): { page?: number }` → `page > 1 ? { page } : {}`. |
| F10 | `features/avatar/hooks/useAssistantLessonOptions.ts` | hook | `export interface AssistantLessonOption { id: string; name: string; isLocked: boolean }` `export interface AssistantLessonGroup { unitId: string; unitName: string; lessons: AssistantLessonOption[] }` `export function useAssistantLessonOptions(subjectId: string): { groups: AssistantLessonGroup[]; isPending: boolean; isError: boolean; retry: () => void }`. Steps: (1) `const subject = useGetStudentSubject(subjectId, { query: { enabled: subjectId !== '' } });` (2) `const units = (subject.data?.units ?? []).filter((u) => Number(u.lessonCount) > 0);` (3) `const results = useQueries({ queries: units.map((u) => getGetStudentUnitQueryOptions(u.id)) });` (4) `groups` = `units` in order, zipped with `results[i].data`, skipping units without data, with lessons mapped to `{ id, name, isLocked }`. (5) `isPending = subjectId !== '' && (subject.isPending \|\| results.some((r) => r.isPending))`. (6) `isError = subject.isError \|\| results.some((r) => r.isError)`. (7) `retry` refetches `subject` if it errored, and every errored result. |
| F11 | `features/avatar/pages/AssistantPage.tsx` | page (≤120 lines) | Module top: `registerAssistantLocales();`. `export interface AssistantPageProps { conversationId: string \| undefined; page: number }`. `export function AssistantPage({ conversationId, page })`. Steps: (1) `t` = `useTranslation('avatar')`; `titleId`, `listId` from `useId()`; `listHeadingRef` and `chatHeadingRef` = `useRef<HTMLHeadingElement>(null)`. (2) `const [state, dispatch] = useReducer(assistantReducer, initialAssistantState);` `const [mobileView, setMobileView] = useState<'chat' \| 'list'>('chat');` (3) `const status = useGetAvatarStatus();` `const examInProgress = status.data?.examInProgress === true;` `const isSending = useIsMutating({ mutationKey: getSendAvatarMessageMutationKey() }) > 0;` (4) `const opening = useAssistantConversation({ routeConversationId: conversationId, page, state, dispatch, canLoad: status.isSuccess && !examInProgress });` (5) `const controller: AvatarController = { state, dispatch, open: (context) => { dispatch({ type: 'newChat', context }); }, close: () => undefined };` (6) `showView(view)`: `flushSync(() => { setMobileView(view); }); (view === 'list' ? listHeadingRef : chatHeadingRef).current?.focus();` (7) `startNewChat`: `dispatch({ type: 'newChat', context: globalContext }); showView('chat');`. Render: `<AvatarControllerContext value={controller}><section aria-labelledby={titleId} className="flex h-assistant min-h-96 flex-col gap-4 lg:h-assistant-desktop">`, a header row `flex flex-wrap items-center justify-between gap-3` with `<h1 id={titleId} className="font-display text-h1 font-bold lg:text-h1-desktop">{t('panel.title')}</h1>` and, when `status.isSuccess && !examInProgress`, `<AssistantToolbar …/>`. Then: `status.isPending` → `<div role="status" aria-busy="true" className="text-ui text-text-muted">{t('panel.loading')}</div>`; `status.isError` → `<p role="alert" className="text-ui text-danger">{t('panel.statusError')}</p>` + secondary `Button` `t('panel.retry')` → `void status.refetch()`; otherwise `<div className="grid min-h-0 flex-1 grid-rows-1 gap-4 lg:grid-cols-3">` containing (unless `examInProgress`) `<AssistantConversationList id={listId} status={status.data} page={page} isSending={isSending} headingRef={listHeadingRef} className={mobileView === 'list' ? 'flex' : 'hidden lg:flex'} onBackToChat={() => { showView('chat'); }} onOpened={() => { showView('chat'); }} />` and `<AssistantChat status={status.data} opening={opening} openingId={state.openingId} headingRef={chatHeadingRef} className={cn(examInProgress ? 'lg:col-span-3' : 'lg:col-span-2', examInProgress \|\| mobileView === 'chat' ? 'flex' : 'hidden lg:flex')} onNewChat={startNewChat} />`. |
| F12 | `features/avatar/components/AssistantToolbar.tsx` | component | `export interface AssistantToolbarProps { listId: string; listOpen: boolean; newChatDisabled: boolean; onShowList: () => void; onNewChat: () => void }`. Renders `<div className="flex items-center gap-2">`: (a) `Button variant="secondary" className="lg:hidden" aria-expanded={listOpen} aria-controls={listId} onClick={onShowList}` with the `History` icon and `t('avatar:panel.history')`; (b) `Button variant="primary" disabled={newChatDisabled} onClick={onNewChat}` with the `Plus` icon and `t('assistant:newChat')`. The page passes `listOpen={mobileView === 'list'}`, `newChatDisabled={isSending}`, `onShowList={() => { showView('list'); }}`. |
| F13 | `features/avatar/components/AssistantConversationList.tsx` | component (≤120) | `export interface AssistantConversationListProps { id: string; status: AvatarStatusResult; page: number; isSending: boolean; headingRef: Ref<HTMLHeadingElement>; className: string; onBackToChat: () => void; onOpened: () => void }`. Steps: `const { list } = useAvatarHistory(page); const { remove, isPending } = useDeleteAvatarConversation(); const [pendingDelete, setPendingDelete] = useState<StudentAvatarConversationResult \| null>(null); const navigate = useNavigate();` `goToPage(next)` = `void navigate({ to: '.', search: (prev) => ({ ...prev, page: next > 1 ? next : undefined }) })`. `useEffect(() => { if (list.isSuccess && list.data.items.length === 0 && page > 1) goToPage(page - 1); }, […])` (after the last item of a page is deleted). Render `<section id={id} aria-labelledby={headingId} className={cn('min-h-0 flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:col-span-1', className)}>`, a row with `<h2 id={headingId} ref={headingRef} tabIndex={-1} className="font-display text-h3 font-semibold focus-visible:outline-hidden">{t('avatar:history.title')}</h2>` and a `Button variant="ghost" className="lg:hidden" onClick={onBackToChat}` (`ArrowLeft` with `rtl:rotate-180`, `t('avatar:panel.backToChat')`). States, using the avatar keys exactly as `AvatarHistory`: pending → `role="status" aria-busy` `history.loading`; error → `role="alert"` `history.error` + secondary retry `panel.retry` → `list.refetch()`; empty → `history.empty`; otherwise `<ul className="flex min-h-0 flex-1 flex-col gap-2 overflow-y-auto">` of `AssistantConversationItem` (`canDelete={status.conversationDeletionEnabled}`, `page`, `isSending`, `onOpened`, `onDelete={setPendingDelete}`), then `<Pagination page totalPages onPageChange={goToPage} />` when `totalPages > 1`. Always `<DeleteAvatarConversationDialog conversation={pendingDelete} isPending={isPending} onOpenChange={(o) => { if (!o) setPendingDelete(null); }} onConfirm={() => pendingDelete === null ? Promise.resolve() : remove(pendingDelete.id)} />`. |
| F14 | `features/avatar/components/AssistantConversationItem.tsx` | component | `export interface AssistantConversationItemProps { conversation: StudentAvatarConversationResult; canDelete: boolean; page: number; isSending: boolean; onOpened: () => void; onDelete: (c: StudentAvatarConversationResult) => void }`. `title = conversationTitle(conversation) ?? t('avatar:history.general')`; `date = formatDateTime(conversation.lastMessageAt, i18n.language)`. `<li className="flex items-start gap-2 rounded-md border border-border bg-surface p-3 has-[[data-status=active]]:border-accent has-[[data-status=active]]:bg-accent-soft">` containing a `<Link to="/student/assistant/$conversationId" params={{ conversationId: conversation.id }} search={pageSearch(page)} activeOptions={{ includeSearch: false }} aria-disabled={isSending \|\| undefined} onClick={(e) => { if (isSending) { e.preventDefault(); return; } onOpened(); }} className="flex min-h-11 flex-1 flex-col items-start gap-1 rounded-sm text-start focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden">` with three spans like `AvatarHistoryItem` (title `text-ui font-semibold`; `history.meta` with `history.entryPoint.{entryPoint}` and date; `firstQuestion` `line-clamp-2 text-caption`). When `canDelete`, a ghost danger icon `Button` (`Trash2`, `aria-label={t('avatar:history.delete', { title })}`, `className="min-h-11 min-w-11 px-0 text-danger"`) → `onDelete(conversation)`. |
| F15 | `features/avatar/components/AssistantChat.tsx` | component (≤120) | `export interface AssistantChatProps { status: AvatarStatusResult; opening: AssistantOpening; openingId: string \| null; headingRef: Ref<HTMLHeadingElement>; className: string; onNewChat: () => void }`. `const { state, dispatch } = useAvatar();` (`state` is `AvatarState`; `openingId` comes from the prop). Root `<section aria-labelledby={headingId} className={cn('min-h-0 flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5', className)}>` + `<h2 id={headingId} ref={headingRef} tabIndex={-1} className="sr-only">{t('avatar:panel.messages')}</h2>`. Then by `opening.phase`: `loading` → `role="status" aria-busy="true"` `assistant:open.loading`; `notFound` → `role="alert"` `assistant:open.notFound` + primary `Button` `assistant:newChat` → `onNewChat`; `error` → `role="alert"` `assistant:open.error` + secondary `Button` `avatar:panel.retry` → `opening.retry`; `idle` → (a) if `!status.examInProgress && state.conversationId === null && openingId === null && state.messages.length === 0`: `<AssistantContextPicker context={state.context} onChange={(context) => { dispatch({ type: 'newChat', context }); }} />`, else `<p className="text-caption text-text-muted">{t('avatar:panel.context', { title: state.context.title ?? t('avatar:panel.contextGlobal') })}</p>`; (b) if `status.tier === 'Free'`: the `avatar:panel.quota` line (same markup as `AvatarPanel`); (c) `<AvatarConversation status={status} />`; (d) `<AssistantComposer status={status} />`. |
| F16 | `features/avatar/components/AssistantContextPicker.tsx` | component (≤120) | `export interface AssistantContextPickerProps { context: AvatarContextInput; onChange: (context: AvatarContextInput) => void }`. `const [subjectId, setSubjectId] = useState(''); const overview = useGetMasteryOverview(); const lessons = useAssistantLessonOptions(subjectId);` `hintId`, `subjectFieldId`, `lessonFieldId` via `useId`. Overview pending, or `subjectId !== '' && lessons.isPending` → `<p role="status" aria-busy="true" className="text-caption text-text-muted">{t('assistant:picker.loading')}</p>` (rendered in place of the lesson options only when the overview is ready; see the layout below). `overview.isError \|\| lessons.isError` → `role="alert"` `assistant:picker.error` + secondary `size="sm"` retry `avatar:panel.retry` → `overview.isError ? void overview.refetch() : lessons.retry()`. Layout: `<div className="flex flex-col gap-2"><div className="grid gap-3 md:grid-cols-2">` two `flex flex-col gap-1.5` cells, each with a `Label` + `Select`. **Subject:** `value={subjectId}`, `aria-describedby={hintId}`, option `''` = `assistant:picker.general`, then `overview.data.subjects` (`subjectId`/`name`); `onChange` → `setSubjectId(v); onChange(globalContext);`. **Lesson:** `value={context.entryPoint === 'Lesson' ? (context.lessonId ?? '') : ''}`, `disabled={subjectId === '' \|\| lessons.isPending}`, `aria-describedby={hintId}`, option `''` = `assistant:picker.noLesson`, then `<optgroup label={unitName}>` per group, with each `<option value={id} disabled={isLocked}>{isLocked ? t('assistant:picker.locked', { name }) : name}</option>`; `onChange` → `v === '' ? onChange(globalContext) : onChange({ entryPoint: 'Lesson', lessonId: v, title: <lesson name> })`. Then `<p id={hintId} className="text-caption text-text-muted">{t('assistant:picker.hint')}</p>`. |
| F17 | `features/avatar/components/AssistantComposer.tsx` | component | `export interface AssistantComposerProps { status: AvatarStatusResult }`. `const chat = useAvatarChat(); const queryClient = useQueryClient(); const sentRef = useRef(false); const blocked = status.examInProgress \|\| Number(status.messagesRemainingToday) === 0 \|\| chat.isPending;` `form = useForm<AvatarMessageValues>({ resolver: zodResolver(avatarMessageSchema(Number(status.messageMaxLength))), defaultValues: { message: '' }, disabled: blocked })`. `useEffect(() => { if (!blocked && sentRef.current) { sentRef.current = false; form.setFocus('message'); } }, [blocked, form]);` `<Form form={form} className="flex-row items-start gap-2" onSubmit={async (values) => { sentRef.current = true; await chat.send(values.message); form.reset(); await queryClient.invalidateQueries({ queryKey: getGetMyAvatarConversationsQueryKey() }); }}>` then `<div className="min-w-0 flex-1"><TextField<AvatarMessageValues> name="message" label={t('avatar:composer.label')} placeholder={t('avatar:composer.placeholder')} /></div>` and `<div className="mt-6.5 shrink-0"><SubmitButton disabled={blocked}>{t('avatar:composer.send')}</SubmitButton></div>`. |
| F18 | `features/avatar/components/StudentAvatarDock.tsx` | component | `export function StudentAvatarDock() { const onAssistantPage = useMatch({ from: '/student/assistant', shouldThrow: false }) !== undefined; return onAssistantPage ? null : <AvatarDock />; }` |
| F19–F27 | test files | tests | See Test plan. |

**Note on F15 (binding):** `AvatarController.state` is typed `AvatarState`, so `AssistantChat` reads `conversationId`, `messages` and `context` from `useAvatar().state` and gets `openingId` as a prop; the page passes `openingId={state.openingId}`. No type casts anywhere.

## Error codes
No new codes and no backend change. The UI handles these existing codes:

| Constant | Thrown by | HTTP | UI |
|----------|-----------|------|----|
| `AVATAR_EXAM_IN_PROGRESS` | send / list / detail | 403 | send: notice `examInProgress` (shared); list and detail are never requested during an exam (D10) |
| `AVATAR_DAILY_LIMIT_REACHED` | send | 403 | notice `dailyLimit` + «اشترك» (shared) |
| `AI_SERVICE_UNAVAILABLE` | send | 503 | notice `unavailable` (shared) |
| `AVATAR_CONVERSATION_NOT_FOUND` | send | 404 | notice `conversationGone`; URL → `/student/assistant` |
| `AVATAR_CONVERSATION_NOT_FOUND` | detail | 404 | `assistant:open.notFound` + «محادثة جديدة» |
| `AVATAR_CONVERSATION_DELETION_DISABLED` | delete | 400 | toast `avatar:toast.deleteDisabled` (shared hook) |
| any other | detail | — | `assistant:open.error` + retry |

The Arabic and English strings are in F5/F6 and the existing `avatar` JSON.

## Domain behaviour
There is no domain entity; this is the page state machine.

**`hooks/assistantReducer.ts` (F8):**
```ts
export interface AssistantState extends AvatarState { openingId: string | null }
export type AssistantAction =
  | AvatarAction
  | { type: 'newChat'; context: AvatarContextInput }
  | { type: 'opening'; conversationId: string };
export const globalContext: AvatarContextInput = { entryPoint: 'Global' };
export const initialAssistantState: AssistantState = { ...initialAvatarState, openingId: null };

export function assistantReducer(state: AssistantState, action: AssistantAction): AssistantState {
  switch (action.type) {
    case 'newChat':
      return { ...state, view: 'chat', context: action.context, messages: [], conversationId: null, openingId: null };
    case 'opening':
      return { ...state, view: 'chat', context: globalContext, messages: [], conversationId: null, openingId: action.conversationId };
    default: {
      const next = avatarReducer(state, action);
      const clearsOpening =
        action.type === 'resumed' ||
        (action.type === 'conversationDeleted' && action.conversationId === state.openingId);
      return { ...next, openingId: clearsOpening ? null : state.openingId };
    }
  }
}
```

**`useAssistantConversation` (F9), in this exact order:**
1. `const navigate = useNavigate();` `const [syncedRouteId, setSyncedRouteId] = useState<string | undefined | null>(null);`
2. Route changed (in render, the same-component state-adjust pattern used by `AvatarHistory`):
   ```ts
   if (syncedRouteId !== routeConversationId) {
     setSyncedRouteId(routeConversationId);
     if (routeConversationId === undefined) {
       if (state.conversationId !== null || state.openingId !== null) dispatch({ type: 'newChat', context: globalContext });
     } else if (routeConversationId !== state.conversationId && routeConversationId !== state.openingId) {
       dispatch({ type: 'opening', conversationId: routeConversationId });
     }
   }
   ```
3. `const detail = useGetMyAvatarConversation(state.openingId ?? '', { query: { enabled: state.openingId !== null && canLoad, staleTime: 0 } });`
4. Resume (in render): `if (state.openingId !== null && detail.isSuccess && !detail.isFetching && detail.data.id === state.openingId) dispatch({ type: 'resumed', ...toResumed(detail.data) });`
5. URL follows state:
   ```ts
   useEffect(() => {
     if (state.openingId !== null) return;
     if (state.conversationId !== null && state.conversationId !== routeConversationId) {
       void navigate({ to: '/student/assistant/$conversationId', params: { conversationId: state.conversationId }, search: pageSearch(page), replace: true });
     } else if (state.conversationId === null && routeConversationId !== undefined) {
       void navigate({ to: '/student/assistant', search: pageSearch(page), replace: true });
     }
   }, [state.openingId, state.conversationId, routeConversationId, page, navigate]);
   ```
6. Return:
   - `phase: 'idle'` when `state.openingId === null`;
   - else `'notFound'` when `detail.isError && detail.error instanceof ApiError && detail.error.code === 'AVATAR_CONVERSATION_NOT_FOUND'`;
   - else `'error'` when `detail.isError`;
   - else `'loading'`;
   - `retry: () => { void detail.refetch(); }`.

| Scenario | Expected trace |
|---|---|
| New chat, first reply | `replied` sets `conversationId=Z`; the effect replaces the URL with `/Z`; the route change sees `Z === conversationId`, so there is no dispatch and the messages stay |
| Deep link `/X` | first render: `opening(X)`, then load, then `resumed` |
| List item Y while on X | push `/Y`; `opening(Y)` clears X, then load |
| Back to `/student/assistant` | `newChat(Global)` |
| «محادثة جديدة» on `/X` | `newChat`; the effect replaces the URL with `/student/assistant` |
| Delete open chat X | `conversationDeleted` clears it; the effect replaces the URL with `/student/assistant`, and the picker shows |
| Send finds X gone | `failed(conversationGone)`: `conversationId=null`, messages kept; the effect replaces the URL with `/student/assistant`; no reset (the conversation is already null), so the notice stays |
| Exam in progress on `/X` | `opening(X)`; the detail is disabled until `canLoad`; the chat column shows the exam notice |

## API surface
No API change. Web routes:

| Route | Component | Search | Guard |
|---|---|---|---|
| `/student/assistant` | `AssistantPage` (lazy chunk `assistant.tsx?tsr-split=component`) | `assistantSearchSchema` (`page?`) | inherited `/student` `beforeLoad` (student, onboarded) |
| `/student/assistant/$conversationId` | none (the parent renders) | inherited | inherited |

Endpoints used, through existing generated hooks: `useGetAvatarStatus`, `useSendAvatarMessage` (in `useAvatarChat`), `useGetMyAvatarConversations` (in `useAvatarHistory`), `useGetMyAvatarConversation`, `useDeleteMyAvatarConversation` (in `useDeleteAvatarConversation`), `useGetMasteryOverview`, `useGetStudentSubject`, `getGetStudentUnitQueryOptions` + `useQueries`.

## Docs
| File | Change |
|---|---|
| `docs/avatar.md` | **Entry points** table: in the `Lesson` row's "Where", append "; on the full page, a lesson picked for a new chat". In the `Global` row, append "; a new chat on the full page with no lesson picked". **Context bundle → Global**: replace "there is no lesson picker" with "the panel has no lesson picker (the full page has an optional one, see Full page)". **Student history**: "inside the assistant panel («محادثاتي السابقة»)" becomes "inside the assistant panel («محادثاتي السابقة») and on the full page". **Exam gate**: add "the full page shows only the exam notice: no chat list, picker or «محادثة جديدة»". **Runtime flag**: "the panel and the full page hide the delete buttons". **Streaming**: "the panel and the full page show «المساعد يكتب…»". **UI**: add a bullet on the header icon link «فتح في صفحة كاملة» (chat view, hidden during an exam). It opens `/student/assistant/{conversationId}` of the open chat, or `/student/assistant`, and closes the panel. **New `## Full page` section** after UI, covering D1, D4–D16 and D18 in prose: the routes; the layout (list on the start side, chat; mobile back pattern); «محادثة جديدة»; the picker rules (subject is a filter, a lesson gives `Lesson`, locked lessons disabled); the URL rules (first reply puts the id in the URL, reload reopens, a deleted or missing chat returns to `/student/assistant`); the dock being hidden on the page; the panel and the page each keeping their own open conversation (messages sent on the page appear in the panel after reopening it from history); focus after send; the live region; and the bundle note (own route chunk, `assistant` strings registered on load, budget `assistant`). |
| `docs/PRD.md` | §9.1: after the table, add one paragraph: the full page «المساعد» (`/student/assistant`, `/student/assistant/{id}`) with past chats beside the conversation, «محادثة جديدة», an optional subject and lesson picker (no lesson: Global; a lesson: Lesson), and the same endpoints, limits, exam refusal and deletion setting as the panel; the panel header links to it. In the §9.1 table's `Global` row, change "asks the student to pick a lesson" to "asks the student to pick a lesson (the full page offers an optional picker)". §9.4 first bullet: "inside the assistant panel («محادثاتي السابقة»)" becomes "inside the assistant panel («محادثاتي السابقة») and on the full assistant page". |
| `docs/claude-design-prompt.md` | §4 Student, panel bullet: "The general context has no lesson picker" becomes "The panel's general context has no lesson picker". Add after «محادثاتي السابقة» … : "The header also has an icon link «فتح في صفحة كاملة» (chat view, hidden during an exam) that closes the panel and opens the full page on the open chat." Add a new bullet `#/student/assistant` (nav «المساعد»; not simulated by the prototype) listing: H1 «المساعد الذكي», «محادثاتي السابقة» toggle on mobile, «محادثة جديدة», the list, the picker and its hint, the context line, the quota line, the conversation, the composer, every state in D14, the exam state D10 and `/student/assistant/:id`. §6: add a "**Chat page** (assistant)" bullet: two columns from 900 px (list one third on the start side, chat two thirds), both white cards; the page fills the viewport below the app bar; the log scrolls inside the card with the composer at the bottom (field and «إرسال» in one row); under 900 px one column with the list behind «محادثاتي السابقة». |
| `docs/prototype.md` | Line 33 parenthetical: after "the prototype does not simulate it", add "nor the full assistant page `/student/assistant` (past chats beside the conversation)". |
| `docs/performance.md` | §3 table: add the row `assistant` · `+ student/route, student/assistant` · measured · budget. §4 "Lazy avatar panel" bullet: append "The full assistant page is its own route chunk, and registers its `assistant` strings when the chunk loads, so no other page carries them." |
| `docs/design-system.md` | §5.10 Avatar panel: append "Full page (`/student/assistant`): the list and the chat are white cards (`--r-lg`, `--shadow-1`) in a 1 : 2 grid from 900 px; the current chat item is `--accent-soft` with an `--accent` border; the bubbles are the panel's." |
| `.claude/design-system.md` | Component table: after `AssistantFab`, add the row `\| AssistantPage \| — \| default, list (mobile) \| two Cards 1:2 from lg, height = viewport − app bar − main padding (`h-assistant`), log scrolls inside the chat card, current item accent.soft + accent border; under lg one column with a «محادثاتي السابقة» toggle \|`. Run `npm run gen:tokens`; it must produce no diff. |
| `docs/backlog.json` | Append epic `{ "key": "E20", "title": "Assistant full page", "labels": ["v1", "student", "frontend"], "description": "Dev request 2026-10-04: a full-page AI assistant chat beside the floating panel, which stays as it is. Epic #287.", "stories": [{ "title": "Full-page AI assistant chat", "description": "As a student I want a full-page AI assistant that feels like a normal chat app, alongside the existing floating assistant panel. Issue #288.", "tasks": [the 4 sub-task lines from the story, verbatim] }] }`. |

## Parallel lane (#286, E19.S4) — shared files
| File | #288 edits | #286 may edit | Resolution |
|---|---|---|---|
| `web/src/styles/app.css` | adds 2 `@utility` blocks | shared fieldset/legend fix | independent hunks; the second to merge rebases |
| `docs/claude-design-prompt.md` §4/§6 | panel bullet, new assistant bullet, Chat page bullet | progress / multi-exam bullets, maybe §6 forms | different bullets; rebase |
| `docs/design-system.md`, `.claude/design-system.md` | §5.10 sentence, AssistantPage row | fieldset / option-card styles | different rows; re-run `gen:tokens` after rebase |
| `docs/performance.md`, `web/scripts/perf/budgets.json` | new row and entry | only if they re-measure | the second to merge re-runs `perf:budget` and updates measured values |
| `docs/backlog.json` | appends E20 at the end of `epics` | may add E19.S4 or an E19 epic at the end | likely textual conflict at the array tail; keep both |
| `web/src/routeTree.gen.ts` | regenerated (new routes) | none expected | regenerate after rebase |
| `features/shell/navConfig.ts`, shell i18n, `app/i18n.ts` | navConfig and shell i18n only; `app/i18n.ts` **not** touched | none expected | — |
| `web/src/shared/ui/select.tsx` | **not** touched (max width set at use site only) | possibly | — |

## Test plan
Rendering is through `renderApp(path, { session: testSessions.student })`. In every `AssistantPage*` file, `beforeEach` adds `getGetMyAvatarConversationsMockHandler(myAvatarConversationsPage([]))` and `getGetMasteryOverviewMockHandler(masteryOverview())`. The status handler is the server default (`avatarStatus()`). Use `const user = userEvent.setup()` and role queries. The URL is asserted through the returned `router.state.location`. Pending replies are held with an MSW handler that awaits a test-controlled promise.

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| T1 | `hooks/assistantReducer.test.ts` › `assistantReducer` | `starts a new chat with the given context and clears the open one` | from a state with messages, conversationId and openingId: `newChat(Lesson ctx)` gives empty messages, `conversationId` null, `openingId` null, the context set |
| T2 | 〃 | `clears the chat and remembers the id while a conversation opens` | `opening('X')` gives `openingId 'X'`, messages `[]`, Global context |
| T3 | 〃 | `clears the opening id when the conversation resumes` | after `opening`, `resumed` gives `openingId` null, `conversationId` set, messages from the action |
| T4 | 〃 | `clears the opening id when the opening conversation is deleted` | `conversationDeleted('X')` while opening X gives `openingId` null; deleting another id keeps it |
| T5 | 〃 | `delegates other actions to the avatar reducer and keeps the opening id` | `sent` appends a student message; `openingId` is unchanged |
| T6 | `schemas/assistantSearchSchema.test.ts` › `assistantSearchSchema` | `parses a page number` | `{ page: '3' }` gives `{ page: 3 }` |
| T7 | 〃 | `drops an invalid page` | `{ page: '0' }` gives `{ page: undefined }` |
| T8 | `assistantLocales.test.ts` › `assistant locales` | `does not ship the assistant namespace until registered` | `i18n.hasResourceBundle('en', 'assistant')` is false |
| T9 | 〃 | `resolves assistant:newChat after registering` | en «New chat», ar «محادثة جديدة» |
| T10 | `pages/AssistantPage.test.tsx` › `AssistantPage` | `shows the general greeting, the context picker and today's quota for a free student` | h1 «AI assistant»; /Open the lesson you need/; comboboxes «Subject» and «Lesson» (the lesson one disabled); «Today's messages: 2 / 5» with `freeAvatarStatus({ messagesUsedToday: 2, messagesRemainingToday: 3 })` |
| T11 | 〃 | `sends a question, shows the reply with its sources and puts the chat in the URL` | reply text; link «Explanation — قانون أوم» has `href` `/student/lesson/{browseLessonId}`; body `{ entryPoint: 'Global', lessonId: null, sessionId: null, questionId: null, conversationId: null, message }`; pathname is `/student/assistant/{avatarConversationId}` |
| T12 | 〃 | `continues the same conversation on the next question` | second body `conversationId === avatarConversationId`; both replies visible (no reset after the URL change) |
| T13 | 〃 | `returns focus to the question field when the reply arrives` | after clicking «Send», textbox «Your question» `toHaveFocus()` (waitFor) |
| T14 | 〃 | `shows the exam refusal without the chat list, picker or new chat while an exam is in progress` | exam notice text; textbox and «Send» disabled; no heading «Past chats»; no button «New chat»; no combobox «Subject»; and, starting at `/student/assistant/{myAvatarConversationId}`, no detail request (the detail handler records no call) |
| T15 | 〃 | `shows the daily limit notice with a subscribe link and disables the composer` | free status with 0 remaining: limit text; link «Subscribe» `href` `/student/subscription`; textbox disabled |
| T16 | 〃 | `shows an inline notice when the assistant is unavailable` | 503 `AI_SERVICE_UNAVAILABLE` gives the notice text, and the student text stays visible |
| T17 | 〃 | `shows the status error with retry and recovers` | `once` 500: «The assistant could not load.» → «Retry» → greeting shown |
| T18 | 〃 | `starts a new chat from the new chat button` | after a reply, click «New chat»: reply text gone, greeting shown, pathname `/student/assistant`, combobox «Subject» back |
| T19 | 〃 | `disables new chat and past chats while a reply is pending` | gated send: «New chat» disabled; clicking a list item link keeps the pathname; after release the reply shows |
| T20 | 〃 | `starts a new chat after the open one no longer exists` | 2nd send 404 `AVATAR_CONVERSATION_NOT_FOUND`: the «This chat no longer exists…» notice; pathname `/student/assistant`; the notice is still visible; 3rd body `conversationId` null |
| T21 | 〃 | `hides the floating assistant button on the assistant page` | `queryByRole('button', { name: 'Assistant' })` is null; the nav link «Assistant» has `aria-current="page"` |
| T22 | 〃 | `renders right to left in Arabic` | `lng: 'ar'`: h1 «المساعد الذكي»; `document.documentElement` `dir="rtl"` |
| T23 | 〃 | `has no axe violations` | `axe(container).violations` is `[]` once the textbox shows (with one list item present) |
| T24 | `pages/AssistantPage.history.test.tsx` › `AssistantPage history` | `lists past chats newest first with title, date and first question` | two items in order; link names contain «قانون أوم» / «General» and the first questions |
| T25 | 〃 | `shows the empty state when there are no past chats` | «You have no past chats yet.» |
| T26 | 〃 | `shows the list error with retry and recovers` | `once` 500: «Your chats could not load.» → «Retry» → item shown |
| T27 | 〃 | `opens a past chat with its messages and context and marks it current` | click item: «ما هو قانون أوم؟» and the reply visible; «Context: قانون أوم»; pathname `/student/assistant/{myAvatarConversationId}`; that link has `aria-current="page"`; no «Subject» combobox |
| T28 | 〃 | `continues a reopened chat with its conversation id and context` | body `{ entryPoint: 'Lesson', lessonId: browseLessonId, conversationId: myAvatarConversationId }` |
| T29 | 〃 | `opens a chat from a deep link` | `renderApp('/student/assistant/{id}')`: «Opening the chat…» then the messages |
| T30 | 〃 | `shows not found for an unknown chat and starts a new one` | detail 404 code gives «This chat does not exist or was deleted.»; «New chat» → pathname `/student/assistant`, greeting shown |
| T31 | 〃 | `shows the open error with retry and recovers` | detail `once` 500: «The chat could not open.» → «Retry» → messages |
| T32 | 〃 | `deletes a chat after confirmation` | trash «Delete chat: قانون أوم» → dialog «Delete this chat?» → «Delete»: toast «The chat was deleted.»; item gone (list handler updated) |
| T33 | 〃 | `returns to a new chat when the open chat is deleted` | open the item, then delete it: pathname `/student/assistant`; messages gone; «Subject» combobox shown |
| T34 | 〃 | `hides delete when deleting chats is turned off` | `conversationDeletionEnabled: false`: no «Delete chat: …» button; the item link is still there |
| T35 | 〃 | `moves to the next page and keeps it in the URL` | `totalPages: 2`; «Next» gives request `pageNumber=2` and `location.search.page === 2` |
| T36 | 〃 | `switches between the chat and the list with focus on the shown heading` | «Past chats» button `aria-expanded="false"` → click: `aria-expanded="true"` and heading «Past chats» focused; «Back to the chat» → heading «Conversation» focused |
| T37 | `pages/AssistantPage.context.test.tsx` › `AssistantPage context picker` | `lists the subjects and the lessons of the chosen subject by unit` | options «General», «Physics», «Chemistry»; choosing «Physics» enables «Lesson» with group «Mechanics» and options «Forces», «Energy» (`getGetStudentSubjectMockHandler(studentSubject({ id: physicsId }))`, `getGetStudentUnitMockHandler(studentUnit())`) |
| T38 | 〃 | `sends the picked lesson as the lesson context with the lesson greeting` | pick «Energy»: greeting /assistant for "Energy"/; body `{ entryPoint: 'Lesson', lessonId: browseLessonId, conversationId: null }` |
| T39 | 〃 | `disables locked lessons for a free student` | unit with `isLocked: true` on «Energy»: option «Energy (subscribers only)» `toBeDisabled()` |
| T40 | 〃 | `returns to the general context when the subject changes` | pick lesson, then subject «General»: lesson value `''`, global greeting; the next body has `entryPoint: 'Global'` |
| T41 | 〃 | `shows the context line instead of the picker once the chat has started` | after a send with a lesson: no «Subject» combobox; «Context: Energy» |
| T42 | 〃 | `shows the lessons error with retry and recovers` | unit `once` 500: «Subjects and lessons could not load.» → «Retry» → option «Forces» |
| T43 | `components/AvatarPanelHeader.test.tsx` › `AvatarPanelHeader full-page link` | `opens the full page from the panel header and closes the panel` | from `/student`, open the dock → link «Open full page» → no dialog; pathname `/student/assistant`; h1 «AI assistant» |
| T44 | 〃 | `links the full page to the open chat` | after one panel reply, the link has `href` `/student/assistant/{avatarConversationId}` |
| T45 | 〃 | `hides the full-page link during an exam` | `examInProgress: true`: no link «Open full page» in the dialog |
| T46 | (existing, unchanged) | `AvatarPanel.test.tsx`, `AvatarHistory.test.tsx`, `AvatarDock.test.tsx`, `AvatarDock.lazy.test.tsx`, `avatarReducer.test.ts`, `navConfig.test.ts` | pass with **zero edits** |
| T47 | `features/shell/components/AppShell.test.tsx` › `AppShell` | **modify** `shows every student destination in the top tabs` | expected `['Home', 'My progress', 'Multi-unit exam', 'Assistant', 'Ask a teacher', 'Subscription']` (only this array changes) |
| T48 | `features/shell/pages/MorePage.test.tsx` › `MorePage` | **add** `lists the student destinations that are not in the tab bar` | `/student/more` main links equal `['Multi-unit exam', 'Assistant', 'Subscription']` |
| T49 | `features/session/permissions.test.ts` › `can` | **add** `grants a student the assistant chat and denies other roles` | student true; teacher and admin false for `avatarChat` |

No new fixture file. Use `avatarFixtures`, `browseFixtures` and `masteryFixtures` (`physicsId`); in T37–T42, pass `studentSubject({ id: physicsId })`.

## Definition of done
- [ ] `git diff ea2e23e5 -- web/src/features/avatar` shows `AvatarPanelHeader.tsx` as the only modified existing component, with one added link block (+ imports and `state` destructure), and no other existing avatar `.tsx`/`.ts` modified apart from `index.ts` (one export line) and the two avatar JSON keys.
- [ ] T46 files pass with no edits (`git diff ea2e23e5 --stat` lists none of them).
- [ ] `/student/assistant` and `/student/assistant/$conversationId` work; the parent has no Outlet and the child has no component; `routeTree.gen.ts` is regenerated.
- [ ] The page lives only behind `features/avatar/assistant.ts`; `features/avatar/index.ts` does not export `AssistantPage`.
- [ ] `assistant` namespace registered on demand; `app/i18n.ts` unchanged; ar and en keys identical.
- [ ] Nav: «المساعد» in the desktop top bar and on `/student/more`; capability `avatarChat` is for the student only.
- [ ] The panel header link follows D17 exactly (condition, target, close).
- [ ] The dock is hidden on the assistant routes and shown elsewhere (AppShell test 'shows the assistant button to students' still passes).
- [ ] Exam: no list, detail, picker or toolbar; notice shown; composer disabled.
- [ ] Deletion flag hides delete; deleting the open chat returns to `/student/assistant`.
- [ ] Free quota line, daily-limit notice and «اشترك» shown; composer disabled at 0.
- [ ] Locked lessons disabled in the picker; a subject alone sends `Global`.
- [ ] The message log is the shared `role="log" aria-live="polite"` region; focus returns to «سؤالك» after a reply; mobile toggle focus follows D5.
- [ ] No arbitrary Tailwind values; logical properties only; the only new CSS is the two `@utility` blocks.
- [ ] `npm run typecheck`, `npm run lint`, prettier check, `npm test -- --run --coverage` (features ≥ 80 % lines and 70 % branches), `npm run build`, `npm run perf:budget` all exit 0; `npm run gen:api` and `npm run gen:tokens` produce no diff.
- [ ] `budgets.json`: the new `assistant` entry; no other `maxKb` changed; the report has a before/after brotli-byte table for entry, landing, lesson and quiz within D20's limits, plus the assistant measured size.
- [ ] Docs updated per the Docs table (avatar.md Full page section, PRD §9.1/§9.4, design prompt §4/§6, prototype.md, performance.md, both design-system files in agreement, backlog.json E20).
- [ ] Tests T1–T45 and T47–T49 exist with exactly these names; no other existing test edited.
