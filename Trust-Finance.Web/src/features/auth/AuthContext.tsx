import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { clearToken, getToken, setToken } from "../../lib/http";
import { userFromToken } from "../../lib/jwt";
import type { AuthUser } from "../../types/api";

interface AuthContextValue {
  user: AuthUser | null;
  signIn: (token: string) => void;
  signOut: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

function readInitialUser(): AuthUser | null {
  const token = getToken();
  if (!token) return null;

  const user = userFromToken(token);
  if (!user) clearToken(); // expired or malformed
  return user;
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(readInitialUser);

  const signIn = useCallback((token: string) => {
    setToken(token);
    setUser(userFromToken(token));
  }, []);

  const signOut = useCallback(() => {
    clearToken();
    setUser(null);
  }, []);

  const value = useMemo(
    () => ({ user, signIn, signOut }),
    [user, signIn, signOut],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) throw new Error("useAuth must be used within AuthProvider");
  return context;
}
