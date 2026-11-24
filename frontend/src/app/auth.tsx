import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { getToken, onUnauthorized, setToken } from '@/api/client';
import { authApi } from '@/api/endpoints';
import { RoleType, type AuthResponse, type RoleName, type TenantResponse, type UserResponse } from '@/api/types';

interface Session {
  user: UserResponse;
  tenant: TenantResponse;
}

interface AuthContextValue {
  session: Session | null;
  /** True while a stored token is being validated against /api/auth/me. */
  restoring: boolean;
  role: RoleName | null;
  isOwner: boolean;
  signIn: (auth: AuthResponse) => void;
  signOut: () => void;
  updateSession: (patch: Partial<Session>) => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

function tokenExpired(token: string): boolean {
  try {
    const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
    return typeof payload.exp === 'number' && payload.exp * 1000 < Date.now();
  } catch {
    return true;
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [session, setSession] = useState<Session | null>(null);
  const [restoring, setRestoring] = useState(() => {
    const token = getToken();
    return !!token && !tokenExpired(token);
  });

  const signOut = useCallback(() => {
    setToken(null);
    setSession(null);
    queryClient.clear();
  }, [queryClient]);

  useEffect(() => {
    onUnauthorized(signOut);
    return () => onUnauthorized(null);
  }, [signOut]);

  useEffect(() => {
    if (!restoring) {
      if (getToken()) setToken(null);
      return;
    }
    authApi
      .me()
      .then((me) => setSession({ user: me.user, tenant: me.tenant }))
      .catch(() => setToken(null))
      .finally(() => setRestoring(false));
    // Runs once on mount to restore a stored session.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const signIn = useCallback(
    (auth: AuthResponse) => {
      queryClient.clear();
      setToken(auth.accessToken);
      setSession({ user: auth.user, tenant: auth.tenant });
    },
    [queryClient],
  );

  const updateSession = useCallback(
    (patch: Partial<Session>) => setSession((current) => (current ? { ...current, ...patch } : current)),
    [],
  );

  const value = useMemo<AuthContextValue>(() => {
    const role: RoleName | null = session
      ? session.user.role === RoleType.Owner
        ? 'Owner'
        : 'Employee'
      : null;
    return { session, restoring, role, isOwner: role === 'Owner', signIn, signOut, updateSession };
  }, [session, restoring, signIn, signOut, updateSession]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used inside AuthProvider');
  return context;
}
