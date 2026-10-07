/** Why the session just ended, for the login page's confirmation notice.
 * In memory only: the notice belongs to the logout that just happened, not
 * to a later visit or a page reload. */
export type LogoutReason = "user" | "idle";
export type LogoutNotice = { at: Date; reason: LogoutReason };

let lastLogout: LogoutNotice | null = null;

export function recordLogout(reason: LogoutReason): void {
  lastLogout = { at: new Date(), reason };
}

/** Read without clearing -- safe inside a state initializer, which React's
 * StrictMode may call twice. Pair with clearLastLogout() in an effect. */
export function peekLastLogout(): LogoutNotice | null {
  return lastLogout;
}

export function clearLastLogout(): void {
  lastLogout = null;
}
