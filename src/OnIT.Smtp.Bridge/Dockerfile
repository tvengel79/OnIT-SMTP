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
COPY src/OnIT.Smtp.Core/OnIT.Smtp.Core.csproj src/OnIT.Smtp.Core/
COPY src/OnIT.Smtp.Bridge/OnIT.Smtp.Bridge.csproj src/OnIT.Smtp.Bridge/
RUN dotnet restore src/OnIT.Smtp.Bridge/OnIT.Smtp.Bridge.csproj

COPY src/OnIT.Smtp.Core/ src/OnIT.Smtp.Core/
COPY src/OnIT.Smtp.Bridge/ src/OnIT.Smtp.Bridge/
RUN dotnet publish src/OnIT.Smtp.Bridge/OnIT.Smtp.Bridge.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .

ENV ONITSMTP_HOME=/config
VOLUME ["/config"]

# Default SMTP listener port -- override in config.json's SmtpListener section if changed.
EXPOSE 25

ENTRYPOINT ["dotnet", "OnIT.Smtp.Bridge.dll"]
