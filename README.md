# OnIT-SMTP

SMTP bridge for sending local e-mails through Microsoft Graph.

**Part 1** (this repo) is a Windows Service + WPF configuration tool that relays plain SMTP
from LAN devices/apps to Microsoft Graph `sendMail`. **Part 2** (this repo, `OnIT.Smtp.Bridge`)
is a Docker container -- seeded from the same config export -- that does the same job on
non-Windows hosts (e.g. a NAS).

## Solution layout

```
OnIT-SMTP.sln
src/
  OnIT.Smtp.Core/        Shared library: config model + storage, Entra app management,
                          Graph mail sending, SMTP protocol/MIME engine, logging, IPC.
                          Deliberately kept on a plain net8.0 TFM (no *-windows) so Part 2
                          can reuse it as-is on Linux.
  OnIT.Smtp.Service/     The Windows Service host (Worker Service). Owns the SMTP listener,
                          applies the allowed-senders policy, relays via Graph, and exposes
                          a local named-pipe control channel for the config tool.
  OnIT.Smtp.ConfigTool/  WPF admin app: create/delete the Entra app registration, manage
                          allowed senders (picked from Entra), the IP allow list, SMTP
                          listener settings, logging (with a live log viewer), test-send,
                          service install/start/stop, Docker config export, and pushing
                          config to / reading status from a paired Part 2 bridge remotely.
  OnIT.Smtp.Bridge/       Part 2: the cross-platform relay host (plain net8.0, runs in
                          Docker on Linux/NAS). Same SMTP listener + Graph relay logic as
                          OnIT.Smtp.Service, minus the Windows-only named-pipe control
                          channel. Seeded from a config directory copied or exported from
                          Part 1, or by the config tool pushing to its optional (off by
                          default) HTTPS remote API. Includes its own Dockerfile.
tests/
  OnIT.Smtp.Core.Tests/  xUnit tests for the IP allow list and MIME parsing/relay logic.
deploy/                  PowerShell scripts to publish and install/uninstall the service.
```

## Building

Requires the **.NET 8 SDK** and, since the Service and ConfigTool projects target
`net8.0-windows` (Windows Service hosting APIs, WPF), a **Windows** machine or CI runner to
build the whole solution. `OnIT.Smtp.Core` and `OnIT.Smtp.Bridge` target plain `net8.0` and
build and run anywhere, including Linux -- CI's `build-linux` job builds and tests both there
directly, and also builds the bridge's Docker image, so Part 2 is verified cross-platform on
every push.

```powershell
dotnet restore OnIT-SMTP.sln
dotnet build OnIT-SMTP.sln -c Release
dotnet test tests/OnIT.Smtp.Core.Tests/OnIT.Smtp.Core.Tests.csproj
```

CI (`.github/workflows/build.yml`) builds and tests the whole solution on `windows-latest`
on every push -- this repository was authored in a Linux sandbox without a .NET SDK
available, so treat the first green CI run as the first real compiler check of this code.

## Publishing / installing

```powershell
# Publish self-contained win-x64 builds of both apps:
.\deploy\publish.ps1

# Install the service from a published build:
.\deploy\Install-Service.ps1 -ServiceExePath .\publish\Service\OnIT.Smtp.Service.exe

# Uninstall:
.\deploy\Uninstall-Service.ps1            # keeps %ProgramData%\OnIT-SMTP
.\deploy\Uninstall-Service.ps1 -RemoveConfig
```

The config tool's **Service Status** tab can also install/uninstall/start/stop the service
directly (it must run elevated -- see `app.manifest` -- since it shares
`%ProgramData%\OnIT-SMTP` with the service and controls the SCM).

## Entra setup: no pre-registration needed

Nothing needs to be registered in advance, by anyone, before this works -- there is no
OnIT-owned app, multi-tenant or otherwise, involved anywhere in the process. Everything
happens inside the target customer's own tenant:

1. On the **Entra App** tab, enter the customer's tenant ID or domain and click **Create
   app**. The operator signs in interactively (browser popup or device code).
2. That one-time sign-in uses Microsoft's own first-party **Microsoft Graph PowerShell**
   application (present in every tenant already, the same way `Connect-MgGraph` works out
   of the box) purely to authenticate -- it requests delegated `Application.ReadWrite.All`,
   `AppRoleAssignment.ReadWrite.All`, `Directory.Read.All`, and `User.Read.All`. Because
   those are high-privilege scopes, the signed-in account needs to be a Global/Application
   Administrator in that tenant to consent (a one-time click, first sign-in only).
