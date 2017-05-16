param (
    [switch]$fast = $false, 
    [string]$vsDir = "C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise")

set-strictmode -version 2.0
$ErrorActionPreference="Stop"

[string]$script:rootPath = split-path -parent $MyInvocation.MyCommand.Definition 
[string]$script:rootPath = resolve-path (join-path $rootPath "..")
[string]$script:zip = join-path $rootPath "Tools\7za920\7za.exe"

# Check to see if the given version of Visual Studio is installed
function test-vsinstall() { 
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

function test-return() {
    if ($LASTEXITCODE -ne 0) {
        write-error "Command failed with code $LASTEXITCODE"
    }
}

# Test the contents of the Vsix to make sure it has all of the appropriate
# files 
function test-vsixcontents() { 
    $vsixPath = "Deploy\VsVim.vsix"
    if (-not (test-Path $vsixPath)) {
        write-error "Vsix doesn't exist"
    }

    $expectedFiles = @(
        "Colors.pkgdef",
        "EditorUtils2010.dll",
        "extension.vsixmanifest",
        "License.rtf",
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
    $target = join-path ([IO.Path]::GetTempPath()) ([IO.Path]::GetRandomFileName())
    mkdir $target | out-null
    & $zip x "-o$target" $vsixPath | out-null

    $foundFiles = gci $target | %{ $_.Name }
    if ($foundFiles.Count -ne $expectedFiles.Count) { 
        write-host "Found $($foundFiles.Count) but expected $(expectedFiles.Count)"
        write-host "Wrong number of foundFiles in VSIX." 
        write-host "Extra foundFiles"
        foreach ($file in $foundFiles) {
            if (-not $expectedFiles.Contains($file)) {
                write-host "`t$file"
            }
        }

        write-host "Missing foundFiles"
        foreach ($file in $expectedFiles) {
            if (-not $foundFiles.Contains($file)) {
                write-host "`t$file"
            }
        }

        write-host "Location: $target"
        write-error "Found $($foundFiles.Count) but expected $expected"
    }

    foreach ($item in $expectedFiles) {
        # Look for dummy foundFiles that made it into the VSIX instead of the 
        # actual DLL 
        $itemPath = join-path $target $item
        if ($item.EndsWith("dll") -and ((get-item $itemPath).Length -lt 5kb)) {
            write-error "Small file detected $item in the zip file ($target)"
        }

        # Make sure the telemetry key was properly deployed.
        $name = split-path -leaf $item
        if ($name -eq "telemetry.txt") {
            [string]$content = gc -raw $itemPath
            if ($content.Trim() -eq "") {
                write-error "Telemetry file is empty"
            }
        }
    }
}

# Run all of the unit tests
function test-unittests() { 
    $all = 
        "Binaries\Release\VimCoreTest\Vim.Core.UnitTest.dll",
        "Binaries\Release\VimWpfTest\Vim.UI.Wpf.UnitTest.dll",
        "Binaries\Release\VsVimSharedTest\Vim.VisualStudio.Shared.UnitTest.dll"
    $xunit = join-path $rootPath "Tools\xunit.console.clr4.x86.exe"

    foreach ($file in $all) { 
        $name = split-path -leaf $file
        write-host -NoNewLine ("`t{0}: " -f $name)
        $output = & $xunit $file /silent
        if ($LASTEXITCODE -ne 0) {
            write-host "& $xunit $file /silent"
            write-error "Command failed with code $LASTEXITCODE"
        }
        $last = $output[$output.Count - 1] 
        write-host $last
    }
}

# Make sure that the version number is the same in all locations.  
function test-version() {
    write-host "Testing Version Numbers"
    $version = $null;
    foreach ($line in gc "Src\VimCore\Constants.fs") {
        if ($line -match 'let VersionNumber = "([\d.]*)"') {
            $version = $matches[1]
            break
        }
    }

    if ($version -eq $null) {
        write-error "Couldn't determine the version from Constants.fs"
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
        write-error $msg
        return
    }

    $data = [xml](gc "Src\VsVim\source.extension.vsixmanifest")
    $manifestVersion = $data.PackageManifest.Metadata.Identity.Version
    if ($manifestVersion -ne $version) { 
        $msg = "The version {0} doesn't match up with the manifest version of {1}" -f $version, $manifestVersion
        write-error $msg
        return
    }
}

function build-clean() {
    param ([string]$fileName = $(throw "Need a project file name"))
    $name = split-path -leaf $fileName
    write-host "`t$name"
    & $msbuild /nologo /verbosity:m /t:Clean /p:Configuration=Release /p:VisualStudioVersion=$vsVersion $fileName
    Test-Return
    & $msbuild /nologo /verbosity:m /t:Clean /p:Configuration=Debug /p:VisualStudioVersion=$vsVersion $fileName
    Test-Return
}

function build-release() {
    param ([string]$fileName = $(throw "Need a project file name"))
    $name = split-path -leaf $fileName
    write-host "`t$name"
    & $msbuild /nologo /verbosity:q /p:Configuration=Release /p:VisualStudioVersion=$vsVersion $fileName
    Test-Return
}

# Due to the way we build the VSIX there are many files included that we don't actually
# want to deploy.  Here we will clear out those files and rebuild the VSIX without 
# them
function clean-vsixcontents() { 
    param ([string]$vsixPath = $(throw "Need the path to the VSIX")) 

    write-host "Cleaning VSIX contents"
    write-host "`tBuilding CleanVsix"
    build-release "Src\CleanVsix\CleanVsix.csproj"

    write-host "`tCleaning contents"
    & Binaries\Release\CleanVsix\CleanVsix.exe "$vsixPath"
}

function build-vsix() {
    if (-not (test-path Deploy)) {
        mkdir Deploy 2>&1 | out-null
    }
    rm Deploy\VsVim* 
    copy "Binaries\Release\VsVim\VsVim.vsix" "Deploy\VsVim.orig.vsix"
    copy "Deploy\VsVim.orig.vsix" "Deploy\VsVim.vsix"
    clean-vsixcontents (resolve-path "Deploy\VsVim.vsix")
    copy "Deploy\VsVim.vsix" "Deploy\VsVim.zip"
} 

pushd $rootPath
try {

    # TODO: Using a path to VS is lazy.  Should use the VS locate APIs to find the 
    # instance.  This lets us get back to a method of using vsVersion as the starting
    # point.
    $vsVersion = "15.0"
    if ($vsDir -eq "") { 
        write-host "Need a path to a Visual Studio 2017 installation."
        write-host "Example: C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise"
        write-error "Exiting"
        exit 1
    }

    $msbuild = join-path $vsDir "MSBuild\15.0\Bin\msbuild.exe"
    if (-not (test-path $msbuild)) {
        write-error "Can't find msbuild.exe"
        exit 1
    }

    write-host "Using MSBuild $msbuild"

    # Next step is to clean out all of the projects 
    if (-not $fast) { 
        write-host "Cleaning Projects"
        build-clean Src\VsSpecific\Vs2012\Vs2012.csproj
        build-clean Src\VsSpecific\Vs2013\Vs2013.csproj
        build-clean Src\VsSpecific\Vs2015\Vs2015.csproj
        build-clean Src\VsSpecific\Vs2017\Vs2017.csproj
        build-clean Src\VsVim\VsVim.csproj
    }

    # Before building make sure the version number is consistent in all 
    # locations
    test-version

    # Build all of the relevant projects.  Both the deployment binaries and the 
    # test infrastructure
    write-host "Building Projects"
    build-release Test\VimCoreTest\VimCoreTest.csproj
    build-release Test\VimWpfTest\VimWpfTest.csproj
    build-release Test\VsVimSharedTest\VsVimSharedTest.csproj

    # Now build the main output project
    build-release Src\VsVim\VsVim.csproj
    build-vsix

    write-host "Verifying the Vsix Contents"
    test-vsixcontents 

    if (-not $fast) {
        write-host "Running unit tests"
        test-unittests
    }
}
catch {
    write-host "Error: $($_.Exception.Message)"
    exit 1
}
finally {
    popd
}


