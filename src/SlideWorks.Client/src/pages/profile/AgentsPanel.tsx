import { useRef, useState } from 'react';
import { ApiError, api } from '../../api/client';
import type { ProfileAgent } from '../../api/profile';

// The Connected agents panel (D18, D27): one row per live agent. Disconnect asks first, inline in the row; Disconnect
// there sends DELETE /api/me/agents/{id}, which revokes the whole token family. Focus goes to the row's Disconnect
// button after Keep or a failure, and to the panel heading when the row is gone.
export function AgentsPanel({ agents: initial }: { agents: ProfileAgent[] }) {
  const [agents, setAgents] = useState(initial);
  const [asking, setAsking] = useState<string | null>(null);
  const [sending, setSending] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const headingRef = useRef<HTMLHeadingElement>(null);
  const disconnectRefs = useRef(new Map<string, HTMLButtonElement>());

  function ask(agent: ProfileAgent) {
    setAsking(agent.id);
    setNotice(null);
    setError(null);
  }

  function keep(agent: ProfileAgent) {
    setAsking(null);
    // The Disconnect button is not in the page until the question closes.
    requestAnimationFrame(() => disconnectRefs.current.get(agent.id)?.focus());
  }

  async function disconnect(agent: ProfileAgent) {
    setSending(true);
    try {
      await api.del(`/api/me/agents/${agent.id}`);
      setAgents((current) => current.filter((a) => a.id !== agent.id));
      setAsking(null);
      setNotice(`${agent.clientName} disconnected.`);
      requestAnimationFrame(() => headingRef.current?.focus());
    } catch (failure) {
      setAsking(null);
      setError(
        failure instanceof ApiError
          ? `Could not disconnect ${agent.clientName}: ${failure.message}`
          : "Can't reach SlideWorks",
      );
      requestAnimationFrame(() => disconnectRefs.current.get(agent.id)?.focus());
    } finally {
      setSending(false);
    }
  }

  return (
    <section
      data-testid="profile-agents"
      aria-labelledby="profile-agents-title"
      className="panel grid gap-3 p-4"
    >
      <h3
        id="profile-agents-title"
        ref={headingRef}
        tabIndex={-1}
        className="text-sm font-bold text-ink"
      >
        Connected agents
      </h3>
      {notice !== null && (
        <p role="status" data-testid="profile-agents-notice" className="text-meta text-ink-muted">
          {notice}
        </p>
      )}
      {error !== null && (
        <p role="alert" data-testid="profile-agents-error" className="text-meta text-bad">
          {error}
        </p>
      )}
      {agents.length === 0 ? (
        <p data-testid="profile-agents-empty" className="text-meta text-ink-muted">
          No agents are connected.
        </p>
      ) : (
        <ul className="grid gap-3">
          {agents.map((agent) => (
            <li
              key={agent.id}
              data-testid="profile-agent"
              data-agent-id={agent.id}
              className="grid gap-2 text-meta"
            >
              <div className="grid">
                <span data-testid="profile-agent-name" className="font-semibold text-ink">
                  {agent.clientName}
                </span>
                <span data-testid="profile-agent-connected" className="text-ink-muted">
                  Connected: {agent.connectedLabel}
                </span>
                <span data-testid="profile-agent-last-used" className="text-ink-muted">
                  Last used: {agent.lastUsedLabel ?? 'Never'}
                </span>
              </div>
              {asking === agent.id ? (
                <div data-testid="profile-agent-confirm" className="grid gap-2">
                  <p className="text-ink">
                    Disconnect {agent.clientName}? It stops acting for you straight away. Its next
                    call is refused until you connect it again.
                  </p>
                  <div className="flex gap-2">
                    <button
                      type="button"
                      data-testid="profile-agent-keep"
                      aria-label={`Keep ${agent.clientName}`}
                      onClick={() => keep(agent)}
                      className="btn-ghost"
                    >
                      Keep
                    </button>
                    <button
                      type="button"
                      data-testid="profile-agent-confirm-disconnect"
                      aria-label={`Disconnect ${agent.clientName}`}
                      disabled={sending}
                      onClick={() => void disconnect(agent)}
                      className="btn-primary"
                    >
                      Disconnect
                    </button>
                  </div>
                </div>
              ) : (
                <div>
                  <button
                    type="button"
                    data-testid="profile-agent-disconnect"
                    aria-label={`Disconnect ${agent.clientName}`}
                    ref={(node) => {
                      if (node === null) disconnectRefs.current.delete(agent.id);
                      else disconnectRefs.current.set(agent.id, node);
                    }}
                    onClick={() => ask(agent)}
                    className="btn-ghost"
                  >
                    Disconnect
                  </button>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
