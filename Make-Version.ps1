
function Compress-Folder {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$true)]
        [string]$FolderPath,
        
        [Parameter(Mandatory=$true)]
        [string]$ZipFilePath
    )
    
    # If the zip file already exists, delete it
    if (Test-Path $ZipFilePath) {
        Remove-Item $ZipFilePath
    }
    
    # Compress the folder contents into the zip file
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($FolderPath, $ZipFilePath)
}

function Copy-Attachments {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$true)]
        [string]$ReleasePath,

        [Parameter(Mandatory=$true)]
        [string]$AttachmentsPath
    )

    # Get all subfolders in the input folder
    $PlatformFolders = Get-ChildItem -Path $ReleasePath -Directory
    
    # Loop through each subfolder and copy attachments into then 
    foreach ($PlatformFolder in $PlatformFolders) {
        $PlatformBuildPath = Join-Path $ReleasePath ($PlatformFolder.Name)

        # Copy global attachments
        $AllAttachmentsPath = Join-Path $AttachmentsPath "all"
        Copy-Item -Path "$AllAttachmentsPath\*" -Destination $PlatformBuildPath -Recurse

        # Copy platform-specific attachments
        $PlatformAttachmentsPath = Join-Path $AttachmentsPath ($PlatformFolder.Name)
        if (Test-Path $PlatformAttachmentsPath -PathType Container) {
            Copy-Item -Path "$PlatformAttachmentsPath\*" -Destination $PlatformBuildPath -Recurse
        }
        
    }
}

function Compress-Subfolders {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$true)]
        [string]$FolderPath,

        [Parameter(Mandatory=$false)]
        [string]$ZipFilenamePrefix,        

        [Parameter(Mandatory=$false)]
        [string]$ZipFilenamePostfix

    )
    
    # Get all subfolders in the input folder
    $Subfolders = Get-ChildItem -Path $FolderPath -Directory
    
    # Loop through each subfolder and call Compress-Folder function
    foreach ($Subfolder in $Subfolders) {
        $PlatformBuildPath = Join-Path $FolderPath $Subfolder
        $ZipFilePath = Join-Path $FolderPath "$ZipFilenamePrefix$Subfolder$ZipFilenamePostfix.zip"
        Compress-Folder -FolderPath $PlatformBuildPath -ZipFilePath $ZipFilePath
    }
}


function Make-Version {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$true)]
        [string]$BuildPath,

        [Parameter(Mandatory=$true)]
        [string]$Version,

        [Parameter(Mandatory=$false)]
        [string]$ZipFilenamePrefix,        

        [Parameter(Mandatory=$false)]
        [string]$ZipFilenamePostfix        
    )

    if (!(Test-Path $BuildPath -PathType Container)) {
        Write-Output "Build path not found"
        exit
    }

    $CurrentPath = Join-Path -Path $BuildPath -ChildPath "/current"
    if (!(Test-Path $CurrentPath -PathType Container)) {
        Write-Output "Current build sub-folder not found"
        exit
    }
    
    $ReleasePath = Join-Path -Path $BuildPath -ChildPath "releases/$version"
    if (Test-Path $ReleasePath -PathType Container) {
        Write-Output "Version sub-folder already exists"
        exit
    }

    $AttachmentsPath = Join-Path -Path $BuildPath -ChildPath "attachments"
    
    # Copy current build to verion folder
    Copy-Item -Path $CurrentPath -Destination $ReleasePath -Recurse
    
    # Copy release attachments into the platform-build folders (readme, installation instructions, etc)
    if (Test-Path $AttachmentsPath -PathType Container) {
        Copy-Attachments -ReleasePath $ReleasePath -AttachmentsPath $AttachmentsPath
    }
    
    # Compress all the platform-build folders into a distributable zip file
    Compress-Subfolders -FolderPath $ReleasePath -ZipFilenamePrefix $ZipFilenamePrefix -ZipFilenamePostfix $ZipFilenamePostfix

}

$Version = Read-Host "Enter version number:"
$BuildPath = Convert-Path .
Make-Version -BuildPath $BuildPath -Version $Version -ZipFilenamePrefix "localnotion-" -ZipFilenamePostfix "-$Version"
Write-Output "Version $version has been packaged"