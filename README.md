# OnIT-SMTP

SMTP bridge for sending local e-mails through Microsoft Graph.

**Part 1** (this repo, in progress) is a Windows Service + WPF configuration tool that
relays plain SMTP from LAN devices/apps to Microsoft Graph `sendMail`. **Part 2** (planned)
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
                          service install/start/stop, and Docker config export.
tests/
  OnIT.Smtp.Core.Tests/  xUnit tests for the IP allow list and MIME parsing/relay logic.
deploy/                  PowerShell scripts to publish and install/uninstall the service.
```

## Building

Requires the **.NET 8 SDK** and, since the Service and ConfigTool projects target
`net8.0-windows` (Windows Service hosting APIs, WPF), a **Windows** machine or CI runner.
`OnIT.Smtp.Core` itself targets plain `net8.0` and builds anywhere.

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

## Logging

- Rolling daily file log under `%ProgramData%\OnIT-SMTP\logs` (configurable), with an
  **Informational** / **Detailed** verbosity switch.
- A live view streams over a local named pipe to the config tool's *Logging* tab whenever
  it's open, seeded with recent history so you don't start from a blank screen.

## Secret protection

The Entra app's client secret is encrypted at rest with AES-256-GCM
(`PortableSecretProtector`), using a key file (`%ProgramData%\OnIT-SMTP\secret.key`) rather
than an OS/machine-bound mechanism like Windows DPAPI. That's deliberate: copy the whole
`%ProgramData%\OnIT-SMTP` folder (config.json **and** secret.key together) to another
machine -- a restore, a migration, seeding the Part 2 container -- and the secret decrypts
there with no re-entry and no redoing Entra admin consent. The key file's NTFS ACLs (best
effort, restricted to Administrators/SYSTEM) are the actual protection boundary, not machine
identity. Copying config.json *without* secret.key leaves the secret as unusable ciphertext.

## Docker export (Part 2 prep)

The *Docker Export* tab writes the current Entra app, allowed senders, IP allow rules, and
SMTP settings as JSON, optionally with the client secret decrypted to plain text -- handy
when you'd rather hand the container a single self-contained file/env var than also copy
`secret.key`. Since protection here isn't machine-bound, copying config.json + secret.key
directly to the container works too, without this export step at all.
