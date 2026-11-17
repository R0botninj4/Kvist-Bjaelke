# Kvist & Bjælke ApS

Login og bruger-CRUD til CDS.security. Projektet tager udgangspunkt i `authorization-frontend-backend` og bruger samme opbygning: React med AuthProvider, C#-controllers, UserService, UserRepository, JWT og Argon2id.

## CDS.security — Part 1

### Start lokalt

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

### Start med Docker

```powershell
pwsh -File scripts/setup.ps1
docker compose up -d --build
```

Åbn http://localhost:8000. Compose starter en web-container og en API-container. Web-containeren sender login og API-kald videre til C#-API'et på det interne Docker-netværk. Browseren bruger derfor samme adresse til begge dele, også fra en anden computer. API'et har ingen offentlig port. Databasen gemmes i volumen `api-data`, så kontiene bevares ved genstart.

Docker er som udgangspunkt bundet til localhost. På Ubuntu sættes `WEB_BIND_ADDRESS` til serverens faktiske IP som beskrevet under Part 2. Lokal udvikling uden Docker bruger stadig port 3500 og 3501.

### Roller

| Rolle | Rettigheder |
|---|---|
| User | Se User-konti i tabellen |
| SuperUser | Oprette, se, redigere og slette User-konti |
| Admin | Oprette, se, redigere og slette konti med alle tre roller |

API'et kontrollerer både den eksisterende og den ønskede rolle, så SuperUser ikke kan ændre en Admin eller oprette en ny Admin. Man kan ikke slette sig selv eller ændre sin egen rolle.

### Hvad er ændret fra authorization-projektet?

- `AuthProvider`, loginmodeller, `frontend.tsx`, password-hasheren og dens interface tager udgangspunkt i originalen. Login bruger nu `/login` på samme adresse som frontend.
- `UserRepository` og `UserService` er udvidet med hentning af én bruger, opdatering og sletning.
- JWT'et indeholder nu også brugerens rolle, så `[Authorize(Roles = "SuperUser,Admin")]` kan bruges.
- `UsersController` har fuld CRUD. `/register` er beskyttet og bruger den samme oprettelse som `POST /api/Users`.
- PasswordHash er markeret `JsonIgnore`, så hashen aldrig kommer med i API-svar.
- Input valideres, og databasen tillader ikke to identiske brugernavne. Brugernavne er case-sensitive som i originalens login.
- Første Admin oprettes fra `.env`, når databasen er tom. Ingen credentials er hardcoded eller lagt på GitHub.
- Logo, CSS og demo-endpoints fra frontend er fjernet.

### Sikkerhedsvalg

**Passwords:** Originalens Argon2id anvendes med 64 MiB hukommelse, 3 iterationer, parallelisme 4, et tilfældigt salt på 16 bytes og en hash på 32 bytes. Formatet i databasen er det samme som originalen: `hash.salt`. Sammenligning bruger `FixedTimeEquals`. Ved oprettelse/ændring kræves 12–128 tegn. Et tomt felt ved redigering sendes som `null` og bevarer det gamle password. Argon2id er valgt frem for hurtig SHA-256, fordi password-hashing skal være dyr at brute-force.

**Login:** Som i originalen udstedes et signeret JWT med én times levetid. `AuthProvider` gemmer det i `sessionStorage`. API'et validerer signatur, issuer, audience og udløb. Et kopieret JWT kan bruges til udløb, også efter logout eller passwordændring. Kontrol af den aktuelle rolle og konto i databasen sikrer dog, at et gammelt token ikke giver tidligere skriverettigheder efter demotion eller sletning. Efter en forfremmelse skal man logge ind igen for at få et JWT med den nye rolle.

`sessionStorage` er tilgængelig for JavaScript ved XSS; React viser her brugerdata som tekst. HttpOnly-cookies er et muligt alternativ. Til rigtig drift skal HTTP suppleres med HTTPS. Eksemplet har ikke MFA, rate limiting eller password recovery. Browserens API-kald går gennem web-containeren på samme adresse, så serveropsætningen kræver ingen ekstra CORS-adresser.

### Kodestruktur

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

### Tests

```powershell
dotnet test server/KvistBjaelke.sln -c Release
cd client
bun run check
bun run build
```

C#-testene bruger en separat, midlertidig database og kontrollerer login, CRUD for alle roller, skjulte password-hashes og afvisning af ulovlige rolleændringer. GitHub Actions kører testene og bygger begge Docker-images.

### Screenshots

![Login](docs/screenshots/login.png)
![Brugeradministration](docs/screenshots/admin.png)

