# Kvist & Bjælke ApS

Login og bruger-CRUD til CDS.security. Projektet tager udgangspunkt i `authorization-frontend-backend` og bruger samme opbygning: React med AuthProvider, C#-controllers, UserService, UserRepository, JWT og Argon2id.

## CDS.security — Part 1

## Start lokalt

Kræver .NET SDK 10, Bun og PowerShell 7. Kør fra projektets rod i to terminaler.

**Terminal 1 — backend:**

```powershell
pwsh -File scripts/start-api.ps1
```

**Terminal 2 — frontend:**

```powershell
cd client
bun install
bun run dev
```

- Frontend: **http://localhost:3500**
- Backend og Swagger: **http://localhost:3501/swagger**
- Admin-login findes i den lokale `.env`-fil. Scriptet opretter filen med tilfældige værdier første gang.

Frontend og backend kører separat som i authorization-projektet. Der bruges ingen `wwwroot`, CSS, Python eller Razor Pages. Du ændrer frontend i `client/src/App.tsx`.

## Start med Docker

```powershell
pwsh -File scripts/setup.ps1
docker compose up -d --build
```

Åbn http://localhost:8000. Compose starter en web-container og en API-container. Web-containeren sender login og API-kald videre til C#-API'et på det interne Docker-netværk. Browseren bruger derfor samme adresse til begge dele, også fra en anden computer. API'et har ingen offentlig port. Databasen gemmes i volumen `api-data`, så kontiene bevares ved genstart.

Docker er som udgangspunkt bundet til localhost. På Ubuntu sættes `WEB_BIND_ADDRESS` til serverens faktiske IP som beskrevet under Part 2. Lokal udvikling uden Docker bruger stadig port 3500 og 3501.

## Roller

| Rolle | Rettigheder |
|---|---|
| User | Se User-konti i tabellen |
| SuperUser | Oprette, se, redigere og slette User-konti |
| Admin | Oprette, se, redigere og slette konti med alle tre roller |

API'et kontrollerer både den eksisterende og den ønskede rolle, så SuperUser ikke kan ændre en Admin eller oprette en ny Admin. Man kan ikke slette sig selv eller ændre sin egen rolle.

## Hvad er ændret fra authorization-projektet?

- `AuthProvider`, loginmodeller, `frontend.tsx`, password-hasheren og dens interface tager udgangspunkt i originalen. Login bruger nu `/login` på samme adresse som frontend.
- `UserRepository` og `UserService` er udvidet med hentning af én bruger, opdatering og sletning.
- JWT'et indeholder nu også brugerens rolle, så `[Authorize(Roles = "SuperUser,Admin")]` kan bruges.
- `UsersController` har fuld CRUD. `/register` er beskyttet og bruger den samme oprettelse som `POST /api/Users`.
- PasswordHash er markeret `JsonIgnore`, så hashen aldrig kommer med i API-svar.
- Input valideres, og databasen tillader ikke to identiske brugernavne. Brugernavne er case-sensitive som i originalens login.
- Første Admin oprettes fra `.env`, når databasen er tom. Ingen credentials er hardcoded eller lagt på GitHub.
- Logo, CSS og demo-endpoints fra frontend er fjernet.

## Sikkerhedsvalg

**Passwords:** Originalens Argon2id anvendes med 64 MiB hukommelse, 3 iterationer, parallelisme 4, et tilfældigt salt på 16 bytes og en hash på 32 bytes. Formatet i databasen er det samme som originalen: `hash.salt`. Sammenligning bruger `FixedTimeEquals`. Ved oprettelse/ændring kræves 12–128 tegn. Et tomt felt ved redigering sendes som `null` og bevarer det gamle password. Argon2id er valgt frem for hurtig SHA-256, fordi password-hashing skal være dyr at brute-force.

**Login:** Som i originalen udstedes et signeret JWT med én times levetid. `AuthProvider` gemmer det i `sessionStorage`. API'et validerer signatur, issuer, audience og udløb. Et kopieret JWT kan bruges til udløb, også efter logout eller passwordændring. Kontrol af den aktuelle rolle og konto i databasen sikrer dog, at et gammelt token ikke giver tidligere skriverettigheder efter demotion eller sletning. Efter en forfremmelse skal man logge ind igen for at få et JWT med den nye rolle.

`sessionStorage` er tilgængelig for JavaScript ved XSS; React viser her brugerdata som tekst. HttpOnly-cookies er et muligt alternativ. Til rigtig drift skal HTTP suppleres med HTTPS. Eksemplet har ikke MFA, rate limiting eller password recovery. Browserens API-kald går gennem web-containeren på samme adresse, så serveropsætningen kræver ingen ekstra CORS-adresser.

## Kodestruktur

```text
client/src/auth/AuthProvider.tsx                     Login, token og logout
client/src/App.tsx                                   Loginformular og bruger-CRUD
server/Security/Controller/                          LoginController og UsersController
server/Security/Services/Implementation/UserService.cs
server/DataAccess/Repositories/Implementation/UserRepository.cs
server/Security/Services/Security/                    Originalens password-hasher
server/Security/Services/Authorization/               Originalens JWT-service
server/Security/Startup.cs                           Authentication og services
server/DataAccess/Startup.cs                         Database og repository
```

