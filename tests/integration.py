"""End-to-end API/security tests. Uses a temporary SQLite DB and only Python's stdlib.

Run after `bun run build` in client and `dotnet build server/Security -c Release`:
    python tests/integration.py
Or test a fresh running container (including rate limits):
    python tests/integration.py --url http://localhost:8000 --admin admin
The external admin password is read from TEST_ADMIN_PASSWORD, never command arguments.
"""
import argparse
import base64
import hashlib
import hmac
import json
import os
from pathlib import Path
import secrets
import socket
import sqlite3
import subprocess
import tempfile
import time
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("--url")
parser.add_argument("--admin", default="admin")
args = parser.parse_args()
process = None
checks = 0
password = os.environ.get("TEST_ADMIN_PASSWORD") if args.url else secrets.token_urlsafe(24)
if not password:
    raise SystemExit("Set TEST_ADMIN_PASSWORD for external tests.")
secret = secrets.token_urlsafe(48)
temp = tempfile.TemporaryDirectory(prefix="kvist-tests-")
db_path = Path(temp.name) / "test.db"
log = open(Path(temp.name) / "server.log", "w+", encoding="utf-8")

if args.url:
    base = args.url.rstrip("/")
else:
    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        port = sock.getsockname()[1]
    base = f"http://127.0.0.1:{port}"


def check(condition, label):
    global checks
    assert condition, label
    checks += 1
    print(f"PASS {checks:03}: {label}", flush=True)


def request(method, path, body=None, token=None):
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = "Bearer " + token
    req = urllib.request.Request(base + path, method=method, headers=headers,
                                 data=json.dumps(body).encode() if body is not None else None)
    try:
        response = urllib.request.urlopen(req, timeout=30)
    except urllib.error.HTTPError as error:
        response = error
    raw = response.read()
    try:
        data = json.loads(raw) if raw else None
    except (ValueError, UnicodeDecodeError):
        data = raw.decode("utf-8", errors="replace")
    return response.status, data, response.headers


def expect(method, path, status, body=None, token=None):
    actual, data, headers = request(method, path, body, token)
    check(actual == status, f"{method} {path}: expected {status}, got {actual} ({str(data)[:180] if actual != status else 'ok'})")
    return data


def login(username, pw=password):
    return expect("POST", "/api/auth/login", 200, {"username": username, "password": pw})["token"]


def payload(name, role="User", pw=None):
    return {"username": name, "role": role, "password": pw or password}


def start():
    global process
    env = os.environ.copy()
    env.update({"JwtSecret": secret, "BootstrapAdmin__Username": args.admin,
                "BootstrapAdmin__Password": password, "ASPNETCORE_URLS": base,
                "ASPNETCORE_ENVIRONMENT": "Production",
                "ConnectionStrings__DefaultConnection": f"Data Source={db_path}"})
    process = subprocess.Popen(["dotnet", str(ROOT / "server/Security/bin/Release/net10.0/Security.dll")],
                               cwd=ROOT / "server/Security", env=env, stdout=log, stderr=log)
    for _ in range(120):
        if process.poll() is not None:
            log.flush(); log.seek(0)
            raise RuntimeError("Server exited: " + log.read())
        try:
            if request("GET", "/health")[0] == 200:
                return
        except (OSError, urllib.error.URLError):
            pass
        time.sleep(0.25)
    raise RuntimeError("Server did not start")