3. Once signed in, the config tool creates the actual relay app registration and activates
   the `Mail.Send` application permission on it, then grants that permission's admin consent
   -- all within that same tenant, and repeated independently for every customer.

### Granting the Mail.Send admin consent

Two ways to complete that last step, both available on the Entra App tab:

- **Automatically** (checked by default) -- the tool tries to grant consent directly via the
  Graph API using the signed-in account's own rights. Works out of the box for a Global
  Administrator in most tenants, but can fail depending on tenant policy (Conditional
  Access, restricted admin units, etc.), since it needs Graph write access the sign-in
  session may not have.
- **Browser consent** (always available, and the fallback if the automatic attempt fails) --
  click **Grant consent in browser**, which opens the standard Microsoft admin-consent page.
  Sign in (or already be signed in) as a Global or Application Administrator and click
  Accept -- that's the entire flow, no Graph permissions on the sign-in session required.
  Click **Check again** afterwards to confirm. Uncheck "Try to grant admin consent
  automatically first" before creating the app to skip straight to this path.

If an operator would rather their own branding show on that one-time sign-in screen instead
of Microsoft's, they can register a single-tenant app themselves in the customer's tenant
(Entra portal > App registrations > New > "Accounts in this organizational directory only",
a **Mobile and desktop applications** platform with redirect URI `http://localhost`, and the
same four delegated permissions above) and paste its client ID into the "Sign-in app" field
under **Advanced** on the Entra App tab. Either way, sign-in and everything it does afterward
stays entirely inside that one tenant.

## How relaying works

1. A LAN device/app sends plain SMTP to the service's listener (`SmtpListener` settings).
2. The connecting IP is checked against the configured IP allow list (single IP / range /
   CIDR) -- unlisted clients are refused before any SMTP banner is sent.
3. If **Restrict relaying to allowed senders** is on, `MAIL FROM` must match one of the
   mailboxes picked on the *Allowed Senders* tab (populated from Entra, so it always lines
   up with real mailboxes).
4. The DATA payload is parsed (headers, subject, text/HTML body, attachments -- including
   multipart messages such as scan-to-email PDFs) and sent via
   `POST /users/{from}/sendMail` using the app-only (client credentials) Entra app.

The listener being open and reachable from other machines on the LAN also depends on Windows
Firewall. The **SMTP Listener** tab checks for a dedicated inbound-allow rule on the
configured port and offers an **Add firewall rule** button when one's missing, mismatched, or
disabled -- it only ever manages that one rule (named "OnIT-SMTP SMTP Listener"), never
touches anything else in the firewall.

## Logging

- Rolling daily file log under `%ProgramData%\OnIT-SMTP\logs` (configurable), with an
  **Informational** / **Detailed** verbosity switch.
- A live view streams over a local named pipe to the config tool's *Logging* tab whenever
  it's open, seeded with recent history so you don't start from a blank screen.

## Client secret expiry

The Entra App tab shows the client secret's expiry date and a color-coded days-remaining
indicator. The service checks it hourly and emails the configured recipients as it gets
close -- at 30, 15, 7, and 3 days out, and again if it actually expires -- so it doesn't
silently break mail relaying. Renewal is manual by design (see the "Auto-renew" discussion
earlier in this project): click **Renew secret now** on the Entra App tab, which issues a
fresh secret and removes the old one via the same delegated sign-in used to create the app.

## Secret protection

The Entra app's client secret is encrypted at rest with AES-256-GCM
(`PortableSecretProtector`), using a key file (`%ProgramData%\OnIT-SMTP\secret.key`) rather
than an OS/machine-bound mechanism like Windows DPAPI. That's deliberate: copy the whole
`%ProgramData%\OnIT-SMTP` folder (config.json **and** secret.key together) to another
machine -- a restore, a migration, seeding the Part 2 container -- and the secret decrypts
there with no re-entry and no redoing Entra admin consent. The key file's NTFS ACLs (best
effort, restricted to Administrators/SYSTEM) are the actual protection boundary, not machine
identity. Copying config.json *without* secret.key leaves the secret as unusable ciphertext.

## Part 2: the Docker/Linux bridge

