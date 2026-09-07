#!/bin/sh
# Run only in a disposable, network-disabled image container; see tests/README.md.
set -eu

fail() {
    printf '%s\n' "FAIL: $*" >&2
    exit 1
}

[ "${LOCALNOTION_GIT_OWNERSHIP_TEST:-}" = 1 ] ||
    fail "Set LOCALNOTION_GIT_OWNERSHIP_TEST=1 only in a disposable test container."
[ -f /.dockerenv ] || fail "This fixture is for disposable Docker containers only."
[ "$(id -u)" = 0 ] || fail "Start the disposable test container with --user root."
[ "$(id -u app)" = 1654 ] || fail "The image app account must have UID 1654."
[ -d /repo ] || fail "The image must provide /repo."
[ -z "$(find /repo -mindepth 1 -maxdepth 1 -print -quit)" ] ||
    fail "/repo must be empty; do not mount a real repository into this test."

for utility in git runuser mktemp cmp grep stat; do
    command -v "$utility" >/dev/null || fail "Missing utility: $utility"
done

fixture=$(mktemp -d /tmp/localnotion-git-ownership.XXXXXX)
chmod 755 "$fixture"
export HOME="$fixture/home"
mkdir -p "$HOME/.local/share"
chown -R app:app "$HOME"

# No real identity, credentials, network remote, or persistent home is used.
# Keep Git's normal ownership checks active; only LocalNotion may supply its
# own command-scoped exception for the repository explicitly selected by CLI.
unset GIT_CONFIG_PARAMETERS GIT_CONFIG_COUNT GIT_DIR GIT_WORK_TREE
export GIT_TERMINAL_PROMPT=0
git config --system --list >"$fixture/system-before" 2>&1 || true
runuser --preserve-environment -u app -- git config --global --list >"$fixture/global-before" 2>&1 || true

assert_untrusted() {
    repository=$1
    if runuser --preserve-environment -u app -- git -C "$repository" status --porcelain >"$fixture/untrusted.log" 2>&1; then
        fail "Standalone Git unexpectedly trusted $repository."
    fi
    grep -F 'detected dubious ownership' "$fixture/untrusted.log" >/dev/null ||
        { cat "$fixture/untrusted.log" >&2; fail "Expected Git's ownership protection for $repository."; }
}

run_cli() {
    log=$1
    shift
    if ! runuser --preserve-environment -u app -- /usr/local/bin/localnotion "$@" >"$log" 2>&1; then
        cat "$log" >&2
        fail "Local Notion command failed."
    fi
    # Git failures historically log an error while the CLI returns success.
    if grep -F '[Error]' "$log" >/dev/null; then
        cat "$log" >&2
        fail "Local Notion logged a Git error despite returning success."
    fi
}

exercise_repository() {
    repository=$1
    suffix=$2
    bare="$fixture/remote-$suffix.git"
    mkdir -p "$repository"
    chown root:app "$repository"
    git init --bare --initial-branch=main "$bare" >/dev/null
    chown -R app:app "$bare"
    git -C "$repository" init --initial-branch=main >/dev/null
    git -C "$repository" config user.name 'Local Notion regression'
    git -C "$repository" config user.email 'regression@example.invalid'
    git -C "$repository" config remote.origin.url "$bare"
    git -C "$repository" config branch.main.remote origin
    git -C "$repository" config branch.main.merge refs/heads/main
    git -C "$repository" config push.default simple

    # Simulate a Windows bind mount or a native repository owned by another
    # account: the app can write the files but is not their owner.
    chown -R root:app "$repository"
    chmod -R g+rwX "$repository"
    [ "$(stat -c %u "$repository")" = 0 ] || fail "Fixture repository owner is not root."
    [ "$(stat -c %u "$repository/.git")" = 0 ] || fail "Fixture Git directory owner is not root."
    assert_untrusted "$repository"

    # init and clean both use the same GitSentry / ProcessChangeControl path
    # as pull and sync, without contacting Notion.
    run_cli "$fixture/init-$suffix.log" init --path "$repository" --git --git-push
    first_commit=$(runuser --preserve-environment -u app -- git --git-dir="$bare" rev-parse refs/heads/main)
    runuser --preserve-environment -u app -- git --git-dir="$bare" cat-file -e 'refs/heads/main:.localnotion/registry.json' ||
        fail "Local Notion did not push the initialized registry."

    printf '%s\n' "local-only ownership regression: $suffix" >"$repository/ownership-regression.txt"
    run_cli "$fixture/clean-$suffix.log" clean --path "$repository"
    second_commit=$(runuser --preserve-environment -u app -- git --git-dir="$bare" rev-parse refs/heads/main)
    [ "$first_commit" != "$second_commit" ] || fail "clean did not push a new commit."
    runuser --preserve-environment -u app -- git --git-dir="$bare" show 'refs/heads/main:ownership-regression.txt' >"$fixture/pushed-$suffix"
    cmp "$repository/ownership-regression.txt" "$fixture/pushed-$suffix" ||
        fail "The pushed content differs from the local change."

    assert_untrusted "$repository"
    [ "$(stat -c %u "$repository")" = 0 ] || fail "Local Notion changed repository ownership."
    [ "$(stat -c %u "$repository/.git")" = 0 ] || fail "Local Notion changed Git directory ownership."
    printf '%s\n' "PASS: Local Notion initialized, committed, and pushed $repository; standalone Git still rejects its ownership."
}

exercise_repository /repo docker
exercise_repository "$fixture/repository with spaces & apostrophe's" native

git config --system --list >"$fixture/system-after" 2>&1 || true
runuser --preserve-environment -u app -- git config --global --list >"$fixture/global-after" 2>&1 || true
cmp "$fixture/system-before" "$fixture/system-after" || fail "The system Git configuration changed."
cmp "$fixture/global-before" "$fixture/global-after" || fail "The user's global Git configuration changed."
printf '%s\n' 'PASS: Global and system Git configuration remain unchanged.'
