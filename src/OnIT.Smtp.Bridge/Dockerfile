# OnIT-SMTP bridge (Part 2): builds and runs the cross-platform relay host. Build from the
# repository root, e.g.:
#   docker build -f src/OnIT.Smtp.Bridge/Dockerfile -t onit-smtp-bridge .
#
# Seed /config first -- copy config.json + secret.key produced by the Windows config tool's
# Docker Export tab (or straight from %ProgramData%\OnIT-SMTP, since PortableSecretProtector's
# key file is not machine-bound) into a mounted volume at /config before starting the container.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props .
COPY src/OnIT.Smtp.Core/ src/OnIT.Smtp.Core/
COPY src/OnIT.Smtp.Bridge/ src/OnIT.Smtp.Bridge/

# A single restore+publish (rather than a separate --no-restore publish layer) sidesteps a
# restore/publish inconsistency seen in CI where a transitive package NuGet had just restored
# was reported missing again at publish time.
RUN dotnet publish src/OnIT.Smtp.Bridge/OnIT.Smtp.Bridge.csproj -c Release -o /app

# The ASP.NET Core shared runtime (rather than the plain dotnet/runtime image) because the
# optional remote config/status API needs it -- see RemoteApiSettings; with that API disabled
# nothing web-specific actually runs, but the assemblies still need to be present.
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .

ENV ONITSMTP_HOME=/config
VOLUME ["/config"]

# Default SMTP listener port -- override in config.json's SmtpListener section if changed.
EXPOSE 25

# Only reachable if RemoteApiSettings.Enabled is turned on in config.json (off by default) --
# override in config.json's RemoteApi section if a different port is configured.
EXPOSE 8443

ENTRYPOINT ["dotnet", "OnIT.Smtp.Bridge.dll"]
