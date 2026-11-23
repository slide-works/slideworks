import type { ProfileAgent } from '../../api/profile';

// The Connected agents panel. TODO: add a Disconnect button per row with its Keep/Disconnect confirmation
// (DELETE /api/me/agents/{id}); for now each row shows what is stored.
export function AgentsPanel({ agents }: { agents: ProfileAgent[] }) {
  return (
    <section
      data-testid="profile-agents"
      aria-labelledby="profile-agents-title"
      className="panel grid gap-3 p-4"
    >
      <h3 id="profile-agents-title" className="text-sm font-bold text-ink">
        Connected agents
      </h3>
      {agents.length === 0 ? (
        <p data-testid="profile-agents-empty" className="text-meta text-ink-muted">
          No agents are connected.
        </p>
      ) : (
        <ul className="grid gap-2">
          {agents.map((agent) => (
            <li
              key={agent.id}
              data-testid="profile-agent"
              data-agent-id={agent.id}
              className="grid text-meta"
            >
              <span data-testid="profile-agent-name" className="font-semibold text-ink">
                {agent.clientName}
              </span>
              <span data-testid="profile-agent-connected" className="text-ink-muted">
                Connected: {agent.connectedLabel}
              </span>
              <span data-testid="profile-agent-last-used" className="text-ink-muted">
                Last used: {agent.lastUsedLabel ?? 'Never'}
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
