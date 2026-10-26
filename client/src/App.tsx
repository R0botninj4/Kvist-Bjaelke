import { useEffect, useRef, useState, type FormEvent } from "react";
import { useAuth, type Role, type User } from "./auth/AuthProvider";

const roleText: Record<Role, string> = { User: "Bruger", SuperUser: "Superbruger", Admin: "Administrator" };
const errorText = (error: unknown) => error instanceof Error ? error.message : "Der opstod en fejl.";

function Brand() {
  return <a href="/" className="brand" aria-label="Kvist og Bjælke, forside"><span className="brand-mark" aria-hidden="true">K/B</span><span>Kvist & Bjælke<span className="brand-caption">MENNESKER. HÅNDVÆRK. TILLID.</span></span></a>;
}

function Login() {
  const { login } = useAuth();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function submit(e: FormEvent) {
    e.preventDefault(); if (busy) return;
    setBusy(true); setError("");
    try { await login(username, password); setPassword(""); }
    catch (e) { setError(errorText(e)); }
    finally { setBusy(false); }
  }
  return <div className="login-layout">
    <section className="welcome"><Brand /><div className="welcome-copy"><span className="eyebrow light">KVIST & BJÆLKE APS / MEDARBEJDERPORTAL</span><h1>Godt arbejde<br />begynder med<br /><em>tryg adgang.</em></h1><p>Én samlet indgang til vores brugere.<br />Bygget på tillid. Beskyttet med omtanke.</p></div><span className="welcome-footer">STÆRKE FORBINDELSER. SOLIDE FUNDAMENTER.</span><div className="wood-lines" aria-hidden="true" /></section>
    <main className="login-main"><div className="login-card"><span className="eyebrow">VELKOMMEN TILBAGE</span><h2>Log ind på portalen</h2><p className="muted">Brug din konto fra Kvist & Bjælke.</p><form onSubmit={submit} aria-busy={busy}>
      <label htmlFor="username">Brugernavn</label><input id="username" autoComplete="username" required maxLength={50} value={username} onChange={e => setUsername(e.target.value)} disabled={busy} autoFocus />
      <label htmlFor="password">Adgangskode</label><input id="password" type="password" autoComplete="current-password" required maxLength={128} value={password} onChange={e => setPassword(e.target.value)} disabled={busy} />
      {error && <p className="alert error" role="alert">{error}</p>}
      <button className="primary full" disabled={busy}>{busy ? "Logger ind…" : "Log ind →"}</button>
    </form><p className="login-help">Mangler du adgang? Kontakt din administrator.</p></div><span className="login-footer">INTERN PORTAL · KVIST & BJÆLKE APS</span></main>
  </div>;
}

function UserEditor({ target, onClose, onSaved }: { target: User | "new"; onClose: () => void; onSaved: (message: string) => void }) {
  const { user, api } = useAuth();
  const editing = target !== "new";
  const [username, setUsername] = useState(editing ? target.username : "");
  const [role, setRole] = useState<Role>(editing ? target.role : "User");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const dialog = useRef<HTMLDialogElement>(null);
  useEffect(() => { dialog.current?.showModal(); }, []);
  async function submit(e: FormEvent) {
    e.preventDefault(); if (busy) return; setBusy(true); setError("");
    try {
      await api(editing ? `/api/users/${target.id}` : "/api/users", {
        method: editing ? "PUT" : "POST", body: JSON.stringify({ username, role, password: editing && !password ? null : password })
      });
      setPassword("");
      onSaved(editing ? "Brugeren er opdateret. Tidligere login-sessioner er tilbagekaldt." : "Den nye bruger er oprettet.");
    } catch (e) { setError(errorText(e)); setBusy(false); }
  }
  return <dialog ref={dialog} onCancel={e => { if (busy) e.preventDefault(); else onClose(); }} aria-labelledby="editor-title"><form onSubmit={submit} aria-busy={busy}>
    <div className="dialog-header"><span className="eyebrow">BRUGERADMINISTRATION</span><button type="button" className="icon-button" aria-label="Luk" onClick={onClose} disabled={busy}>×</button></div>
    <h2 id="editor-title">{editing ? "Rediger bruger" : "Opret en ny bruger"}</h2><p className="muted">{editing ? "Opdater kontoens oplysninger og adgang." : "Giv en kollega adgang til portalen."}</p>
    <label htmlFor="edit-username">Brugernavn</label><input id="edit-username" autoComplete="off" value={username} onChange={e => setUsername(e.target.value)} required pattern="[a-zA-Z0-9._\-]{3,50}" minLength={3} maxLength={50} disabled={busy} /><small>3–50 tegn. Bogstaver, tal, punktum, bindestreg og underscore.</small>
    <label htmlFor="edit-role">Rolle</label><select id="edit-role" value={role} onChange={e => setRole(e.target.value as Role)} disabled={busy || (editing && target.id === user?.id) || user?.role !== "Admin"}>
      <option value="User">Bruger</option>{user?.role === "Admin" && <><option value="SuperUser">Superbruger</option><option value="Admin">Administrator</option></>}
    </select>
    <label htmlFor="edit-password">{editing ? "Ny adgangskode (valgfri)" : "Adgangskode"}</label><input id="edit-password" type="password" autoComplete="new-password" value={password} onChange={e => setPassword(e.target.value)} minLength={12} maxLength={128} required={!editing} disabled={busy} /><small>{editing ? "Lad feltet være tomt for at beholde adgangskoden. " : ""}Mindst 12 tegn.</small>
    {error && <p className="alert error" role="alert">{error}</p>}
    <div className="dialog-actions"><button type="button" className="secondary" onClick={onClose} disabled={busy}>Annuller</button><button className="primary" disabled={busy}>{busy ? "Gemmer…" : editing ? "Gem ændringer" : "Opret bruger"}</button></div>
  </form></dialog>;
}