Kildekode: [GitHub](https://github.com/R0botninj4/Kvist-Bjaelke-Part1).

## CDS.infrastructure — Part 2

Platformen er sat op på Ubuntu Server i skolens Proxmox. Docker-containerne kører på serveren, og serverens egen test af `/health` svarer `OK`. En separat Ubuntu Desktop-VM på klientnetværket har åbnet login-siden på serveren over HTTP. Login, CRUD og rollernes rettigheder er kontrolleret i applikationens automatiserede tests. Billedet fra klienten dokumenterer adgang til login-siden over netværket.

### IP-plan — 2.1.1

IP-planen bygger på VLAN3-dokumentet. Dette er forslaget til virksomhedens netværk; alle de foreslåede VLAN er ikke oprettet i skolelabbet.

Vi bruger private adresser i 10.5.0.0/16. VLAN 570 og 571 er givet i opgaven. De øvrige følger samme nummerering, fx VLAN 572 med 10.5.72.0/24.

| VLAN | Navn | Enheder | Nu | Ved fordobling | Mindste subnet inkl. gateway | Valgt netværk | Foreslået gateway | Adressering |
|---|---|---|---:|---:|---|---|---|---|
| 570 | Clients | 15 stationære og 10 bærbare | 25 | 50 | /26, 62 værter | 10.5.70.0/24 | 10.5.70.1 | DHCP |
| 571 | Servers | Servere og login-VM | 4 | 8 | /28, 14 værter | 10.5.71.0/24 | 10.5.71.1 | Statisk |
| 572 | Voice | IP-telefoner | 12 | 24 | /27, 30 værter | 10.5.72.0/24 | 10.5.72.1 | DHCP |
| 573 | Printers | Netværksprintere | 3 | 6 | /28, 14 værter | 10.5.73.0/24 | 10.5.73.1 | Statisk/reservation |
| 574 | Cameras | Overvågningskameraer | 6 | 12 | /28, 14 værter | 10.5.74.0/24 | 10.5.74.1 | Statisk |
| 575 | Guest | Gæsters enheder | ca. 50 | 100 | /25, 126 værter | 10.5.75.0/24 | 10.5.75.1 | DHCP, kort lejetid |
| 579 | MGMT | Switches, firewall og access points | ca. 10 | 20 | /27, 30 værter | 10.5.79.0/24 | 10.5.79.1 | Statisk |

Vi regner med dobbelt så mange enheder for at give plads til vækst. De fire servere er de tre fra opgaven plus login-VM. Gæster og management er skønnet til ca. 50 og 10 enheder.

I virksomhedens plan er gatewayen .1. Statiske adresser bruger .2–.49 og DHCP .50–.250. Printere kan bruge DHCP-reservationer. Skolelabbet har sin egen adressering.

De fire access points giver medarbejdere adgang til VLAN 570 og gæster til VLAN 575. Administration ligger på VLAN 579. Medarbejdere kan få fjernadgang gennem VPN på firewallen.

| Netværk | Begrundelse for opdelingen |
|---|---|
| Clients | Medarbejdernes computere adskilles fra serverne, så adgangen kan begrænses. |
| Servers | Servere indeholder data og loginplatformen. Kun nødvendige forbindelser skal tillades. |
| Voice | Telefontrafik adskilles og kan prioriteres med QoS. |
| Printers | Printere har ofte lang levetid og skal ikke kunne tilgå alle interne systemer. |
| Cameras | Kameraer håndterer følsomme optagelser og bør kun nå fx optageserveren. |
| Guest | Ukendte enheder skal have internetadgang, men ikke adgang til interne systemer. |
| MGMT | Administrationssider skal kun være tilgængelige for IT. |

VLAN adskiller enhedsgrupperne. Firewallregler styrer trafikken mellem dem.

### Subnetting — 2.1.2

| Netværk | Netværksadresse | Maske | Brugbare værter | Værtsinterval | Broadcast | Behov inkl. vækst og gateway | Ledige efter behov |
|---|---|---|---:|---|---|---:|---:|
| Clients, valgt | 10.5.70.0/24 | 255.255.255.0 | 254 | 10.5.70.1–10.5.70.254 | 10.5.70.255 | 51 | 203 |
| Clients, mindste passende | 10.5.70.0/26 | 255.255.255.192 | 62 | 10.5.70.1–10.5.70.62 | 10.5.70.63 | 51 | 11 |
| Servers, valgt | 10.5.71.0/24 | 255.255.255.0 | 254 | 10.5.71.1–10.5.71.254 | 10.5.71.255 | 9 | 245 |
| Servers, mindste passende | 10.5.71.0/28 | 255.255.255.240 | 14 | 10.5.71.1–10.5.71.14 | 10.5.71.15 | 9 | 5 |
| Voice, valgt | 10.5.72.0/24 | 255.255.255.0 | 254 | 10.5.72.1–10.5.72.254 | 10.5.72.255 | 25 | 229 |
| Voice, mindste passende | 10.5.72.0/27 | 255.255.255.224 | 30 | 10.5.72.1–10.5.72.30 | 10.5.72.31 | 25 | 5 |

Brugbare adresser beregnes med `2^(32 − præfiks) − 2`. Netværksadresse og broadcast kan ikke bruges til enheder. 50 klienter og en gateway kræver 51 adresser, så /26 er nok.

Vi vælger /24 med 254 brugbare adresser i hvert VLAN. Det er nemt at administrere og giver plads til vækst. De mindre subnet i tabellen viser minimumsbehovet.

### Server og klient i Proxmox — 2.2 og 2.3

| | Server | Klient |
|---|---|---|
| Node | pve01 | pve01 |
| VM-ID | 116 | 117 |
| Navn i Proxmox | JPM-server | JPM-Desktop |
| Operativsystem | Ubuntu Server 26.04.1 LTS | Ubuntu Desktop 26.04.1 |
| CPU | 2 kerner | 2 kerner |
| RAM | 4096 MiB | 4096 MiB |
| Disk | 32 GiB, local-lvm | 32 GiB, local-lvm |
| Bridge | VLAN571 | vmbr0 |
| Netværk | VLAN 571, 10.5.71.0/24 | Klientnetværket, 10.5.70.0/24 |
| Observeret IPv4 | 10.5.71.33 | 10.5.70.169/24 |

Skolens krav til VM'erne er mindst 4096 MB RAM og to CPU-kerner. VM-ID blev valgt automatisk. Serveren blev installeret fra Ubuntu Server-ISO'en, og ISO'en blev frakoblet efter installationen. Klienten bruger Ubuntu Desktop, så platformen kan testes i Firefox gennem Proxmox-konsollen.

Serveren bruger skolens DHCP og har IP `10.5.71.33/24`. Gateway, DNS og DHCP-server er `10.5.71.5`. Vi beholdt skolens opsætning, da vi ikke kender DHCP-puljen. Hvis IP-adressen ændres, skal `WEB_BIND_ADDRESS` og adressen i browseren opdateres.

![Server- og klient-VM i Proxmox](docs/screenshots/infrastructure/virtual-machines.png)

Begge VM'er vises som startet i Proxmox.

![Serverens hardware og VLAN571-bridge](docs/screenshots/infrastructure/server-hardware.png)

VM 116 har to kerner, 4 GiB RAM, 32 GiB disk og `bridge=VLAN571`. CD/DVD-drevet er tomt efter installationen.

![Serverens faktiske netværkskonfiguration](docs/screenshots/infrastructure/server-network.png)

Netværksoversigten for `ens18` viser DHCP-adresse, gateway og DNS. Skolelabbets gateway `.5` adskiller sig fra den foreslåede `.1` i virksomhedens IP-plan.

![Klientens hardware og vmbr0-bridge](docs/screenshots/infrastructure/client-hardware.png)

VM 117 bruger `vmbr0` uden ekstra VLAN-tag. Billedet er fra opsætningen, hvor Desktop-ISO'en stadig var tilsluttet.

![Klientens IPv4-konfiguration](docs/screenshots/infrastructure/client-ip.png)

`ip -4 -br address` viser klientens adresse: `10.5.70.169/24` på `ens18`.

### Docker på Ubuntu Server

Docker blev installeret fra Ubuntus pakkearkiv. Følgende kommandoer blev brugt fra administratorprompten:

```bash
apt update
apt install -y docker.io docker-compose-v2 docker-buildx git curl openssl
systemctl enable --now docker
docker run --rm hello-world
docker compose version
```

Ved kørsel fra en almindelig bruger sættes `sudo` foran kommandoer, der kræver administratoradgang.

![Docker hello-world og Compose-version](docs/screenshots/infrastructure/docker-test.png)

`Hello from Docker!` viser, at Docker kunne hente og køre en container. Billedet viser også Compose-version `2.40.3+ds1-0ubuntu1`.

### Hent og start platformen

Kilden blev hentet fra det private GitHub-repo. GitHub CLI's browserlogin blev brugt, så et langt token ikke skulle indtastes i Proxmox-konsollen.

```bash
apt install -y gh
gh auth login
git clone https://github.com/R0botninj4/Kvist-Bjaelke-Part1.git
cd Kvist-Bjaelke-Part1
sh scripts/setup.sh
nano .env
```

I `.env` blev denne linje sat:

```text
WEB_BIND_ADDRESS=10.5.71.33
```

De genererede værdier for JWT-nøgle og admin-password blev bevaret. Admin-login findes kun i serverens lokale `.env`. Passwords, tokens og indholdet af `.env` skal ikke med i dokumentationen.

```bash
docker compose up -d --build
docker compose ps
curl http://10.5.71.33:8000/health
```

Der kører en web-container og en API-container. Web-containeren lytter på serverens TCP-port **8000**, som er inden for opgavens interval 8000–8100. Den sender API-kald videre internt til C#-containeren på port 4000. API'et har ingen separat port ud på skolens netværk. SQLite-databasen gemmes i Docker-volumen `api-data`.

Adressen til platformen er **http://10.5.71.33:8000**. `/health` er kun en kontrol af, om API'et svarer; den tester ikke login eller CRUD.

![Kørende containere og HTTP-test på serveren](docs/screenshots/infrastructure/server-health.png)

`docker compose ps` viser begge containere som `Up`. HTTP-kaldet til `/health` svarer `OK`, og kaldet til forsiden returnerer HTML. Testene er udført på serveren selv.

Ved opdatering køres `git pull` og `docker compose up -d --build`. `docker compose down` stopper platformen og bevarer databasen. `down -v` sletter volumen og må ikke bruges, hvis kontiene skal bevares.

### Routing og firewall

Klienten på VLAN 570 når serveren på VLAN 571 gennem gatewayen. Ifølge opgaven er TCP 8000–8100 allerede tilladt mellem netværkene. Platformen bruger port 8000. Vi har ikke ændret skolens firewall.

Den fysiske computer fik timeout fra skolens Wi-Fi. Derfor brugte vi klient-VM på 10.5.70.0/24 til at åbne platformen.

### Problemer og teststatus — 2.6

| Problem | Hvad blev gjort? | Resultat |
|---|---|---|
| Specialtegn blev ikke overført korrekt gennem automatiseret konsolinput | Kommandoerne blev indtastet manuelt | Docker og Compose blev installeret |
| `docker compose up -d -build` blev afvist | Rettet til `docker compose up -d --build` | Containerne blev bygget og startet |
| HTTPS mod serverens HTTP-port gav TLS-fejl | Serverens test blev ændret til `http://` | `/health` svarede `OK`, og forsiden returnerede HTML |
| Browseren på den fysiske Wi-Fi-computer fik timeout | Oprettet en separat Desktop-klient på `vmbr0` | Klienten fik `10.5.70.169/24` |
| Firefox i klienten viste `SSL_ERROR_RX_RECORD_TOO_LONG` | Adressen blev åbnet med `http://` | Login-siden vises i Firefox på klient-VM 117 |

![HTTPS-fejl i klientens Firefox](docs/screenshots/infrastructure/client-https-error.png)

Dette er et fejlsøgningsbillede. Serveren bruger HTTP i labopsætningen, mens browseren forsøgte en TLS-forbindelse.

![Login-siden åbnet fra klient-VM 117](docs/screenshots/infrastructure/client-login-page.png)

Firefox på klienten åbner `http://10.5.71.33:8000`. Billedet viser adgang til login-siden fra VLAN 570 til VLAN 571, men ikke et gennemført login.

![Klientens IP, gateway og DNS](docs/screenshots/infrastructure/client-network.png)

Klientens netværksoversigt viser IP-adressen `10.5.70.169/24`, gateway `10.5.70.5` og DNS `10.5.70.5` på `ens18`.

Login, CRUD og rollebegrænsninger er testet automatisk som beskrevet under Part 1. I skolelabbet er Docker-driften, serverens HTTP-svar og adgang til login-siden fra klient-VM dokumenteret. En fuld manuel gennemgang af CRUD for hver rolle fra klient-VM er ikke udført.

Det valgfrie netværksdiagram i afsnit 2.5 er udeladt. Opgaven nævner dog også diagrammet under afleveringskravene; den modstrid er ikke afklaret.

## Afsluttende refleksion — Part 3

Docker virkede på serveren, mens browseren stadig gav fejl. Vi fik adgang fra klient-VM ved at bruge det rigtige netværk og HTTP i stedet for HTTPS.

Til rigtig drift ville vi tilføje HTTPS, en fast IP eller DHCP-reservation og backup af databasen. Login, CRUD og roller er testet automatisk. Billederne fra labbet viser Docker og adgang fra klienten.

### Brug af AI

Vi brugte AI som vejledning, når vi stødte på problemer eller havde brug for at få noget forklaret. AI hjalp os med at forstå fejl og viste trin for trin, hvordan vi kunne gå videre.

Vi fik også vejledning af AI til at sætte serveren op i en virtuel maskine (VM), fordi vi ikke havde prøvet den type serveropsætning før. Vi fulgte vejledningen og satte selv serveren op.

Et konkret eksempel var, da Firefox viste fejlen `SSL_ERROR_RX_RECORD_TOO_LONG`. Vi bad AI om at forklare fejlen og hjælpe os med at finde årsagen. Vi prøvede derefter selv at åbne adressen med `http://`, og så kunne vi se login-siden fra klient-VM’en.
