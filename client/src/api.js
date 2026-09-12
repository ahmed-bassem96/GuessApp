// Cookies stay in the browser. No passwords or tokens are stored in localStorage.
export async function api(path, method = 'GET', body) {
  const headers = { 'Content-Type': 'application/json' };

  if (method !== 'GET') {
    // Get a fresh token for the current identity, including after login/logout.
    const csrf = await api('/auth/csrf');
    headers['X-CSRF-TOKEN'] = csrf.token;
  }

  let response;
  try {
    response = await fetch(`/api${path}`, {
      method,
      credentials: 'include',
      headers,
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    });
  } catch {
    throw new Error('Cannot reach the server. Check that the backend is running.');
  }

  const data = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) {
    const message = data?.message || (data?.errors && Object.values(data.errors).flat().join(' '))
      || (response.status === 401 ? 'Please log in to continue.' : 'Request failed. Check that the backend and database are running.');
    const error = new Error(message);
    error.status = response.status;
    throw error;
  }
  return data;
}
