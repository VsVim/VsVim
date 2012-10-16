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

# First step is to clean out all of the projects 
if (-not $fast) { 
    write-host "Cleaning Projects"
    build-clean VsVimSpecific\VsVimDev10.csproj
    build-clean VsVimSpecific\VsVimDev11.csproj
    build-clean VsVim\VsVim.csproj

    if (test-path "VsVim\VsVim.Dev11.dll") {
        rm VsVim\VsVim.Dev11.dll
    }
}

# Next step is to build the 2012 components.  We need to get the 2010 specific DLL
# to deploy with the standard install
write-host "Building Projects"
build-release VsVimSpecific\VsVimDev11.csproj

# Copy the 2012 specfic components into the location expected by the build system
copy VsVimSpecific\bin11\Release\VsVim.Dev11.dll VsVim

# Now build the final output project
build-release VsVim\VsVim.csproj

$vsixPath = "VsVim\bin\Release\VsVim.vsix"
test-vsixcontents $vsixPath

