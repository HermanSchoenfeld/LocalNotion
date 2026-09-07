# Official Local Notion Docker
# Build with: docker build --platform linux/amd64 -t local-notion .
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
ARG VERSION=dev
ARG BUILD_NUMBER=0
ARG VCS_REF=
WORKDIR /src
COPY Version.props Directory.Build.props Directory.Build.targets ./
COPY LocalNotion.CLI/LocalNotion.CLI.csproj LocalNotion.CLI/
COPY LocalNotion.Core/LocalNotion.Core.csproj LocalNotion.Core/
COPY Notion.Client/Notion.Client.csproj Notion.Client/
RUN dotnet restore LocalNotion.CLI/LocalNotion.CLI.csproj -r linux-x64
COPY LocalNotion.CLI/ LocalNotion.CLI/
COPY LocalNotion.Core/ LocalNotion.Core/
COPY Notion.Client/ Notion.Client/
# JSON polymorphism uses reflection, so trimming must remain disabled.
# Copy the complete publish output, including native dependencies.
RUN set --; \
    if [ -n "$VERSION" ] && [ "$VERSION" != dev ]; then set -- "$@" "-p:ReleaseVersion=$VERSION"; fi; \
    if [ -n "$VCS_REF" ] && [ "$VCS_REF" != unknown ]; then set -- "$@" "-p:SourceRevisionId=$VCS_REF"; fi; \
    dotnet publish LocalNotion.CLI/LocalNotion.CLI.csproj \
    -c Release -r linux-x64 --self-contained true --no-restore \
    -p:PublishSingleFile=true -p:PublishTrimmed=false -p:PublishReadyToRun=false \
    -p:DebugType=None -p:DebugSymbols=false "-p:BuildNumber=$BUILD_NUMBER" "$@" -o /out

FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-bookworm-slim
ARG VERSION=dev
ARG VCS_REF=unknown
ARG BUILD_NUMBER=0
LABEL org.opencontainers.image.title="Official Local Notion Docker" \
      org.opencontainers.image.description="The official Local Notion CLI for Notion backups, synchronization, and HTML export." \
      org.opencontainers.image.source="https://github.com/Sphere10/LocalNotion" \
      org.opencontainers.image.url="https://sphere10.com/products/localnotion" \
      com.sphere10.localnotion.portable-paths="1" \
      com.sphere10.localnotion.build-number="${BUILD_NUMBER}" \
      org.opencontainers.image.vendor="Sphere10" \
      org.opencontainers.image.licenses="GPL-3.0-or-later" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.revision="${VCS_REF}"
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates git openssh-client \
    && rm -rf /var/lib/apt/lists/*
ENV HOME=/var/lib/localnotion
# Container Git uses OpenSSH even when a Windows repository selects Plink.
ENV GIT_SSH_COMMAND=ssh GIT_SSH_VARIANT=ssh
# OpenSSH finds ~/.ssh from the account database, not just the HOME variable.
RUN usermod --home /var/lib/localnotion app
RUN mkdir -p /repo "$HOME/.local/share" \
    && chown -R app:app /repo "$HOME"
COPY --from=build /out/ /opt/localnotion/
COPY docker/entrypoint.sh /usr/local/bin/localnotion-entrypoint
RUN ln -s /opt/localnotion/localnotion /usr/local/bin/localnotion \
    && chmod 755 /usr/local/bin/localnotion-entrypoint
USER app
WORKDIR /repo
STOPSIGNAL SIGINT
ENTRYPOINT ["/usr/local/bin/localnotion-entrypoint"]
CMD ["--help"]
