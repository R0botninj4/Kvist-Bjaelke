import { useEffect, useState, type FormEvent } from "react";
import { useAuth } from "./auth/AuthProvider";

type User = { id: number; username: string; role: string };

export function App() {
    const { token, login, logout } = useAuth();
    const [users, setUsers] = useState<User[]>([]);
    const [currentUser, setCurrentUser] = useState<User | null>(null);
    const [username, setUsername] = useState("");
    const [password, setPassword] = useState("");
    const [role, setRole] = useState("User");
    const [editId, setEditId] = useState<number | null>(null);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState("");

    function clearForm() {
        setUsername("");
        setPassword("");
        setRole("User");
        setEditId(null);
    }

    async function getUsers() {
        const response = await fetch("/api/Users", {
            headers: { Authorization: `Bearer ${token}` }
        });
        if (response.status === 401) { logout(); return; }
        if (!response.ok) throw new Error("Brugerne kunne ikke hentes.");
        setUsers(await response.json());
    }

    useEffect(() => {
        if (!token) {
            setCurrentUser(null);
            setUsers([]);
            return;
        }
        async function loadUser() {
            try {
                const response = await fetch("/api/auth/me", {
                    headers: { Authorization: `Bearer ${token}` }
                });
                if (response.status === 401) { logout(); return; }
                if (!response.ok) throw new Error("Brugeren kunne ikke hentes.");
                setCurrentUser(await response.json());
                await getUsers();
            } catch (error) {
                setError(error instanceof Error ? error.message : "Serveren kan ikke kontaktes.");
            }
        }
        loadUser();
    }, [token]);

    async function handleSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        setError("");
        setIsLoading(true);
        try {
            await login({ username, password });
            clearForm();
        } catch (error) {
            setError(error instanceof Error ? error.message : "Login failed.");
        } finally {
            setIsLoading(false);
        }
    }

    async function saveUser(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        setError("");
        setIsLoading(true);
        let url = "/api/Users";
        let method = "POST";
        if (editId !== null) {
            url += `/${editId}`;
            method = "PUT";
        }
        try {
            const response = await fetch(url, {
                method,
                headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}` },
                body: JSON.stringify({ username, password: password || null, role })
            });
            if (response.status === 401) { logout(); return; }
            if (response.status === 409) throw new Error("Brugernavnet er allerede i brug.");
            if (!response.ok) throw new Error("Brugeren kunne ikke gemmes. Kontrollér oplysninger og rolle.");
            clearForm();
            await getUsers();
        } catch (error) {
            setError(error instanceof Error ? error.message : "Serveren kan ikke kontaktes.");
        } finally {
            setIsLoading(false);
        }
    }

    function editUser(user: User) {
        setUsername(user.username);
        setPassword("");
        setRole(user.role);
        setEditId(user.id);
    }

    async function deleteUser(user: User) {
        if (!confirm(`Vil du slette ${user.username}?`)) return;
        setError("");
        setIsLoading(true);
        try {
            const response = await fetch(`/api/Users/${user.id}`, {
                method: "DELETE",
                headers: { Authorization: `Bearer ${token}` }
            });
            if (response.status === 401) { logout(); return; }
            if (!response.ok) throw new Error("Brugeren kunne ikke slettes.");
            if (editId === user.id) clearForm();
            await getUsers();
        } catch (error) {
            setError(error instanceof Error ? error.message : "Serveren kan ikke kontaktes.");
        } finally {
            setIsLoading(false);
        }
    }

    return (
        <div>
            <h1>Kvist & Bjælke</h1>
            {error && <p role="alert">{error}</p>}
            {!token ? (
                <form onSubmit={handleSubmit}>
                    <h2>Login</h2>
                    <fieldset disabled={isLoading}>
                        <p><label htmlFor="username">Username</label><br /><input id="username" autoComplete="username" value={username} onChange={event => setUsername(event.target.value)} required maxLength={50} /></p>
                        <p><label htmlFor="password">Password</label><br /><input id="password" type="password" autoComplete="current-password" value={password} onChange={event => setPassword(event.target.value)} required maxLength={128} /></p>
                        <button>{isLoading ? "Logging in…" : "Log in"}</button>
                    </fieldset>
                </form>
            ) : (
                <div>
                    <p>{currentUser?.username} · {currentUser?.role} <button disabled={isLoading} onClick={() => { logout(); clearForm(); setError(""); }}>Log out</button></p>
                    {currentUser && currentUser.role !== "User" && (
                        <form onSubmit={saveUser}>
                            <h2>{editId === null ? "Opret bruger" : "Rediger bruger"}</h2>
                            <fieldset disabled={isLoading}>
                                <p><label htmlFor="edit-username">Brugernavn</label><br /><input id="edit-username" value={username} onChange={event => setUsername(event.target.value)} required pattern="[a-zA-Z0-9._-]{3,50}" /></p>
                                <p><label htmlFor="edit-password">{editId === null ? "Adgangskode" : "Ny adgangskode (valgfri)"}</label><br /><input id="edit-password" type="password" autoComplete="new-password" value={password} onChange={event => setPassword(event.target.value)} required={editId === null} minLength={12} maxLength={128} /></p>
                                <p><label htmlFor="role">Rolle</label><br /><select id="role" value={role} onChange={event => setRole(event.target.value)} disabled={currentUser.role !== "Admin"}><option>User</option>{currentUser.role === "Admin" && <><option>SuperUser</option><option>Admin</option></>}</select></p>
                                <button>Gem</button> <button type="button" onClick={clearForm}>Annuller</button>
                            </fieldset>
                        </form>
                    )}
                    <h2>Brugere</h2>
                    <table>
                        <thead><tr><th>Brugernavn</th><th>Rolle</th><th>Handlinger</th></tr></thead>
                        <tbody>{users.map(user => (
                            <tr key={user.id}>
                                <td>{user.username}</td><td>{user.role}</td>
                                <td>{currentUser && currentUser.role !== "User" ? <><button disabled={isLoading} onClick={() => editUser(user)}>Rediger</button> {user.id !== currentUser.id && <button disabled={isLoading} onClick={() => deleteUser(user)}>Slet</button>}</> : "Kun læsning"}</td>
                            </tr>
                        ))}</tbody>
                    </table>
                </div>
            )}
        </div>
    );
}
