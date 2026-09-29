import { reportClientError } from '@/shared/api/generated/client-errors/client-errors';
import type { ClientErrorSource } from '@/shared/api/generated/model';
import { ApiError } from '@/shared/lib/apiError';

// mirrors the API ClientErrors defaults (appsettings.example.json)
export const clientErrorLimits = { message: 500, errorName: 100, stack: 4000, path: 300 } as const;

export const maxReportsPerPage = 10;

const unknownErrorMessage = 'Unknown error';

export interface ClientErrorReporter {
  report(error: unknown, source: ClientErrorSource): void;
  install(target: Window): () => void;
  reset(): void;
}

function isIgnored(error: unknown): boolean {
  const isAbort = (error instanceof Error || error instanceof DOMException) && error.name === 'AbortError';
  return error instanceof ApiError || isAbort;
}

function messageOf(error: unknown): string {
  const text = error instanceof Error ? error.message : String(error);
  return (text || (error instanceof Error ? error.name : '') || unknownErrorMessage).slice(
    0,
    clientErrorLimits.message,
  );
}

export function createClientErrorReporter(): ClientErrorReporter {
  let sent = 0;
  const seen = new Set<string>();

  const report = (error: unknown, source: ClientErrorSource) => {
    if (isIgnored(error) || sent >= maxReportsPerPage) {
      return;
    }
    const message = messageOf(error);
    const key = `${source}|${message}`;
    if (seen.has(key)) {
      return;
    }
    seen.add(key);
    sent++;
    const isError = error instanceof Error;
    // error reporting must never throw or surface a second failure
    void reportClientError({
      message,
      errorName: isError ? error.name.slice(0, clientErrorLimits.errorName) : null,
      stack: isError && error.stack ? error.stack.slice(0, clientErrorLimits.stack) : null,
      source,
      path: window.location.pathname.slice(0, clientErrorLimits.path),
    }).catch(() => undefined);
  };

  return {
    report,
    install(target) {
      const onError = (event: ErrorEvent) => {
        report(event.error ?? event.message, 'Window');
      };
      const onRejection = (event: PromiseRejectionEvent) => {
        report(event.reason, 'UnhandledRejection');
      };
      target.addEventListener('error', onError);
      target.addEventListener('unhandledrejection', onRejection);
      return () => {
        target.removeEventListener('error', onError);
        target.removeEventListener('unhandledrejection', onRejection);
      };
    },
    reset() {
      sent = 0;
      seen.clear();
    },
  };
}

export const clientErrorReporter = createClientErrorReporter();
