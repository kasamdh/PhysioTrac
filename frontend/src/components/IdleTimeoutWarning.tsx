interface IdleTimeoutWarningProps {
  onStayActive: () => void;
  onSignOut: () => void;
}

export function IdleTimeoutWarning({ onStayActive, onSignOut }: IdleTimeoutWarningProps) {
  return (
    <div className="modal-overlay" role="alertdialog" aria-modal="true" aria-labelledby="idle-timeout-title">
      <div className="modal-panel">
        <h2 id="idle-timeout-title" className="text-base font-semibold text-text">
          Are you still there?
        </h2>
        <p className="mt-2 text-sm text-text-muted">
          You've been inactive for a while. For your security, you'll be signed out in about a minute unless you
          choose to stay signed in.
        </p>
        <div className="mt-5 flex justify-end gap-2">
          <button type="button" className="btn-secondary" onClick={onSignOut}>
            Sign out now
          </button>
          <button type="button" className="btn-primary" onClick={onStayActive}>
            Stay signed in
          </button>
        </div>
      </div>
    </div>
  );
}