try:
    if not args.url:
        start()
    expect("GET", "/health", 200)
    html = expect("GET", "/", 200)
    check(isinstance(html, str) and '<html lang="da">' in html, "Built Danish frontend served by backend")
    status, _, headers = request("GET", "/api/users")
    check(status == 401, "Anonymous list denied")
    check(headers.get("Content-Security-Policy", "").startswith("default-src 'self'"), "CSP present")
    check(headers.get("Cache-Control") == "no-store", "API responses are not cached")
    expect("POST", "/register", 401, payload("intruder", "Admin"))
    expect("GET", "/api/missing", 404)
    for method, path in [("GET", "/api/users/1"), ("POST", "/api/users"), ("PUT", "/api/users/1"), ("DELETE", "/api/users/1")]:
        expect(method, path, 401, payload("intruder") if method in ("POST", "PUT") else None)
    unknown = expect("POST", "/api/auth/login", 401, {"username": "unknown-account", "password": password})
    wrong = expect("POST", "/api/auth/login", 401, {"username": args.admin, "password": "wrong-password"})
    check(unknown == wrong, "Unknown username and wrong password have the same error")
    expect("POST", "/api/auth/login", 400, {"username": None, "password": None})
    admin = login(args.admin.upper())
    me = expect("GET", "/api/auth/me", 200, token=admin)
    check(me["role"] == "Admin", "Case-insensitive login authenticates bootstrap admin")
    superuser = expect("POST", "/api/users", 201, payload("test-super", "SuperUser"), admin)
    normal = expect("POST", "/api/users", 201, payload("test-user"), admin)
    second_admin = expect("POST", "/api/users", 201, payload("test-admin", "Admin"), admin)
    for role in ("admin", "Root", "", None):
        expect("POST", "/api/users", 400, payload("bad-role", role), admin)
    expect("POST", "/api/users", 400, payload("short-password", pw="short"), admin)
    expect("POST", "/api/users", 400, payload("contains spaces"), admin)
    expect("POST", "/api/users", 409, payload("TEST-USER"), admin)
    all_users = expect("GET", "/api/users", 200, token=admin)
    check(len(all_users) == 4, "Admin reads all three roles")
    check(all(set(u) == {"id", "username", "role", "createdAt"} for u in all_users), "API exposes only safe DTO fields, no hashes")
    su = login("test-super")
    user = login("test-user")
    for token in (su, user):
        rows = expect("GET", "/api/users", 200, token=token)
        check(all(row["role"] == "User" for row in rows), "Lower roles see only ordinary users")
        expect("GET", f"/api/users/{normal['id']}", 200, token=token)
        expect("GET", f"/api/users/{me['id']}", 403, token=token)
        expect("GET", f"/api/users/{superuser['id']}", 403, token=token)
    expect("POST", "/api/users", 403, payload("denied-create"), user)
    expect("PUT", f"/api/users/{normal['id']}", 403, payload("denied-update"), user)
    expect("DELETE", f"/api/users/{normal['id']}", 403, token=user)
    created = expect("POST", "/api/users", 201, payload("su-created"), su)
    for role in ("Admin", "SuperUser"):
        expect("POST", "/api/users", 403, payload("escalation", role), su)
        expect("PUT", f"/api/users/{normal['id']}", 403, payload("test-user", role), su)
    for target in (me, superuser, second_admin):
        expect("PUT", f"/api/users/{target['id']}", 403, payload(target["username"]), su)
        expect("DELETE", f"/api/users/{target['id']}", 403, token=su)
    # Editing the bootstrap account is forbidden only for destructive self actions.
    expect("PUT", f"/api/users/{me['id']}", 409, payload(args.admin, "User"), admin)
    expect("DELETE", f"/api/users/{me['id']}", 409, token=admin)
    expect("PUT", f"/api/users/{created['id']}", 200, {"username": "su-renamed", "role": "User", "password": None}, su)
    retained = login("su-renamed")
    expect("GET", "/api/auth/me", 200, token=retained)
    expect("PUT", f"/api/users/{created['id']}", 400, {"username": "su-renamed", "role": "User", "password": ""}, su)
    expect("PUT", f"/api/users/{created['id']}", 409, {"username": "TEST-USER", "role": "User", "password": None}, su)
    expect("GET", "/api/auth/me", 200, token=retained)
    expect("DELETE", f"/api/users/{created['id']}", 204, token=su)
    expect("GET", "/api/auth/me", 401, token=retained)
    expect("GET", f"/api/users/{created['id']}", 404, token=admin)
    expect("PUT", "/api/users/999999", 404, payload("missing"), admin)
    expect("DELETE", "/api/users/999999", 404, token=admin)
    new_password = secrets.token_urlsafe(24)
    expect("PUT", f"/api/users/{normal['id']}", 200, payload("test-user", pw=new_password), su)
    expect("GET", "/api/auth/me", 401, token=user)
    expect("POST", "/api/auth/login", 401, {"username": "test-user", "password": password})
    user = login("test-user", new_password)
    expect("POST", "/api/auth/logout", 204, token=user)
    expect("GET", "/api/users", 401, token=user)
    expect("POST", "/api/auth/logout", 401, token=user)
    # Admin CRUD on higher roles, including immediate revocation on demotion.
    expect("PUT", f"/api/users/{superuser['id']}", 200, {"username": "test-super", "role": "User", "password": None}, admin)
    expect("GET", "/api/auth/me", 401, token=su)
    demoted = login("test-super")
    expect("POST", "/api/users", 403, payload("still-denied"), demoted)
    expect("PUT", f"/api/users/{superuser['id']}", 200, {"username": "test-super", "role": "SuperUser", "password": None}, admin)
    expect("DELETE", f"/api/users/{superuser['id']}", 204, token=admin)
    expect("PUT", f"/api/users/{second_admin['id']}", 200, {"username": "test-admin-renamed", "role": "Admin", "password": None}, admin)
    admin2 = login("test-admin-renamed")
    expect("DELETE", f"/api/users/{second_admin['id']}", 204, token=admin)
    expect("GET", "/api/auth/me", 401, token=admin2)
    # Tampering is rejected even if only a single signature character is changed.
    parts = admin.split(".")
    tampered = ".".join(parts[:2] + [("A" if parts[2][0] != "A" else "B") + parts[2][1:]])
    expect("GET", "/api/users", 401, token=tampered)
    expect("GET", "/api/users", 401, token="not-a-jwt")

    if not args.url:
        def encode(data):
            return base64.urlsafe_b64encode(json.dumps(data, separators=(",", ":")).encode()).decode().rstrip("=")

        claims = json.loads(base64.urlsafe_b64decode(parts[1] + "=" * (-len(parts[1]) % 4)))
        def sign(claims):
            body = parts[0] + "." + encode(claims)
            sig = base64.urlsafe_b64encode(hmac.new(secret.encode(), body.encode(), hashlib.sha256).digest()).decode().rstrip("=")
            return body + "." + sig
        expired = dict(claims, exp=int(time.time()) - 10, nbf=int(time.time()) - 60)
        expect("GET", "/api/users", 401, token=sign(expired))
        expect("GET", "/api/users", 401, token=sign(dict(claims, iss="wrong-issuer")))
        expect("GET", "/api/users", 401, token=sign(dict(claims, aud="wrong-audience")))
        with sqlite3.connect(db_path) as connection:
            stored = connection.execute('SELECT PasswordHash FROM Users').fetchall()
        connection.close()
        check(all(row[0].startswith("$argon2id$v=19$m=65536,t=3,p=4$") for row in stored), "Database stores versioned Argon2id hashes")
        check(len({row[0].split("$")[4] for row in stored}) == len(stored), "Each account has a unique random salt")
        check(all(password not in row[0] and new_password not in row[0] for row in stored), "No plaintext passwords in password columns")
        process.terminate(); process.wait(timeout=15)
        start()
        expect("GET", "/api/auth/me", 200, token=admin)
        check(len(expect("GET", "/api/users", 200, token=admin)) == 2, "SQLite CRUD persists across a server restart")
        expect("GET", "/api/auth/me", 401, token=user)

    expect("POST", "/api/auth/logout", 204, token=admin)
    expect("GET", "/api/users", 401, token=admin)
    # Rate limit last: no waiting or disabling production security for the tests.
    responses = [request("POST", "/api/auth/login", {"username": "unknown-account", "password": "wrong-password"})[0] for _ in range(31)]
    check(429 in responses, "Repeated login attempts are rate limited")
    check(all(s in (401, 429) for s in responses), "Invalid repeated logins never authenticate")
    print(f"\n{checks} checks passed ({'container/external HTTP' if args.url else 'isolated .NET + SQLite HTTP'}).", flush=True)
except Exception:
    if not args.url:
        log.flush(); log.seek(0)
        print(log.read()[-10000:])
    raise
finally:
    if process and process.poll() is None:
        process.terminate(); process.wait(timeout=15)
    log.close()
    temp.cleanup()
