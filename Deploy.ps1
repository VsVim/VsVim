param ([switch]$fast = $false)
$script:scriptPath = split-path -parent $MyInvocation.MyCommand.Definition 
cd $scriptPath

$msbuild = join-path ${env:SystemRoot} "microsoft.net\framework\v4.0.30319\msbuild.exe"
if (-not (test-path $msbuild)) {
    write-error "Can't find msbuild.exe"
}

function test-return() {
    if ($LASTEXITCODE -ne 0) {
        write-error "Command failed with code $LASTEXITCODE"
    }
}

# Test the contents of the Vsix to make sure it has all of the appropriate
# files 
function test-vsixcontents() { 
    param ([string]$vsixPath = $(throw "Need the path to the VSIX")) 
    if (-not (test-Path $vsixPath)) {
        write-error "Vsix doesn't exist"
    }

    # Make a folder to hold the files
    $target = join-path ([IO.Path]::GetTempPath()) ([IO.Path]::GetRandomFileName())
    mkdir $target | out-null

    # Copy the VSIX to a file with a zip extension.  This is required for the 
    # shell to unzip it for us. 
    $vsixTarget = join-path $target "VsVim.zip"
    copy $vsixPath $vsixTarget

    # Unzip the file 
    $shellApp = new-object -com shell.application
    $source = $shellApp.NameSpace($vsixTarget)
    $dest = $shellApp.NameSpace($target)
    $items = $source.Items()
    $dest.Copyhere($items, 20)

    $files = gci $target | %{ $_.Name }
    if ($files.Count -ne 13) { 
        write-host "Wrong number of files in VSIX. Found ..."
        foreach ($file in $files) {
            write-host "`t$file"
        }
        write-host "Location: $target"
        write-error "Found $($files.Count) but expected 13"
    }

    # The set of important files that are easy to miss 
    #   - FSharp.Core.dll: We bind to the 4.0 version but 2012 only installs
    #     the 4.5 version that we can't bind to
    #   - EditorUtils.dll: Not a part of the build but necessary.  Make sure 
    #     that it's found        
    $expected = 
        "FSharp.Core.dll", 
        "EditorUtils.dll",
        "Vim.Core.dll", 
        "Vim.UI.Wpf.dll",
        "VsVim.Vs2010.dll",
        "VsVim.Vs2012.dll",
        "VsVim.dll",
        "VsVim.Shared.dll"

    foreach ($item in $expected) {
        if (-not ($files -contains $item)) { 
            write-error "Didn't found $item in the zip file ($target)"
        }
    }
}

# Run all of the unit tests
function test-unittests() { 
    $all = 
        "VimCoreTest\bin\Release\Vim.Core.UnitTest.dll",
        "VimWpfTest\bin\Release\Vim.UI.Wpf.UnitTest.dll",
        "VsVimSharedTest\bin\Release\VsVim.Shared.UnitTest.dll"
    $xunit = join-path $scriptPath "Tools\xunit.console.clr4.x86.exe"

    write-host "Running Unit Tests"
    foreach ($file in $all) { 
        $name = split-path -leaf $file
        write-host -NoNewLine ("`t{0}: " -f $name)
        $output = & $xunit $file /silent
        test-return
        $last = $output[$output.Count - 1] 
        write-host $last
    }
}

# Make sure that the version number is the same in all locations.  
function test-version() {
    write-host "Testing Version Numbers"
    $version = $null;
    foreach ($line in gc "VimCore\Constants.fs") {
        if ($line -match 'let VersionNumber = "([\d.]*)"') {
            $version = $matches[1]
            break
        }
    }

    if ($version -eq $null) {
        write-error "Couldn't determine the version from Constants.fs"
        return
    }

    $data = [xml](gc "VsVim\source.extension.vsixmanifest")
    $manifestVersion = $data.Vsix.Identifier.Version
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
    & $msbuild /nologo /verbosity:m /t:Clean /p:Configuration=Release $fileName
    Test-Return
    & $msbuild /nologo /verbosity:m /t:Clean /p:Configuration=Debug $fileName
    Test-Return
}

function build-release() {
    param ([string]$fileName = $(throw "Need a project file name"))
    $name = split-path -leaf $fileName
    write-host "`t$name"
    & $msbuild /nologo /verbosity:q /p:Configuration=Release $fileName
    Test-Return
}

function publish-vsix() {
    param ([string]$vsixPath = $(throw "Need the path to the VSIX")) 
    if (-not (test-path "Deploy")) {
        mkdir "Deploy" | out-null
    }
    $vsixName = split-path -leaf $vsixPath
    $target = join-path "Deploy" $vsixName
    copy -force $vsixPath $target
    ii "Deploy"
}

# First step is to clean out all of the projects 
if (-not $fast) { 
    write-host "Cleaning Projects"
    build-clean Vs2010\Vs2010.csproj
    build-clean Vs2012\Vs2012.csproj
    build-clean VsVim\VsVim.csproj

    if (test-path "VsVim\VsVim.Vs2012.dll") {
        rm VsVim\VsVim.Vs2012.dll
    }
}

# Before building make sure the version number is consistent in all 
# locations
test-version

# Build all of the relevant projects.  Both the deployment binaries and the 
# test infrastructure
write-host "Building Projects"
build-release VimCoreTest\VimCoreTest.csproj
build-release VimWpfTest\VimWpfTest.csproj
build-release VsVimSharedTest\VsVimSharedTest.csproj

# Next step is to build the 2012 components.  We need to get the 2012 specific DLL
# to deploy with the standard install
build-release Vs2012\Vs2012.csproj

# Copy the 2012 specfic components into the location expected by the build system
copy Vs2012\bin\Release\VsVim.Vs2012.dll VsVim

# Now build the final output project
build-release VsVim\VsVim.csproj

write-host "Verifying the Vsix Contents"
$vsixPath = "VsVim\bin\Release\VsVim.vsix"
test-vsixcontents $vsixPath
test-unittests
publish-vsix $vsixPath 
