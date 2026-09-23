import { useCallback, useEffect, useRef, useState } from "react";

const ACTIVITY_EVENTS = ["mousedown", "mousemove", "keydown", "scroll", "touchstart"] as const;

/** Mirrors Security:IdleTimeoutMinutes on the Api (appsettings.json,
 * default 15) -- this is a client-side convenience warning only, not the
 * actual enforcement. The real timeout is enforced server-side regardless
 * (SessionValidationMiddleware rejects a stale session's cookie with a
 * coded 401 on its next request), so a user who ignores this warning and
 * closes their laptop is still logged out for real; this just gives an
 * active user a chance to keep working instead of being surprised by a
 * sudden 401 on their next click. */
const IDLE_TIMEOUT_MS = 15 * 60 * 1000;
const WARNING_BEFORE_MS = 60 * 1000;

export function useIdleTimeout(onTimeout: () => void) {
  const [warning, setWarning] = useState(false);
  const warningRef = useRef(false);
  const timeoutRef = useRef<number | undefined>(undefined);
  const warnTimerRef = useRef<number | undefined>(undefined);

  const reset = useCallback(() => {
    warningRef.current = false;
    setWarning(false);
    window.clearTimeout(timeoutRef.current);
    window.clearTimeout(warnTimerRef.current);

    warnTimerRef.current = window.setTimeout(() => {
      warningRef.current = true;
      setWarning(true);
    }, IDLE_TIMEOUT_MS - WARNING_BEFORE_MS);
    timeoutRef.current = window.setTimeout(onTimeout, IDLE_TIMEOUT_MS);
  }, [onTimeout]);

  useEffect(() => {
    reset();

    // Once the warning is showing, ordinary mouse/keyboard activity no
    // longer resets the timer -- the user must explicitly dismiss it via
    // stayActive(). Otherwise just moving the mouse to read the warning
    // would silently cancel it, defeating the point of asking.
    const handleActivity = () => {
      if (!warningRef.current) reset();
    };

    ACTIVITY_EVENTS.forEach((event) => window.addEventListener(event, handleActivity));
    return () => {
      ACTIVITY_EVENTS.forEach((event) => window.removeEventListener(event, handleActivity));
      window.clearTimeout(timeoutRef.current);
      window.clearTimeout(warnTimerRef.current);
    };
  }, [reset]);

  return { warning, stayActive: reset };
}
