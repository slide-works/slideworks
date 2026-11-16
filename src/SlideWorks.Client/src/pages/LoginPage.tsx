import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { setToken } from '../api/client';

// Paste the SLIDEWORKS_API_TOKEN; every API call carries it as a bearer token
// from localStorage.
export function LoginPage() {
  const [value, setValue] = useState('');
  const navigate = useNavigate();

  const submit = (event: React.FormEvent) => {
    event.preventDefault();
    if (!value.trim()) return;
    setToken(value.trim());
    navigate('/');
  };

  return (
    <main className="grid min-h-dvh place-items-center px-4 py-10">
      <form className="panel flex w-full max-w-sm flex-col gap-3 p-6 sm:p-8" onSubmit={submit}>
        <h1 className="mb-2 text-2xl font-bold tracking-tight text-ink">
          SlideWorks<span className="text-signal">.</span>
        </h1>
        <label className="text-meta text-ink-muted" htmlFor="token">
          API token
        </label>
        <input
          className="field"
          id="token"
          data-testid="token-input"
          type="password"
          value={value}
          onChange={(e) => setValue(e.target.value)}
          autoFocus
        />
        <button className="btn-signal" type="submit" data-testid="token-submit">
          Enter
        </button>
      </form>
    </main>
  );
}
