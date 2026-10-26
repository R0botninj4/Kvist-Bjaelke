import { useEffect, useState, type FormEvent } from "react";

type Role = "User" | "SuperUser" | "Admin";
type User = { id: number; username: string; role: Role };

export function App() {
    const [token, setToken] = useState<string | null>(sessionStorage.getItem("token"));
    const [user, setUser] = useState<User | null>(null);
    const [users, setUsers] = useState<User[]>([]);
    const [username, setUsername] = useState("");
    const [password, setPassword] = useState("");
    const [role, setRole] = useState<Role>("User");
    const [editId, setEditId] = useState<number | null>(null);
    const [error, setError] = useState("");
    const [message, setMessage] = useState("");
    const [busy, setBusy] = useState(false);
    const [loading, setLoading] = useState(!!token);

    function clearForm() {
        setUsername(""); setPassword(""); setRole("User"); setEditId(null);
    }

    function logout() {
        sessionStorage.removeItem("token"); setToken(null); setUser(null);
        setUsers([]); setMessage(""); clearForm();
    }

    async function request<T>(path: string, method = "GET", body?: unknown): Promise<T> {
        const response = await fetch(path, {
            method,
            headers: { "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}) },
            body: body === undefined ? undefined : JSON.stringify(body)
        });
        if (response.status === 401) {
            logout();
            throw new Error(path === "/login" ? "Forkert brugernavn eller adgangskode." : "Log ind igen.");
        }
        if (response.status === 403) throw new Error("Du har ikke adgang til denne handling.");
        if (!response.ok) {
            const text = await response.text();
            let message = text;
            try {
                const data = JSON.parse(text);
                message = data.errors ? Object.values(data.errors).flat().join(" ") : data.title || text;
            } catch { /* API'et kan også returnere en almindelig tekstbesked. */ }
            throw new Error(message || "Handlingen mislykkedes.");
        }
        return response.status === 204 ? undefined as T : response.json();
    }

    function showError(e: unknown) {
        setError(e instanceof Error ? e.message : "Serveren kan ikke kontaktes.");
    }

    async function getUsers() { setUsers(await request<User[]>("/api/users")); }

    useEffect(() => {
        if (!token) return;
        setLoading(true);
        Promise.all([request<User>("/api/auth/me"), request<User[]>("/api/users")])
            .then(([me, list]) => { setUser(me); setUsers(list); })
            .catch(showError).finally(() => setLoading(false));
    }, [token]);

    async function login(e: FormEvent) {
        e.preventDefault(); setBusy(true); setError("");
        try {
            const data = await request<{ token: string; user: User }>("/login", "POST", { username, password });
            sessionStorage.setItem("token", data.token); setToken(data.token); setUser(data.user); clearForm();
        } catch (e) { showError(e); }
        finally { setBusy(false); }
    }

    async function saveUser(e: FormEvent) {
        e.preventDefault(); setBusy(true); setError(""); setMessage("");
        try {
            await request(editId === null ? "/api/users" : `/api/users/${editId}`,
                editId === null ? "POST" : "PUT", { username, password: password || null, role });
            clearForm(); await getUsers(); setMessage("Brugeren er gemt.");
        } catch (e) { showError(e); }
        finally { setBusy(false); }
    }

    function editUser(target: User) {
        setUsername(target.username); setRole(target.role); setPassword(""); setEditId(target.id); setMessage("");
    }

    async function deleteUser(target: User) {
        if (!confirm(`Vil du slette ${target.username}?`)) return;
        setBusy(true); setError(""); setMessage("");
        try {
            await request(`/api/users/${target.id}`, "DELETE");
            if (editId === target.id) clearForm();
            await getUsers(); setMessage("Brugeren er slettet.");
        } catch (e) { showError(e); }
        finally { setBusy(false); }
    }

    if (loading) return <p>Henter brugere…</p>;
    return <main>
        <h1>Kvist & Bjælke</h1>
        {error && <p role="alert">{error}</p>}
        {message && <p role="status">{message}</p>}
        {!user ? <form onSubmit={login}>
            <h2>Log ind</h2><fieldset disabled={busy}>
                <p><label htmlFor="username">Brugernavn</label><br /><input id="username" value={username} onChange={e => setUsername(e.target.value)} required maxLength={50} autoComplete="username" /></p>
                <p><label htmlFor="password">Adgangskode</label><br /><input id="password" type="password" value={password} onChange={e => setPassword(e.target.value)} required maxLength={128} autoComplete="current-password" /></p>
                <button>{busy ? "Logger ind…" : "Log ind"}</button>
            </fieldset>
        </form> : <>
            <p>{user.username} · {user.role} <button onClick={logout} disabled={busy}>Log ud</button></p>
            <h2>Brugere</h2>
            {user.role !== "User" && <form onSubmit={saveUser}>
                <h3>{editId === null ? "Opret bruger" : "Rediger bruger"}</h3><fieldset disabled={busy}>
                    <p><label htmlFor="edit-username">Brugernavn</label><br /><input id="edit-username" value={username} onChange={e => setUsername(e.target.value)} required pattern="[a-zA-Z0-9._-]{3,50}" /></p>
                    <p><label htmlFor="edit-password">{editId === null ? "Adgangskode" : "Ny adgangskode (valgfri)"}</label><br /><input id="edit-password" type="password" value={password} onChange={e => setPassword(e.target.value)} required={editId === null} minLength={12} maxLength={128} autoComplete="new-password" /></p>
                    <p><label htmlFor="role">Rolle</label><br /><select id="role" value={role} onChange={e => setRole(e.target.value as Role)} disabled={user.role !== "Admin"}>
                        <option>User</option>{user.role === "Admin" && <><option>SuperUser</option><option>Admin</option></>}
                    </select></p>
                    <button>{busy ? "Gemmer…" : "Gem"}</button> <button type="button" onClick={clearForm}>Annuller</button>
                </fieldset>
            </form>}
            <table><thead><tr><th>Brugernavn</th><th>Rolle</th><th>Handlinger</th></tr></thead><tbody>
                {users.map(target => <tr key={target.id}><td>{target.username}</td><td>{target.role}</td><td>
                    {user.role !== "User" ? <><button onClick={() => editUser(target)} disabled={busy}>Rediger</button> {target.id !== user.id && <button onClick={() => deleteUser(target)} disabled={busy}>Slet</button>}</> : "Kun læsning"}
                </td></tr>)}
                {users.length === 0 && <tr><td colSpan={3}>Ingen brugere endnu.</td></tr>}
            </tbody></table>
        </>}
    </main>;
}
