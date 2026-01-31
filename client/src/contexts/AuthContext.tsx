import { createContext, useState, useCallback, useEffect, type ReactNode } from 'react';
import { api } from '@/lib/api';

type AuthState = 'loading' | 'authenticated' | 'unauthenticated';

export interface AuthContextType {
  state: AuthState;
  error: string | null;
  login: (password: string) => Promise<boolean>;
  logout: () => Promise<void>;
}

export const AuthContext = createContext<AuthContextType | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>('loading');
  const [error, setError] = useState<string | null>(null);

  // Check auth on mount
  useEffect(() => {
    api
      .checkAuth()
      .then(() => setState('authenticated'))
      .catch(() => setState('unauthenticated'));
  }, []);

  // Listen for 401 errors from any API call
  useEffect(() => {
    const handleUnauthorized = () => setState('unauthenticated');
    window.addEventListener('selfmx:unauthorized', handleUnauthorized);
    return () => window.removeEventListener('selfmx:unauthorized', handleUnauthorized);
  }, []);

  const login = useCallback(async (password: string): Promise<boolean> => {
    setError(null);
    try {
      await api.login(password);
      setState('authenticated');
      return true;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Login failed';
      setError(message);
      return false;
    }
  }, []);

  const logout = useCallback(async () => {
    await api.logout();
    setState('unauthenticated');
    setError(null);
  }, []);

  return (
    <AuthContext.Provider value={{ state, error, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}
