import React, { useEffect, useState } from 'react';
import { api } from './api';

export default function App() {
  const [user, setUser] = useState(null);
  const [game, setGame] = useState({ isActive: false, guessCount: 0 });
  const [registering, setRegistering] = useState(false);
  const [guess, setGuess] = useState('');
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);

  async function refresh() {
    const currentUser = await api('/auth/me');
    const currentGame = await api('/games/current');
    setUser(currentUser);
    setGame(currentGame);
  }

  useEffect(() => {
    refresh().catch(error => {
      if (error.status !== 401) setError(error.message);
    }).finally(() => setLoading(false));
  }, []);

  async function run(action) {
    if (busy) return;
    setBusy(true);
    setError('');
    try {
      await action();
    } catch (error) {
      if (error.status === 401) setUser(null);
      if (error.status === 409 && user) {
        setMessage('');
        await refresh().catch(() => {});
      }
      setError(error.message);
    } finally {
      setBusy(false);
    }
  }

  function authenticate(event) {
    event.preventDefault();
    const body = Object.fromEntries(new FormData(event.currentTarget));
    run(async () => {
      const currentUser = await api(registering ? '/auth/register' : '/auth/login', 'POST', body);
      setUser(currentUser);
      setGame(await api('/games/current'));
      setMessage('');
      setGuess('');
    });
  }

  function startGame() {
    run(async () => {
      setGame(await api('/games', 'POST'));
      setGuess('');
      setMessage('Game started. Enter a number from 1 to 43.');
    });
  }

  function submitGuess(event) {
    event.preventDefault();
    run(async () => {
      const result = await api('/games/current/guesses', 'POST', { number: Number(guess) });
      setGame({ isActive: result.message !== 'correct', guessCount: result.guessCount });
      setUser(current => ({ ...current, bestGuesses: result.bestGuesses }));
      setMessage(result.message === 'correct'
        ? `Correct! You won in ${result.guessCount} ${result.guessCount === 1 ? 'guess' : 'guesses'}.`
        : result.message === 'guess higher' ? 'Guess higher ↑' : 'Guess lower ↓');
      setGuess('');
    });
  }

  return <main>
    <header>
      <span className="badge">1–43</span>
      <h1>Guess the Number</h1>
      <p>Find the secret number in as few guesses as possible.</p>
    </header>

    {error && <p className="error" role="alert">{error}</p>}

    {loading ? <p role="status">Loading…</p> : !user ? <section className="card">
      <h2>{registering ? 'Create an account' : 'Log in'}</h2>
      <form onSubmit={authenticate} key={registering ? 'register' : 'login'}>
        <fieldset disabled={busy}>
          <label htmlFor="email">Email</label>
          <input id="email" name="email" type="email" autoComplete="email" maxLength={254} required />
          <label htmlFor="password">Password</label>
          <input id="password" name="password" type="password"
            autoComplete={registering ? 'new-password' : 'current-password'}
            minLength={registering ? 8 : 1} maxLength={128} required />
          {registering && <p className="hint">Use at least 8 characters.</p>}
          <button type="submit">{busy ? 'Please wait…' : registering ? 'Register' : 'Log in'}</button>
        </fieldset>
      </form>
      <button className="secondary" disabled={busy} onClick={() => { setRegistering(!registering); setError(''); }}>
        {registering ? 'Already have an account? Log in' : 'New here? Register'}
      </button>
    </section> : <section className="card">
      <div className="account">
        <span>{user.email}</span>
        <button className="link" disabled={busy} onClick={() => run(async () => {
          await api('/auth/logout', 'POST');
          setUser(null);
          setMessage('');
          setRegistering(false);
        })}>Log out</button>
      </div>

      <div className="scores">
        <div><span>Personal best</span><strong>{user.bestGuesses ?? '—'}</strong><small>{user.bestGuesses === null ? 'No wins yet' : 'guesses'}</small></div>
        <div><span>Current round</span><strong>{game.guessCount}</strong><small>guesses</small></div>
      </div>

      {game.isActive ? <form onSubmit={submitGuess}>
        <fieldset disabled={busy}>
          <label htmlFor="guess">Your guess (1–43)</label>
          <input id="guess" type="number" min="1" max="43" step="1" required
            value={guess} onChange={event => setGuess(event.target.value)} />
          <button type="submit">{busy ? 'Checking…' : 'Submit guess'}</button>
        </fieldset>
      </form> : <button disabled={busy} onClick={startGame}>{busy ? 'Starting…' : 'Start a new game'}</button>}

      <p className="message" role="status">{message || (game.isActive ? 'A game is in progress. Make your next guess.' : 'Start a game when you’re ready.')}</p>
      {game.isActive && <button className="secondary" disabled={busy} onClick={startGame}>Restart this round</button>}
    </section>}
  </main>;
}
