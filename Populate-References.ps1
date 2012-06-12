$script:scriptPath = Split-Path -parent $MyInvocation.MyCommand.Definition 
$refPath = Join-Path $scriptPath "References"

function Get-ProgramFiles32() {
    if (Test-Path (Join-Path $env:WinDir "SysWow64") ) {
        return ${env:ProgramFiles(x86)}
    }
    
    return $env:ProgramFiles
}

function Ensure-Directory() {
    param ( [string]$path )

    if (-not (Test-Path $path)) {
        mkdir $path | Out-Null
    }
}

function Copy-Specific() {
    param ( [string]$basePath, [string]$destPath )

    Ensure-Directory $destPath
    $source = Join-Path $basePath "Microsoft.VisualStudio.Shell.ViewManager.dll"
    copy $source $destPath
}

# Copy the shared COM DLLs to the References folder.  These DLLs don't change between
# versions so they don't need a version specific sub-dir
function Copy-Shared() {
    param ( [string]$basePath )

    $source = Join-Path $basePath "PrivateAssemblies\Microsoft.VisualStudio.Platform.VSEditor.Interop.dll"
    copy $source $refPath
}

Ensure-Directory $refPath

$progPath = Get-ProgramFiles32
$dev10Path = Join-Path $progPath "Microsoft Visual Studio 10.0"
if (Test-Path $dev10Path) {
    Write-Host "Found Visual Studio 2010"

    $basePath = Join-Path $dev10Path "Common7\Ide"
    Copy-Shared $basePath
    Copy-Specific $basePath (Join-Path $refPath "Vs2010")
}

$dev11Path = Join-Path $progPath "Microsoft Visual Studio 11.0"
if (Test-Path $dev11Path) {
    Write-Host "Found Visual Studio 2012"

    $basePath = Join-Path $dev11Path "Common7\Ide"
    Copy-Specific $basePath (Join-Path $refPath "Vs2012")

    # If Dev10 isn't installed then use the COM references from Dev11
    if (-not (Test-Path $dev10Path)) {
        Copy-Shared $basePath
    }
}
