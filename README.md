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

## One-time setup before this works: register the config tool's own Entra app

Creating *your* app registration is a delegated (signed-in-admin) Graph operation, which
means the config tool itself needs an Entra app registration to sign the operator in with.
That's a single, one-time registration done by whoever owns this deployment (not per
customer/tenant):

1. In the Azure/Entra portal, register a new app (e.g. "OnIT-SMTP Config Tool"),
   multi-tenant ("Accounts in any organizational directory").
2. Add a **Mobile and desktop applications** platform with redirect URI `http://localhost`.
3. Add these **delegated** Microsoft Graph permissions: `Application.ReadWrite.All`,
   `AppRoleAssignment.ReadWrite.All`, `Directory.Read.All`, `User.Read.All`.
4. Put its Application (client) ID into
   `src/OnIT.Smtp.Core/Entra/GraphWellKnown.cs` -> `ConfigToolClientId`.

Until that's filled in, use the "sign in with a device code" option together with an
explicit `ClientIdOverride` (see `EntraBootstrapOptions`) if you need to test against your
own tenant with your own app registration in the meantime.

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

## Docker export (Part 2 prep)

The *Docker Export* tab writes the current Entra app, allowed senders, IP allow rules, and
SMTP settings as JSON, with the client secret decrypted to plain text (the container can't
read this machine's DPAPI keys) -- transport that file securely to the container host.
