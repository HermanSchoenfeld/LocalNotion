Local Notion for Linux and macOS
================================

Extract the entire archive for your operating system and processor. Keep
localnotion, native libraries, and all supporting files together. A separate
.NET installation is not required. Operating system libraries are still needed.

Portable use
------------
From a terminal in the extracted directory:

    ./localnotion --version
    ./localnotion --help

Install for your user
---------------------
From the extracted directory:

    sh ./install.sh

The installer copies the complete archive into
$HOME/.local/share/localnotion/<version>-build.<number> and creates the command
$HOME/.local/bin/localnotion. Add $HOME/.local/bin to PATH in your shell
configuration if it is not already present. No sudo access is needed.

To use a different prefix:

    sh ./install.sh --prefix "$HOME/tools"

This stores versions under $HOME/tools/share/localnotion and creates the command
$HOME/tools/bin/localnotion. Add that bin directory to PATH if needed.

Earlier versions are retained. A symlink created by a previous installation in
the same prefix can be updated. Other existing commands and existing version
directories are left untouched. The installer does not edit shell configuration.

macOS builds are unsigned and are not notarized by Apple. macOS security settings
may prevent execution. Follow your organization's policy for unsigned software.

License and documentation
-------------------------
Local Notion is open source under the GNU General Public License. See LICENSE
and COPYRIGHT in this archive. Release metadata is in localnotion-release.json.

Source and releases: https://github.com/Sphere10/LocalNotion
Getting started: https://sphere10.com/products/localnotion/getting-started
