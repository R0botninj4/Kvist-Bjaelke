import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from "react";

export type Role = "User" | "SuperUser" | "Admin";
export type User = { id: number; username: string; role: Role; createdAt: string };
type Auth = {
  user: User | null; loading: boolean;
  login: (username: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  api: <T>(path: string, init?: RequestInit) => Promise<T>;
};
const Context = createContext<Auth | null>(null);

async function readError(response: Response): Promise<string> {
  if (response.status === 401) return "Din session er udløbet. Log ind igen.";
  if (response.status === 403) return "Din rolle har ikke adgang til denne handling.";
  if (response.status === 429) return "For mange forespørgsler. Vent lidt og prøv igen.";
  try {
    const data = await response.json();
    return data.message || (data.errors ? Object.values(data.errors).flat().join(" ") : data.title) || "Handlingen kunne ikke gennemføres.";
  } catch { return "Handlingen kunne ikke gennemføres."; }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(() => sessionStorage.getItem("token"));
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);

  const clear = useCallback(() => {
    sessionStorage.removeItem("token"); setToken(null); setUser(null);
  }, []);

  const api = useCallback(async <T,>(path: string, init: RequestInit = {}): Promise<T> => {
    let response: Response;
    try {
      response = await fetch(path, {
        ...init,
        headers: { "Content-Type": "application/json", ...init.headers, ...(token ? { Authorization: `Bearer ${token}` } : {}) }
      });
    } catch { throw new Error("Serveren kan ikke kontaktes. Kontrollér forbindelsen og prøv igen."); }
    if (!response.ok) {
      if (response.status === 401) clear();
      throw new Error(await readError(response));
    }
    return response.status === 204 ? undefined as T : response.json();
  }, [token, clear]);

  useEffect(() => {
    let active = true;
    if (!token) { setLoading(false); return; }
    setLoading(true);
    api<User>("/api/auth/me").then(u => { if (active) setUser(u); })
      .catch(() => { if (active) clear(); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [token, api, clear]);

  async function login(username: string, password: string) {
    let response: Response;
    try {
      response = await fetch("/api/auth/login", {
        method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ username, password })
      });
    } catch { throw new Error("Serveren kan ikke kontaktes. Prøv igen om lidt."); }
    if (!response.ok) throw new Error(response.status === 401 ? "Forkert brugernavn eller adgangskode." : await readError(response));
    const data: { token: string; user: User } = await response.json();
    sessionStorage.setItem("token", data.token); setToken(data.token); setUser(data.user);
  }

  async function logout() {
    // Keep the session visible if revocation fails, so logout can be retried.
    await api<void>("/api/auth/logout", { method: "POST" });
    clear();
  }

  return <Context.Provider value={{ user, loading, login, logout, api }}>{children}</Context.Provider>;
}

export function useAuth() {
  const value = useContext(Context);
  if (!value) throw new Error("AuthProvider mangler.");
  return value;
}
