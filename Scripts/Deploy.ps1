param (
    [switch]$fast = $false, 
    [string]$vsDir = "C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise")

Set-StrictMode -version 2.0
$ErrorActionPreference="Stop"

[string]$rootDir = Split-Path -parent $MyInvocation.MyCommand.Definition 
[string]$rootDir = Resolve-Path (Join-Path $rootDir "..")
[string]$zip = Join-Path $rootDir "Tools\7za920\7za.exe"

# Check to see if the given version of Visual Studio is installed
function Test-VsInstall() { 
    param ([string]$version = $(throw "Need a version"))

    if ([IntPtr]::Size -eq 4) {
        $path = "hklm:\Software\Microsoft\VisualStudio\{0}" -f $version
    }
    else {
        $path = "hklm:\Software\Wow6432Node\Microsoft\VisualStudio\{0}" -f $version
    }
    $i = get-itemproperty $path InstallDir -ea SilentlyContinue | %{ $_.InstallDir }
    return $i -ne $null
}

# Test the contents of the Vsix to make sure it has all of the appropriate
# files 
function Test-VsixContents() { 
    $vsixPath = "Deploy\VsVim.vsix"
    if (-not (Test-Path $vsixPath)) {
        Write-Error "Vsix doesn't exist"
    }

    $expectedFiles = @(
        "Colors.pkgdef",
        "extension.vsixmanifest",
        "License.txt",
        "Microsoft.ApplicationInsights.dll",
        "telemetry.txt",
        "Vim.Core.dll",
        "Vim.UI.Wpf.dll",
        "Vim.VisualStudio.Interfaces.dll",
        "Vim.VisualStudio.Shared.dll",
        "Vim.VisualStudio.Vs2012.dll",
        "Vim.VisualStudio.Vs2013.dll",
        "Vim.VisualStudio.Vs2015.dll",
        "Vim.VisualStudio.Vs2017.dll",
        "VsVim.dll",
        "VsVim.pkgdef",
        "VsVim_large.png",
        "VsVim_small.png",
        "catalog.json",
        "manifest.json",
        "[Content_Types].xml")

    # Make a folder to hold the foundFiles
    $target = Join-Path ([IO.Path]::GetTempPath()) ([IO.Path]::GetRandomFileName())
    mkdir $target | Out-Null
    & $zip x "-o$target" $vsixPath | Out-Null

    $foundFiles = gci $target | %{ $_.Name }
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
            Write-Error "Small file detected $item in the zip file ($target)"
        }

        # Make sure the telemetry key was properly deployed.
        $name = Split-Path -leaf $item
        if ($name -eq "telemetry.txt") {
            [string]$content = gc -raw $itemPath
            if ($content.Trim() -eq "") {
                Write-Error "Telemetry file is empty"
            }
        }
    }
}

# Run all of the unit tests
function Test-UnitTests() { 
    $all = 
        "Binaries\Release\EditorUtilsTest\EditorUtils.UnitTest.dll",
        "Binaries\Release\VimCoreTest\Vim.Core.UnitTest.dll",
        "Binaries\Release\VimWpfTest\Vim.UI.Wpf.UnitTest.dll",
        "Binaries\Release\VsVimSharedTest\Vim.VisualStudio.Shared.UnitTest.dll"
    $xunit = Join-Path $rootDir "Tools\xunit.console.clr4.x86.exe"

    foreach ($file in $all) { 
        $name = Split-Path -leaf $file
        Write-Host -NoNewLine ("`t{0}: " -f $name)
        $output = & $xunit $file /silent
        if ($LASTEXITCODE -ne 0) {
            Write-Host "& $xunit $file /silent"
            Write-Error "Command failed with code $LASTEXITCODE"
        }
        $last = $output[$output.Count - 1] 
        Write-Host $last
    }
}

# Make sure that the version number is the same in all locations.  
function Test-Version() {
    Write-Host "Testing Version Numbers"
    $version = $null;
    foreach ($line in gc "Src\VimCore\Constants.fs") {
        if ($line -match 'let VersionNumber = "([\d.]*)"') {
            $version = $matches[1]
            break
        }
    }

    if ($version -eq $null) {
        Write-Error "Couldn't determine the version from Constants.fs"
        return
    }

    $foundPackageVersion = $false
    foreach ($line in gc "Src\VsVim\VsVimPackage.cs") {
        if ($line -match 'productId: VimConstants.VersionNumber') {
            $foundPackageVersion = $true
            break
        }
    }

    if (-not $foundPackageVersion) {
        $msg = "Could not verify the version of VsVimPackage.cs"
        Write-Error $msg
        return
    }

    $data = [xml](gc "Src\VsVim\source.extension.vsixmanifest")
    $manifestVersion = $data.PackageManifest.Metadata.Identity.Version
    if ($manifestVersion -ne $version) { 
        $msg = "The version {0} doesn't match up with the manifest version of {1}" -f $version, $manifestVersion
        Write-Error $msg
        return
    }
}

