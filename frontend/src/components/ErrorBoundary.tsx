import { Component, type ReactNode } from "react";

interface Props {
  children: ReactNode;
}

interface State {
  error: Error | null;
}

/** React error boundaries must be class components -- there's no hook
 * equivalent for getDerivedStateFromError/componentDidCatch. Catches a
 * render-time crash anywhere below it and shows a recoverable screen
 * instead of a blank white page. */
export class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: { componentStack: string }) {
    // eslint-disable-next-line no-console
    console.error("Unhandled error in the app tree:", error, info.componentStack);
  }

  render() {
    if (this.state.error) {
      return (
        <div className="flex min-h-screen items-center justify-center bg-surface-muted px-4">
          <div className="card max-w-sm text-center">
            <h1 className="text-lg font-semibold text-text">Something went wrong</h1>
            <p className="mt-2 text-sm text-text-muted">
              An unexpected error occurred. Try reloading the page.
            </p>
            <button type="button" className="btn-primary mt-4" onClick={() => window.location.reload()}>
              Reload
            </button>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