function DeleteDialog({ target, onClose, onSaved }: { target: User; onClose: () => void; onSaved: (message: string) => void }) {
  const { api } = useAuth();
  const dialog = useRef<HTMLDialogElement>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  useEffect(() => { dialog.current?.showModal(); }, []);
  async function remove() {
    if (busy) return; setBusy(true);
    try { await api(`/api/users/${target.id}`, { method: "DELETE" }); onSaved(`Brugeren ${target.username} er slettet.`); }
    catch (e) { setError(errorText(e)); setBusy(false); }
  }
  return <dialog ref={dialog} onCancel={e => { if (busy) e.preventDefault(); else onClose(); }} aria-labelledby="delete-title"><span className="eyebrow">SLET BRUGER</span><h2 id="delete-title">Slet {target.username}?</h2><p>Kontoen og dens login-sessioner slettes permanent. Handlingen kan ikke fortrydes.</p>{error && <p role="alert" className="alert error">{error}</p>}<div className="dialog-actions"><button className="secondary" onClick={onClose} disabled={busy}>Annuller</button><button className="danger" onClick={remove} disabled={busy}>{busy ? "Sletter…" : "Slet bruger"}</button></div></dialog>;
}

function Dashboard() {
  const { user, logout, api } = useAuth();
  const [users, setUsers] = useState<User[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [search, setSearch] = useState("");
  const [roleFilter, setRoleFilter] = useState("all");
  const [editor, setEditor] = useState<User | "new" | null>(null);
  const [deleting, setDeleting] = useState<User | null>(null);
  const [revision, setRevision] = useState(0);
  const [loggingOut, setLoggingOut] = useState(false);
  const admin = user?.role === "Admin", writable = user?.role !== "User";
  useEffect(() => {
    let active = true; setLoading(true); setError("");
    api<User[]>("/api/users").then(data => { if (active) setUsers(data); })
      .catch(e => { if (active) setError(errorText(e)); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [api, revision]);
  function saved(text: string) { setEditor(null); setDeleting(null); setMessage(text); setRevision(r => r + 1); }
  async function signOut() {
    setLoggingOut(true);
    try { await logout(); } catch (e) { setError(errorText(e)); setLoggingOut(false); }
  }
  const visible = users.filter(u => u.username.toLowerCase().includes(search.toLowerCase()) && (roleFilter === "all" || u.role === roleFilter));
  return <div className="dashboard"><header className="topbar"><Brand /><span className="portal-label">MEDARBEJDERPORTAL</span><div className="account"><span className="avatar">{user?.username.slice(0, 2).toUpperCase()}</span><div><strong>{user?.username}</strong><small>{roleText[user!.role]}</small></div><button className="secondary compact" onClick={signOut} disabled={loggingOut}>{loggingOut ? "Logger ud…" : "Log ud"}</button></div></header>
    <nav className="section-nav"><span className="active">Brugere</span><span className="nav-note">SIKKER ADGANG. KLARE ROLLER.</span></nav>
    <main className="content"><div className="breadcrumb">PORTAL <span>/</span> BRUGERADMINISTRATION</div><div className="page-heading"><div><h1>Godt overblik.<br /><em>De rette adgange.</em></h1><p className="muted">Administrér de mennesker, der er en del af Kvist & Bjælke.</p></div>{writable && <button className="primary" onClick={() => { setMessage(""); setEditor("new"); }}>+ Opret bruger</button>}</div>
    <div className="stats"><div><span>SYNLIGE KONTI</span><strong>{loading ? "—" : users.length.toString().padStart(2, "0")}</strong></div><div><span>DIN ROLLE</span><strong className="stat-role">{roleText[user!.role]}</strong></div><div className="access-note"><span>DINE RETTIGHEDER</span><p>{admin ? "Du kan administrere brugere, superbrugere og administratorer." : writable ? "Du kan oprette, læse, redigere og slette almindelige brugere." : "Du har læseadgang til almindelige brugere."}</p></div></div>
    {message && <p role="status" className="alert success">✓ {message}</p>}{error && <div role="alert" className="alert error">{error} <button className="text-button" onClick={() => setRevision(r => r + 1)}>Prøv igen</button></div>}
    <section className="users-panel" aria-label="Brugerliste"><div className="panel-heading"><div><h2>Brugere</h2><span className="muted">{visible.length} {visible.length === 1 ? "konto" : "konti"} i oversigten</span></div><div className="filters"><label className="sr-only" htmlFor="search">Søg efter bruger</label><input id="search" type="search" placeholder="Søg efter brugernavn…" value={search} onChange={e => setSearch(e.target.value)} />{admin && <><label className="sr-only" htmlFor="role-filter">Filtrér efter rolle</label><select id="role-filter" value={roleFilter} onChange={e => setRoleFilter(e.target.value)}><option value="all">Alle roller</option><option value="User">Brugere</option><option value="SuperUser">Superbrugere</option><option value="Admin">Administratorer</option></select></>}</div></div>
    <div className="table-scroll"><table><thead><tr><th scope="col">BRUGERNAVN</th><th scope="col">ROLLE</th><th scope="col">OPRETTET</th><th scope="col"><span className="sr-only">Handlinger</span></th></tr></thead><tbody>{loading ? <tr><td colSpan={4} className="empty" role="status">Henter brugere…</td></tr> : visible.length === 0 ? <tr><td colSpan={4} className="empty">{error ? "Brugerlisten kunne ikke hentes." : search || roleFilter !== "all" ? "Ingen brugere matcher din søgning." : "Ingen almindelige brugere endnu."}</td></tr> : visible.map(u => <tr key={u.id}><td><div className="user-cell"><span className="avatar pale">{u.username.slice(0, 2).toUpperCase()}</span><div><strong>{u.username}</strong><small>Konto #{u.id}{u.id === user?.id ? " · Dig" : ""}</small></div></div></td><td><span className={`badge ${u.role.toLowerCase()}`}>{roleText[u.role]}</span></td><td className="date">{new Intl.DateTimeFormat("da-DK", { day: "numeric", month: "short", year: "numeric" }).format(new Date(u.createdAt))}</td><td><div className="row-actions">{writable && (admin || u.role === "User") ? <><button className="text-button" onClick={() => setEditor(u)}>Rediger</button>{u.id !== user?.id && <button className="text-button delete" onClick={() => setDeleting(u)}>Slet</button>}</> : <span className="read-only">Kun læsning</span>}</div></td></tr>)}</tbody></table></div><div className="panel-footer"><span>Adgange styres af din rolle</span><span>KVIST & BJÆLKE APS</span></div></section>
    <footer className="content-footer">Bygget på tillid. Beskyttet med omtanke.<span>INTERN BRUGERADMINISTRATION</span></footer></main>
    {editor !== null && <UserEditor target={editor} onClose={() => setEditor(null)} onSaved={saved} />}{deleting && <DeleteDialog target={deleting} onClose={() => setDeleting(null)} onSaved={saved} />}
  </div>;
}

export function App() {
  const { user, loading } = useAuth();
  if (loading) return <main className="loading-screen" role="status">Henter din session…</main>;
  return user ? <Dashboard /> : <Login />;
}