function Build-Clean() {
    param ([string]$fileName = $(throw "Need a project file name"))
    $name = Split-Path -leaf $fileName
    Write-Host "`t$name"
    Exec-Console $msbuild "/nologo /verbosity:m /t:Clean /p:Configuration=Release /p:VisualStudioVersion=$vsVersion $fileName"
    Exec-Console $msbuild "/nologo /verbosity:m /t:Clean /p:Configuration=Debug /p:VisualStudioVersion=$vsVersion $fileName"
}

function Build-Release() {
    param ([string]$fileName = $(throw "Need a project file name"))
    $name = Split-Path -leaf $fileName
    Write-Host "`t$name"
    Exec-Console $msbuild "/nologo /verbosity:q /p:Configuration=Release /p:VisualStudioVersion=$vsVersion $fileName"
}

# Due to the way we build the VSIX there are many files included that we don't actually
# want to deploy.  Here we will clear out those files and rebuild the VSIX without 
# them
function Clean-VsixContents() { 
    param ([string]$vsixPath = $(throw "Need the path to the VSIX")) 
    $cleanUtil = Join-Path $rootDir "Binaries\Release\CleanVsix\CleanVsix.exe"

    Write-Host "Cleaning VSIX contents"
    Write-Host "`tBuilding CleanVsix"
    Build-Release "Src\CleanVsix\CleanVsix.csproj"

    Write-Host "`tCleaning contents"
    Exec-Console $cleanUtil "$vsixPath"
}

function Build-Vsix() {
    Create-Directory "Deploy"
    Remove-Item -re -fo "Deploy\VsVim*" 
    Copy-Item "Binaries\Release\VsVim\VsVim.vsix" "Deploy\VsVim.orig.vsix"
    Copy-Item "Deploy\VsVim.orig.vsix" "Deploy\VsVim.vsix"
    Clean-VsixContents (Resolve-Path "Deploy\VsVim.vsix")
    Copy-item "Deploy\VsVim.vsix" "Deploy\VsVim.zip"
} 

pushd $rootDir
try {

    . "Scripts\Common-Utils.ps1"

    # TODO: Using a path to VS is lazy.  Should use the VS locate APIs to find the 
    # instance.  This lets us get back to a method of using vsVersion as the starting
    # point.
    $vsVersion = "15.0"
    if ($vsDir -eq "") { 
        Write-Host "Need a path to a Visual Studio 2017 installation."
        Write-Host "Example: C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise"
        Write-Error "Exiting"
        exit 1
    }

    $msbuild = Join-Path $vsDir "MSBuild\15.0\Bin\msbuild.exe"
    if (-not (Test-Path $msbuild)) {
        Write-Error "Can't find msbuild.exe"
        exit 1
    }

    Write-Host "Using MSBuild $msbuild"

    Write-Host "Cleaning Projects"
    Build-Clean Src\VsSpecific\Vs2012\Vs2012.csproj
    Build-Clean Src\VsSpecific\Vs2013\Vs2013.csproj
    Build-Clean Src\VsSpecific\Vs2015\Vs2015.csproj
    Build-Clean Src\VsSpecific\Vs2017\Vs2017.csproj
    Build-Clean Src\VsVim\VsVim.csproj

    # Before building make sure the version number is consistent in all 
    # locations
    Test-Version

    # Build all of the relevant projects.  Both the deployment binaries and the 
    # test infrastructure
    Write-Host "Building Projects"
    Build-Release Test\VimCoreTest\VimCoreTest.csproj
    Build-Release Test\VimWpfTest\VimWpfTest.csproj
    Build-Release Test\VsVimSharedTest\VsVimSharedTest.csproj

    # Now build the main output project
    Build-Release Src\VsVim\VsVim.csproj
    Build-Vsix

    Write-Host "Verifying the Vsix Contents"
    Test-VsixContents 

    if (-not $fast) {
        Write-Host "Running unit tests"
        Test-UnitTests
    }
}
catch {
    Write-Host "Error: $($_.Exception.Message)"
    Write-Host $_.ScriptStackTrace
    exit 1
}
finally {
    popd
}


