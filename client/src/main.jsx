import React, { useEffect, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import './styles.css';

async function api(path, method = 'GET', body) {
  const response = await fetch(`/api/${path}`, {
    method, credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'Guess43' },
    ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
  });
  const data = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) {
    const error = new Error(data?.message || (data?.errors ? Object.values(data.errors).flat().join(' ') : null)
      || (response.status === 429 ? 'Too many attempts. Please wait a minute.' : response.status === 401 ? 'Please sign in to continue.' : 'Something went wrong. Please try again.'));
    error.status = response.status;
    throw error;
  }
  return data;
}

function App() {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [mode, setMode] = useState('login');
  const [game, setGame] = useState({ active: false, attempts: 0 });
  const [history, setHistory] = useState([]);
  const [guess, setGuess] = useState('');
  const [feedback, setFeedback] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [settings, setSettings] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const input = useRef(null);

  useEffect(() => {
    api('auth/me').then(async current => { setUser(current); setGame(await api('game')); })
      .catch(e => { if (e.status !== 401) setError(e.message); }).finally(() => setLoading(false));
  }, []);

  async function perform(action) {
    if (busy) return;
    setBusy(true); setError('');
    try { await action(); } catch (e) { setError(e.message); }
    finally { setBusy(false); }
  }

  function authenticate(event) {
    event.preventDefault();
    const fields = Object.fromEntries(new FormData(event.currentTarget));
    perform(async () => {
      const current = await api(`auth/${mode}`, 'POST', fields);
      setUser(current); setGame(await api('game')); setHistory([]); setFeedback(null);
    });
  }

  function start() {
    perform(async () => {
      setGame(await api('game', 'POST')); setHistory([]); setFeedback(null); setGuess('');
      setTimeout(() => input.current?.focus(), 0);
    });
  }

  function submitGuess(event) {
    event.preventDefault();
    perform(async () => {
      const number = Number(guess);
      const result = await api('game/guess', 'POST', { number });
      setGame({ attempts: result.attempts, active: result.direction !== 'correct' });
      setUser(current => ({ ...current, bestGuesses: result.bestGuesses }));
      setFeedback(result); setHistory(current => [{ number, direction: result.direction, attempt: result.attempts }, ...current]);
      setGuess('');
      setTimeout(() => input.current?.focus(), 0);
    });
  }

  return <div className="app-shell">
    <header><a className="brand" href="/" aria-label="Guess43 home"><span className="brand-icon">#</span>guess<span>43</span><span className="brand-dot">.</span></a>
      {user ? <nav aria-label="Account"><button className="text-button" onClick={() => { setSettings(!settings); setDeleting(false); setError(''); }}>{settings ? 'Back to game' : user.displayName}</button><button className="outline small" disabled={busy} onClick={() => perform(async () => { await api('auth/logout', 'POST'); setUser(null); setSettings(false); setFeedback(null); setHistory([]); setGuess(''); })}>Log out</button></nav>
        : <span className="header-note">SMALL GAME. SHARP MIND.</span>}
    </header>
    {loading ? <main className="loading" aria-live="polite">Getting things ready…</main> : <main>
      <section className="intro"><div className="eyebrow"><span /> THE NUMBER CHALLENGE</div><h1>A little logic.<br />A little <em>intuition.</em></h1><p>One secret number between 1 and 43.<br />How few guesses will you need?</p>
        <div className="number-art" aria-hidden="true"><div className="orbit orbit-one" /><div className="orbit orbit-two" /><span className="floating n-one">07</span><span className="floating n-two">38</span><span className="floating n-three">21</span><div className="mystery">?</div><span className="art-label">43 POSSIBILITIES. ONE ANSWER.</span></div>
        <div className="how"><span className="step">01</span><p>Pick a number.</p><span className="step">02</span><p>Follow the clues.</p><span className="step">03</span><p>Beat your best.</p></div>
      </section>
      <section className="play-column">
        {error && <div role="alert" className="error">{error}</div>}
        {!user ? <div className="card auth-card"><div className="card-kicker">YOUR NEXT PERSONAL BEST STARTS HERE</div><h2>{mode === 'login' ? 'Welcome back.' : 'Let’s play.'}</h2><p className="muted">{mode === 'login' ? 'Sign in to play and pick up your best score.' : 'Create an account to keep your best score.'}</p>
          <div className="tabs"><button className={mode === 'login' ? 'selected' : ''} disabled={busy} onClick={() => { setMode('login'); setError(''); }}>Log in</button><button className={mode === 'register' ? 'selected' : ''} disabled={busy} onClick={() => { setMode('register'); setError(''); }}>Register</button></div>
          <form onSubmit={authenticate} key={mode}><fieldset disabled={busy}>
            {mode === 'register' && <label>Display name<input name="displayName" autoComplete="nickname" placeholder="What should we call you?" required maxLength={64} pattern=".*\S.*" /></label>}
            <label>Email address<input name="email" type="email" autoComplete="email" placeholder="you@example.com" required maxLength={254} /></label>
            <label>Password<input name="password" type="password" autoComplete={mode === 'login' ? 'current-password' : 'new-password'} placeholder={mode === 'register' ? 'At least 8 characters' : 'Enter your password'} required minLength={mode === 'register' ? 8 : 1} maxLength={128} /></label>
            <button className="primary full" type="submit">{busy ? 'One moment…' : mode === 'login' ? 'Log in & play' : 'Create account'} <span>↗</span></button>
          </fieldset></form><div className="card-foot">A quick brain break. A new best to chase.</div></div>
          : settings ? <div className="card"><div className="card-kicker">MAKE YOURSELF AT HOME</div><h2>Your account.</h2><p className="muted">{user.email}</p><form onSubmit={e => { e.preventDefault(); const fields = Object.fromEntries(new FormData(e.currentTarget)); perform(async () => { setUser(await api('auth/me', 'PUT', fields)); setSettings(false); }); }}><fieldset disabled={busy}><label>Display name<input name="displayName" defaultValue={user.displayName} required maxLength={64} pattern=".*\S.*" /></label><button className="primary full">Save changes</button></fieldset></form>
            <div className="danger-zone"><h3>Delete account</h3><p className="muted">Permanently removes your account, game, and personal best.</p>{!deleting ? <button className="danger" onClick={() => setDeleting(true)}>Delete my account</button> : <form onSubmit={e => { e.preventDefault(); const fields = Object.fromEntries(new FormData(e.currentTarget)); perform(async () => { await api('auth/me', 'DELETE', fields); setUser(null); setSettings(false); setDeleting(false); }); }}><fieldset disabled={busy}><label>Confirm your password<input name="password" type="password" autoComplete="current-password" required maxLength={128} /></label><button className="danger">Permanently delete</button><button type="button" className="text-button" onClick={() => setDeleting(false)}>Cancel</button></fieldset></form>}</div></div>
          : <><div className="score-strip"><div><span className="card-kicker">YOUR PERSONAL BEST</span><strong>{user.bestGuesses ?? '—'} <small>{user.bestGuesses === 1 ? 'guess' : 'guesses'}</small></strong></div><span className="score-icon" aria-hidden="true">☆</span></div>
            <div className="card game-card"><div className="game-heading"><span className="card-kicker">{game.active ? 'GAME ON' : feedback?.direction === 'correct' ? 'NICELY DONE' : 'READY WHEN YOU ARE'}</span><span className="range-pill">1 — 43</span></div><h2>{feedback?.direction === 'correct' ? 'You found it!' : game.active ? 'Find the number.' : 'Trust your hunch.'}</h2>
              <p className="muted">{game.active ? 'Make a guess. We’ll point you in the right direction.' : feedback?.direction === 'correct' ? `Solved in ${game.attempts} ${game.attempts === 1 ? 'guess' : 'guesses'}. ${feedback.newBest ? 'A new personal best!' : 'Ready for another round?'}` : 'Your secret number is one click away.'}</p>
              {game.active ? <form onSubmit={submitGuess}><label htmlFor="guess">Your guess</label><div className="guess-entry"><input ref={input} id="guess" className="guess-input" type="number" min="1" max="43" step="1" placeholder="?" value={guess} onChange={e => setGuess(e.target.value)} required disabled={busy} /><button className="primary" disabled={busy}>{busy ? '…' : 'Guess →'}</button></div></form> : <button className="primary full" disabled={busy} onClick={start}>{busy ? 'Starting…' : feedback ? 'Play again ↗' : 'Start a game ↗'}</button>}
              <div className={`feedback ${feedback?.direction === 'correct' ? 'won' : ''}`} role="status" aria-live="polite">{feedback ? feedback.direction === 'correct' ? '✓ That’s the number. Well played.' : feedback.direction === 'higher' ? '↑ Go higher. You’ve got this.' : '↓ Go lower. Keep narrowing it down.' : 'Every guess gets you a little closer.'}</div>
              <div className="game-bottom"><span><strong>{game.attempts}</strong> guesses this round</span>{game.active && <button className="text-button" disabled={busy} onClick={start}>New round ↻</button>}</div>
            </div>
            {history.length > 0 && <div className="history"><h3>This round <span>LATEST FIRST</span></h3><div className="history-items">{history.map(item => <div className={`history-chip ${item.direction}`} key={item.attempt}><strong>{item.number}</strong><span>{item.direction === 'higher' ? '↑ Higher' : item.direction === 'lower' ? '↓ Lower' : '✓ Correct'}</span></div>)}</div></div>}
            <p className="save-note">Your personal best is saved automatically.</p></>}
      </section>
    </main>}
    <footer><span>A small challenge for a curious mind.</span><span>BUILT TO KEEP YOU GUESSING ↗</span></footer>
  </div>;
}

createRoot(document.getElementById('root')).render(<App />);