## Tests

```powershell
dotnet test server/KvistBjaelke.sln -c Release
cd client
bun run check
bun run build
```

C#-testene bruger en separat, midlertidig database og kontrollerer login, CRUD for alle roller, skjulte password-hashes og afvisning af ulovlige rolleændringer. GitHub Actions kører testene og bygger begge Docker-images.

## Screenshots

![Login](docs/screenshots/login.png)
![Brugeradministration](docs/screenshots/admin.png)

Kildekode: [GitHub](https://github.com/R0botninj4/Kvist-Bjaelke-Part1).

## CDS.infrastructure — Part 2

Dette er fremgangsmåden til serveropsætningen. VM'en på skolen og forbindelsen fra VLAN 570 er endnu ikke verificeret.

### Opret Ubuntu Server i Proxmox

1. Vælg den node, det VM-ID og den storage, skolen har tildelt jer.
2. Opret en VM med skolens Ubuntu Server ISO. Et forslag er 2 CPU-kerner, 4 GB RAM og 25 GB disk; det er vores valg, ikke opgavens minimumskrav.
3. Brug skolens netværksbridge. Hvis bridgen bruger VLAN-tags, vælges tag **571**. En bridge, der allerede er tilsluttet VLAN 571, skal ikke nødvendigvis tagges igen. Følg skolens anvisning.
4. Installér Ubuntu Server og opret din egen bruger. OpenSSH kan vælges under installationen, hvis skolen tillader SSH-adgang.
5. Brug den IP, gateway og DNS, skolen tildeler på `10.5.71.0/24`. Kontrollér med `ip -br address` og `ip route`. Vælg ikke en ledig adresse ved at gætte.

### Installér Docker på Ubuntu

Kør i VM'ens konsol eller gennem SSH. Kommandoerne forudsætter en ny Ubuntu Server uden en anden Docker-installation.

```bash
sudo apt update
sudo apt install -y ca-certificates curl git openssl
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc
sudo tee /etc/apt/sources.list.d/docker.sources >/dev/null <<EOF
Types: deb
URIs: https://download.docker.com/linux/ubuntu
Suites: $(. /etc/os-release && echo "${UBUNTU_CODENAME:-$VERSION_CODENAME}")
Components: stable
Architectures: $(dpkg --print-architecture)
Signed-By: /etc/apt/keyrings/docker.asc
EOF
sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo systemctl enable --now docker
sudo docker run --rm hello-world
sudo docker compose version
```

Kilde: [Dockers installationsvejledning til Ubuntu](https://docs.docker.com/engine/install/ubuntu/).

### Hent og start platformen

```bash
git clone https://github.com/R0botninj4/Kvist-Bjaelke-Part1.git
cd Kvist-Bjaelke-Part1
sh scripts/setup.sh
nano .env
```

Repoet er privat, så GitHub kræver adgang ved kloning. Brug din egen godkendte GitHub-adgang. Gem aldrig et token i kloningsadressen eller README.

I `.env` sættes `WEB_BIND_ADDRESS` til Ubuntu-serverens faktiske IPv4-adresse på VLAN 571. Behold de genererede værdier for `JWT_SECRET` og `ADMIN_PASSWORD`. Brugernavnet er `admin`; password står i `.env`. Filen skal ikke med på GitHub eller screenshots.

```bash
sudo docker compose up -d --build
sudo docker compose ps
sudo docker compose logs --tail=50
```

Åbn `http://SERVER-IP:8000`, hvor `SERVER-IP` erstattes af den faktiske adresse. TCP **8000** er valgt, fordi porten er inden for opgavens interval 8000–8100. Browseren bruger denne ene port til både frontend og API. Containerne starter igen efter genstart af Docker/serveren.

Hvis forbindelsen fejler, kontrollér først IP-adresse, containerlogs og VLAN. Hvis Ubuntu har en aktiv lokal firewall, skal den også tillade den krævede trafik på TCP 8000 fra VLAN 570. Skolens netværksfirewall er ifølge opgaven allerede konfigureret; den skal ikke ændres.

### Kontrollér og dokumentér serveren

```bash
cat /etc/os-release
ip -br address
ip route
sudo systemctl is-active docker
sudo docker compose ps
curl -f http://SERVER-IP:8000/health
```

`/health` skal svare `OK`. Tag screenshots af Ubuntu-version, IP-konfiguration, Docker og kørende containere. Fra klient-VM'en på VLAN 570 åbnes derefter `http://SERVER-IP:8000`. Test login og brugerfunktioner og gem screenshots med adresselinjen synlig.

De faktiske IP-adresser, screenshots og testresultater tilføjes efter opsætningen på skolen. En vellykket lokal test eller et Docker-build dokumenterer ikke i sig selv forbindelsen mellem skolens VLAN.

Ved opdatering køres `git pull` og `sudo docker compose up -d --build`. `sudo docker compose down` stopper platformen og bevarer databasen. Brug ikke `down -v`, hvis kontiene skal bevares. En ny server/et nyt Docker-volumen får en ny database; den lokale `app.db` kopieres ikke automatisk.
