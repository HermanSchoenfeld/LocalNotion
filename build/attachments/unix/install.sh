#!/bin/sh
set -eu

fail() {
    printf '%s\n' "$*" >&2
    exit 1
}

prefix=${HOME:?HOME is required}/.local
case $# in
    0) ;;
    2)
        [ "$1" = '--prefix' ] || fail 'Usage: sh install.sh [--prefix DIRECTORY]'
        [ -n "$2" ] || fail 'The prefix cannot be empty.'
        prefix=$2
        ;;
    *) fail 'Usage: sh install.sh [--prefix DIRECTORY]' ;;
esac
case $prefix in
    /*) ;;
    *) prefix=$(pwd)/$prefix ;;
esac

source_dir=$(CDPATH= cd -P "$(dirname "$0")" && pwd)
[ -f "$source_dir/localnotion" ] && [ -f "$source_dir/VERSION.txt" ] &&
    [ -f "$source_dir/localnotion-release.json" ] ||
    fail 'Extract the complete Unix release archive before running this installer.'
version=$(cat "$source_dir/VERSION.txt")
case $version in
    ''|[!A-Za-z0-9]*|*[!A-Za-z0-9._+-]*) fail 'VERSION.txt contains an invalid version identifier.' ;;
esac

mkdir -p "$prefix/share/localnotion" "$prefix/bin"
install_base=$(CDPATH= cd -P "$prefix/share/localnotion" && pwd)
bin_dir=$(CDPATH= cd -P "$prefix/bin" && pwd)
installation_path=$install_base/$version
case $installation_path in
    "$source_dir"/*) fail 'Choose an installation prefix outside the extracted archive directory.' ;;
esac
command_path=$bin_dir/localnotion

if [ -L "$command_path" ]; then
    current_target=$(readlink "$command_path")
    case $current_target in
        "$install_base/"*/localnotion)
            current_version=${current_target#"$install_base/"}
            current_version=${current_version%/localnotion}
            case $current_version in
                ''|[!A-Za-z0-9]*|*[!A-Za-z0-9._+-]*)
                    fail "Refusing to replace an unrelated symlink: $command_path" ;;
            esac
            [ -f "$install_base/$current_version/localnotion-release.json" ] ||
                fail "Refusing to replace an unmanaged symlink: $command_path"
            ;;
        *) fail "Refusing to replace an unrelated symlink: $command_path" ;;
    esac
elif [ -e "$command_path" ]; then
    fail "Refusing to replace an existing file or directory: $command_path"
fi

if [ -e "$installation_path" ] || [ -L "$installation_path" ]; then
    fail "The version directory already exists: $installation_path. Existing installations are preserved."
fi
mkdir "$installation_path"
# Copy the whole release so native libraries stay beside the executable.
cp -Rp "$source_dir/." "$installation_path/"
chmod u+x "$installation_path/localnotion" "$installation_path/install.sh"
ln -sfn "$installation_path/localnotion" "$command_path"

printf 'Installed Local Notion %s in %s\n' "$version" "$installation_path"
printf 'Command: %s\n' "$command_path"
case :${PATH-}: in
    *:"$bin_dir":*) ;;
    *) printf 'Add %s to PATH in your shell configuration to use localnotion by name.\n' "$bin_dir" ;;
esac
