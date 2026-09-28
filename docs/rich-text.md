# Rich text format

The storage and rendering contract for lesson rich text (`Lesson.Explanation` and `Lesson.Summary`) and question rich text (`Question.Stem`, `Question.Explanation` and each choice option `text` inside `Question.Body`). The sanitiser allow-list (`RichTextSanitizer`), the editor extensions (`RichTextEditor`) and this document change together.

## Storage

Lesson explanation and summary are stored as **sanitised HTML** in the `text` columns `Lessons.Explanation` and `Lessons.Summary`. The HTML is the TipTap `editor.getHTML()` output of the admin lesson editor, sanitised on the server before it is saved. An empty field is stored as an empty string. Question stem and explanation are stored in `text` columns, and choice option text inside the `jsonb` body, all sanitised the same way.

## Allowed markup

| Kind | Allowed |
|---|---|
| Tags | `p` `br` `strong` `em` `u` `s` `code` `pre` `blockquote` `h2` `h3` `ul` `ol` `li` `a` `hr` `img` `span` `div` |
| Attributes | `href` `src` `alt` `start` `data-type` `data-latex` |
| URL schemes (`href`, `src`) | `https` `http` `mailto` |

- No `style` and no `class` attributes, and no CSS properties.
- Everything else (scripts, event handlers, iframes, forms, other attributes) is removed.
- `img[src]` is kept only when it starts with `FileStorage:PublicBaseUrl` followed by `/` (default `/api/media/`). Any other image source is dropped, so lessons carry no tracking pixels or hotlinked images.

## Math

LaTeX lives in the `data-latex` attribute, never in text delimiters such as `$...$`.

| Node | Shape |
|---|---|
| Inline | `<span data-type="inline-math" data-latex="F=ma"></span>` |
| Block | `<div data-type="block-math" data-latex="\sum_{i=1}^{n} x_i"></div>` |

The editor produces these shapes with `@tiptap/extension-mathematics`, which also renders the LaTeX live inside the editor.

## Sanitisation

- **Server, on every save:** `UpdateLessonHandler` passes both fields through `IRichTextSanitizer` (implemented by `RichTextSanitizer` on `HtmlSanitizer`) before `Lesson.Update`. No raw request HTML reaches the domain.
- **Client, on every render:** `SafeHtml` runs `DOMPurify.sanitize` before it sets the HTML. It is the only component in the web app that sets raw HTML (defence in depth).

## Rendering

- `RichTextViewer` is the only renderer of lesson rich text. It runs `renderMath(html)` and then `SafeHtml`.
- `renderMath` replaces the content of every `[data-type="inline-math"]` and `[data-type="block-math"]` node with KaTeX `renderToString` output. `block-math` renders in `displayMode`. `throwOnError` is `false`, so invalid LaTeX shows as a KaTeX error instead of breaking the page.
- The `.rich-text` class styles the output with design tokens only. LaTeX, code and preformatted text are `direction: ltr` with `unicode-bidi: isolate`, so they read correctly inside Arabic text.

## Images

- Upload: `POST /api/lessons/{lessonId}/images` (multipart, field `file`, policy `Content.Manage`). The response is `{ "url": "..." }`, which the editor puts in an `img` tag with the required description as `alt`.
- Allowed: `.png` `.jpg` `.jpeg` `.webp` `.gif`, and the content type must match (`image/png`, `image/jpeg`, `image/webp`, `image/gif`). SVG is excluded because it can carry script and media is served from the API origin.
- Size cap: `Content:LessonImageMaxSizeInMb` (default 5).
- Key: `lessons/{lessonId}/{random guid}{lower-case extension}`. Files are public-read by an unguessable key.
- Storage: `IFileStorage`, selected by `FileStorage:Provider`. The `Local` provider writes under `FileStorage:LocalRootPath` (relative paths resolve against the API content root) and the API serves it at `FileStorage:PublicBaseUrl` (`/api/media`) with `X-Content-Type-Options: nosniff`. An S3-compatible adapter is pending.
- Every upload is audited as `Lesson.UploadImage` (see `docs/audit-log.md`). Images that are uploaded but never referenced are not cleaned up yet.
- The question editor uploads stem and explanation images through the same endpoint, using the question's lesson. Choice option text uses the compact editor, which has no image tool.

## Changing the format

Adding a tag, attribute, scheme or node type is one change across three places: the `RichTextSanitizer` allow-list, the `RichTextEditor` extensions (and `renderMath` for math nodes), and this document. DOMPurify on the client must allow it too.
