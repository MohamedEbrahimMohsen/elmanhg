export interface AuthHandlers {
  refresh: () => Promise<boolean>;
  onExpired: () => void;
}

let accessToken: string | null = null;
let handlers: AuthHandlers | null = null;
let inFlight: Promise<boolean> | null = null;

export function getAccessToken(): string | null {
  return accessToken;
}

export function setAccessToken(token: string | null): void {
  accessToken = token;
}

export function registerAuthHandlers(next: AuthHandlers | null): void {
  handlers = next;
}

export function refreshOnce(): Promise<boolean> {
  if (handlers === null) {
    return Promise.resolve(false);
  }
  inFlight ??= handlers.refresh().finally(() => {
    inFlight = null;
  });
  return inFlight;
}

export function notifyExpired(): void {
  handlers?.onExpired();
}
