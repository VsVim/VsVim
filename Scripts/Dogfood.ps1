param ([string]$rootSuffix)

set-strictmode -version 2.0
$ErrorActionPreference="Stop"

try {
    [string]$rootPath= [IO.Path]::GetFullPath((join-path $PSScriptRoot ".."))

    & (join-path $PSScriptRoot "Download-NuGet.ps1")
    $nuget = join-path $rootPath "Binaries\Tools\NuGet.exe"
    $dogfoodDir = join-path $rootPath "Binaries\Dogfood"

    function build-project() {
        param ([string]$fileName = $(throw "Need a project file name"))
        $name = split-path -leaf $fileName
        write-host "Building $name"
        & msbuild /nologo /verbosity:m /p:Configuration=Debug /p:DeployExtension=false /p:VisualStudioVersion=14.0 $fileName
    }

    build-project (join-path $rootPath "Src\VsVim\VsVim.csproj")

    pushd $rootPath
    mkdir $dogfoodDir -ErrorAction SilentlyContinue | out-null
    rm -re -fo "$dogfoodDir\*"
    copy Binaries\Debug\VsVim\* $dogfoodDir

    write-host "Installing VsixUtil"
    & $nuget install VsixUtil -OutputDirectory $dogfoodDir -ExcludeVersion

    write-host "Installing VsVim"
    if ($rootSuffix -eq "") {
        & "$dogfoodDir\VsixUtil\tools\VsixUtil.exe" /install "$dogfoodDir\VsVim.vsix" 
    } else {
        & "$dogfoodDir\VsixUtil\tools\VsixUtil.exe" /rootSuffix $rootSuffix /install "$dogfoodDir\VsVim.vsix" 
    }

    popd
}
catch {
    write-host "Error: $($_.Exception.Message)"
    exit 1
}
