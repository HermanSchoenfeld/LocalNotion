Local Notion for Windows
========================

Extract the entire archive. Keep localnotion.exe, native libraries, and all
supporting files together. A separate .NET installation is not required.

Portable use
------------
From PowerShell in the extracted directory:

    .\localnotion.exe --version
    .\localnotion.exe --help

Install for your Windows user
----------------------------
From the extracted directory:

    .\install.bat

The installer copies the complete archive into
%LOCALAPPDATA%\Programs\LocalNotion\<version>-build.<number> and prepends that
directory to your user PATH. Open a new terminal before running localnotion.
Administrator access is not required. Earlier version directories are retained.
An existing directory for the same version and build is left untouched.

To choose the installation root and leave PATH unchanged:

    .\install.bat -InstallRoot "D:\Apps\LocalNotion" -NoPath

You can also run .\install.ps1 directly in Windows PowerShell 5.1 or later.
These scripts are unsigned and must be permitted by your PowerShell execution
policy. The installer does not change that policy or the machine PATH.

License and documentation
-------------------------
Local Notion is open source under the GNU General Public License. See LICENSE
and COPYRIGHT in this archive. Release metadata is in localnotion-release.json.

Source and releases: https://github.com/Sphere10/LocalNotion
Getting started: https://sphere10.com/products/localnotion/getting-started
