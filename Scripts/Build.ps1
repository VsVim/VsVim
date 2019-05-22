param (
  [switch]$build = $false,
  [switch]$test = $false,

  # Whether or not  we're running in CI / Pipelines
  [switch]$ci = $false,
  [string]$config = "Release")

Set-StrictMode -version 2.0
$ErrorActionPreference="Stop"

[string]$rootDir = Split-Path -parent $MyInvocation.MyCommand.Definition 
[string]$rootDir = Resolve-Path (Join-Path $rootDir "..")
[string]$binariesDir = Join-Path $rootDir "Binaries"
[string]$configDir = Join-Path $binariesDir $config
[string]$deployDir = Join-Path $binariesDir "Deploy"
[string]$logsDir = Join-Path $binariesDir "Logs"
[string]$toolsDir = Join-Path $rootDir "Tools"

# Toggle between human readable messages and Azure Pipelines messages based on 
# our current environment.
# https://docs.microsoft.com/en-us/azure/devops/pipelines/scripts/logging-commands?view=azure-devops&tabs=powershell
function Write-PipelineError([string]$message) {
  if ($ci) {
    Write-Host "##vso[task.logissue type=error]$message"
  }
  else {
    Write-Host $message
  }
}


function Get-MSBuildPath() {
  $vsWhere = Join-Path $toolsDir "vswhere.exe"
  $vsInfo = Exec-Command $vsWhere "-latest -format json -requires Microsoft.Component.MSBuild" | ConvertFrom-Json

  # use first matching instance
  $vsInfo = $vsInfo[0]
  $vsInstallDir = $vsInfo.installationPath
  $vsMajorVersion = $vsInfo.installationVersion.Split('.')[0]
  $msbuildVersionDir = if ([int]$vsMajorVersion -lt 16) { "$vsMajorVersion.0" } else { "Current" }
  return Join-Path $vsInstallDir "MSBuild\$msbuildVersionDir\Bin\msbuild.exe"
}

# Test the contents of the Vsix to make sure it has all of the appropriate
# files 
function Test-VsixContents() { 
    Write-Host "Verifying the Vsix Contents"
    $vsixPath = Join-Path $deployDir "VsVim.vsix"
    if (-not (Test-Path $vsixPath)) {
        throw "Vsix doesn't exist"
    }

    $expectedFiles = @(
        "Colors.pkgdef",
        "extension.vsixmanifest",
        "License.txt",
        "Vim.Core.dll",
        "Vim.UI.Wpf.dll",
        "Vim.VisualStudio.Interfaces.dll",
        "Vim.VisualStudio.Shared.dll",
        "Vim.VisualStudio.Vs2015.dll",
        "Vim.VisualStudio.Vs2017.dll",
        "Vim.VisualStudio.Vs2019.dll",
        "VsVim.dll",
        "VsVim.pkgdef",
        "VsVim_large.png",
        "VsVim_small.png",
        "catalog.json",
        "manifest.json",
        "[Content_Types].xml")

    # Make a folder to hold the foundFiles
    $target = Join-Path ([IO.Path]::GetTempPath()) ([IO.Path]::GetRandomFileName())
    Create-Directory $target 
    $zipUtil = Join-Path $rootDir "Tools\7za920\7za.exe"
    Exec-Command $zipUtil "x -o$target $vsixPath" | Out-Null

    $foundFiles = Get-ChildItem $target | %{ $_.Name }
    if ($foundFiles.Count -ne $expectedFiles.Count) { 
        Write-Host "Found $($foundFiles.Count) but expected $($expectedFiles.Count)"
        Write-Host "Wrong number of foundFiles in VSIX." 
        Write-Host "Extra foundFiles"
        foreach ($file in $foundFiles) {
            if (-not $expectedFiles.Contains($file)) {
                Write-Host "`t$file"
            }
        }

        Write-Host "Missing foundFiles"
        foreach ($file in $expectedFiles) {
            if (-not $foundFiles.Contains($file)) {
                Write-Host "`t$file"
            }
        }

        Write-Host "Location: $target"
    }

    foreach ($item in $expectedFiles) {
        # Look for dummy foundFiles that made it into the VSIX instead of the 
        # actual DLL 
        $itemPath = Join-Path $target $item
        if ($item.EndsWith("dll") -and ((get-item $itemPath).Length -lt 5kb)) {
            throw "Small file detected $item in the zip file ($target)"
        }
    }
}