`OnIT.Smtp.Bridge` is the same SMTP-listener-to-Graph-relay logic as the Windows Service,
packaged to run headless in a container on a non-Windows host (e.g. a NAS). It has no local
WPF config tool to talk to, so there's no named-pipe control channel -- instead it reads
`config.json` from a mounted volume, watches it for changes, and logs to both a rolling file
and stdout (`docker logs`). Configuration is entirely edited on the Part 1 side (the config
tool, or by hand) and copied or mounted in; the bridge itself has no UI.

### Seeding the config directory

Because `PortableSecretProtector`'s key file isn't machine-bound (see below), the simplest
path is to copy the Part 1 config directory as-is:

1. On the machine running the config tool, copy `%ProgramData%\OnIT-SMTP\config.json` and
   `%ProgramData%\OnIT-SMTP\secret.key` into a directory that will become the container's
   mounted volume (e.g. `./onit-smtp-config` on the NAS).
2. Alternatively, use the config tool's **Docker Export** tab to write a self-contained
   `config.json`, optionally with the client secret already decrypted to plain text (so you
   don't need to also transfer `secret.key`) -- useful when you'd rather hand the container a
   single file or inject the secret via its own secrets mechanism.

Either way, the bridge reads whichever `config.json` (and, if present, `secret.key`) it finds
under the directory pointed at by `ONITSMTP_HOME`.

### Running the container

```bash
# Build (from the repository root):
docker build -f src/OnIT.Smtp.Bridge/Dockerfile -t onit-smtp-bridge .

# Run, mounting the seeded config directory at /config (the image's default ONITSMTP_HOME):
docker run -d --name onit-smtp-bridge \
  -p 25:25 \
  -v ./onit-smtp-config:/config \
  onit-smtp-bridge
```

Or with Compose:

```yaml
services:
  onit-smtp-bridge:
    build:
      context: .
      dockerfile: src/OnIT.Smtp.Bridge/Dockerfile
    restart: unless-stopped
    ports:
      - "25:25"
    volumes:
      - ./onit-smtp-config:/config
```

Editing `config.json` in the mounted volume while the container runs (e.g. to update the IP
allow list) is picked up automatically -- the bridge watches the file the same way the
Windows Service does, restarting only the SMTP listener if the bind address/port changed.

The client secret expiry notification worker runs in the bridge too, so a Part 2-only
deployment still gets the 30/15/7/3/0-day warning emails; renewal itself still happens from
the Part 1 config tool (it's the one with the delegated Entra sign-in), after which the
refreshed `config.json` just needs to reach the mounted volume again.

### Remote config push (alternative to copying files)

Instead of copying `config.json`/`secret.key` onto the bridge's host by hand every time
something changes, the bridge can expose an HTTPS API the config tool pushes to directly and
reads live status from -- the **Remote Bridge** tab. It's off by default: turning it on adds a
network-facing endpoint, so it's an explicit opt-in per bridge, set in that bridge's own
`config.json`:

```json
"RemoteApi": { "Enabled": true, "Port": 8443 }
```

On the next start with the API enabled, the bridge generates (once) a self-signed TLS
certificate and a random pairing token, and logs both:

```bash
docker logs onit-smtp-bridge
# Remote API certificate generated. SHA-256 fingerprint (pin this in the config tool): AA:BB:...
# Remote API pairing token generated -- shown once, copy it into the config tool now: 3F9C...
```

There's no CA involved (a self-signed cert has nowhere else to get trust from), so the config
tool doesn't validate a certificate chain -- it pins the exact fingerprint the operator enters,
the same trust model as an SSH host key. Copy that fingerprint and the token (shown only on
the boot where they're generated -- note them down) into the config tool's **Remote Bridge**
tab, along with the bridge's host and port, and click **Pair**. The pairing token is stored
encrypted at rest (same protector as the Entra client secret), never in plain text.

Once paired, **Push configuration** sends the current config to the bridge, which saves it and
reloads immediately (the same effect as replacing config.json on the mounted volume by hand).
The Entra client secret travels as plaintext in this one request -- safe, since the connection
is already authenticated and TLS-pinned -- rather than as the ciphertext config.json normally
stores it as: the config tool's and the bridge's `secret.key` files are independent, so
ciphertext made with one could never be decrypted by the other. The bridge re-encrypts the
secret locally with its own key before saving, so pushing a config never requires transferring
`secret.key` at all. **Refresh status** reads the same live status (listening state, message
counts, IP allow-list rejections) the local named-pipe IPC exposes to the Windows Service.
