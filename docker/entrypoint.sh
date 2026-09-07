#!/bin/sh
set -eu

# .NET needs this directory before resolving LocalApplicationData.
mkdir -p "$HOME/.local/share"

# docker-sync provides first-run setup; other arguments go to the CLI.
if [ "${1:-}" = "docker-sync" ]; then
    shift
    if [ ! -f /repo/.localnotion/registry.json ]; then
        printf '%s\n' 'Official Local Notion Docker: waiting for setup.' \
            'Save your Notion integration token with docker/localnotion.ps1 -Action Configure.'
        trap 'exit 0' INT TERM
        while [ ! -s "${NOTION_API_KEY_FILE:-/run/secrets/notion-token}" ]; do
            sleep 5 &
            wait $! || true
        done
        /usr/local/bin/localnotion init --path /repo
        trap - INT TERM
    fi
    printf '%s\n' 'Official Local Notion Docker: starting synchronization.'
    exec /usr/local/bin/localnotion sync --path /repo "$@"
fi

exec /usr/local/bin/localnotion "$@"
