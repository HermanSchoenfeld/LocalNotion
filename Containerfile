# Local Notion -- container image
#
# Build:
#   podman build -t localnotion --build-arg GIT_REV="$(git rev-parse HEAD)" .
#
# Run (the LocalNotion repository is mounted at /repo):
#   podman run --rm -v /path/to/repo:/repo:Z localnotion pull --all -k <token>

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .

# Explicit flags rather than /p:PublishProfile=linux-x64: that profile sets
# PublishTrimmed=true, which fails NETSDK1124 because Notion.Client targets
# netstandard2.0, and it hardcodes a Windows PublishDir.
#
# PublishTrimmed must stay false permanently -- the JSON layer resolves
# polymorphic types through reflection (JsonSubTypes), which trimming breaks.
RUN dotnet publish LocalNotion.CLI \
        -c Release \
        -r linux-x64 \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:PublishTrimmed=false \
        -o /out


FROM mcr.microsoft.com/dotnet/runtime-deps:8.0

ARG GIT_REV=unknown
LABEL org.opencontainers.image.source="https://github.com/komastudios/LocalNotion" \
      org.opencontainers.image.revision="${GIT_REV}"

# ca-certificates: the pull path reaches api.notion.com, www.notion.so (built-in
#   icon SVGs) and pre-signed S3 attachment URLs.
# git: GitSentry shells out to it when a repository enables change tracking.
# ICU is left as the base image provides it -- slug generation does
# culture-sensitive casing, so invariant globalization is not appropriate here.
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
        ca-certificates \
        git \
 && rm -rf /var/lib/apt/lists/*

# The Sphere10 usage layer writes $HOME/.local/share/Local Notion/ at startup.
# Setting HOME alone is not enough: .NET's GetFolderPath(LocalApplicationData)
# returns an empty string when the directory does not already exist, and the app
# then creates "Local Notion/" relative to the working directory -- which here is
# the mounted repository. Pre-creating $HOME/.local/share keeps that state in the
# ephemeral container layer and out of the volume. Verified both ways.
ENV HOME=/var/lib/localnotion
RUN mkdir -p "$HOME/.local/share"

COPY --from=build /out/localnotion /usr/local/bin/localnotion

# Mount point for the LocalNotion repository.
WORKDIR /repo

# No USER directive on purpose: under rootless podman the container root maps to
# the host service user, and a USER here would complicate volume ownership.
ENTRYPOINT ["/usr/local/bin/localnotion"]