# Run all of the unit tests
function Test-UnitTests() { 
    Write-Host "Running unit tests"
    $resultsDir = Join-Path $binariesDir "xunitResults"
    Create-Directory $resultsDir

    $all = 
        "VimCoreTest\net472\Vim.Core.UnitTest.dll",
        "VimWpfTest\net472\Vim.UI.Wpf.UnitTest.dll",
        "VsVimSharedTest\net472\Vim.VisualStudio.Shared.UnitTest.dll"
    $xunit = Join-Path $rootDir "Tools\xunit.console.x86.exe"

    foreach ($filePath in $all) { 
        $filePath = Join-Path $configDir $filePath
        $logFilePath = Join-Path $resultsDir "$($filePath).xml"
        $arg = "$filePath -xml $logFilePath"
        Exec-Console $xunit $filePath
    }
}

# Make sure that the version number is the same in all locations.  
function Test-Version() {
    Write-Host "Testing Version Numbers"
    $version = $null;
    foreach ($line in Get-Content "Src\VimCore\Constants.fs") {
        if ($line -match 'let VersionNumber = "([\d.]*)"') {
            $version = $matches[1]
            break
        }
    }

    if ($version -eq $null) {
        throw "Couldn't determine the version from Constants.fs"
    }

    $foundPackageVersion = $false
    foreach ($line in Get-Content "Src\VsVim\VsVimPackage.cs") {
        if ($line -match 'productId: VimConstants.VersionNumber') {
            $foundPackageVersion = $true
            break
        }
    }

    if (-not $foundPackageVersion) {
        throw "Could not verify the version of VsVimPackage.cs"
    }

    $data = [xml](Get-Content "Src\VsVim\source.extension.vsixmanifest")
    $manifestVersion = $data.PackageManifest.Metadata.Identity.Version
    if ($manifestVersion -ne $version) { 
        throw "The version $version doesn't match up with the manifest version of $manifestVersion" 
    }
}

function Build-Vsix() {
    Create-Directory $deployDir
    Push-Location $deployDir 
    try { 
        Remove-Item -re -fo "$deployDir\*"
        $sourcePath = Join-Path $configDir "VsVim\net45\VsVim.vsix"
        Copy-Item $sourcePath "VsVim.orig.vsix"
        Copy-Item $sourcePath "VsVim.vsix"

        # Due to the way we build the VSIX there are many files included that we don't actually
        # want to deploy.  Here we will clear out those files and rebuild the VSIX without 
        # them
        $cleanUtil = Join-Path $configDir "CleanVsix\net472\CleanVsix.exe"
        Exec-Console $cleanUtil (Join-Path $deployDir "VsVim.vsix")
        Copy-Item "VsVim.vsix" "VsVim.zip"
    }
    finally {
        Pop-Location
    }
} 

function Build-Code(){ 
    Write-Host "Building VsVim.sln"
    $binlogFilePath = Join-Path $logsDir "msbuild.binlog"
    Exec-Console $msbuild "/nologo /restore /v:m /m /bl:$binlogFilePath /p:Configuration=$config VsVim.sln"
}

Push-Location $rootDir
try {
    . "Scripts\Common-Utils.ps1"

    $msbuild = Get-MSBuildPath
    Write-Host "Using MSBuild from $msbuild"

    if ($build) {
      Build-Code
      Build-Vsix
    }

    if ($test) {
      Test-UnitTests
    }
}
catch {
    Write-PipelineError "Error: $($_.Exception.Message)"
    Write-PipelineError $_.ScriptStackTrace
    exit 1
}
finally {
    Pop-Location
}


