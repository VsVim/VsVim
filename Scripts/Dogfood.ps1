[string]$script:rootPath = split-path -parent $MyInvocation.MyCommand.Definition 
[string]$script:rootPath = resolve-path (join-path $rootPath "..")

$msbuild = join-path ${env:SystemRoot} "microsoft.net\framework\v4.0.30319\msbuild.exe"

function build-project()
{
    param ([string]$fileName = $(throw "Need a project file name"))
    $name = split-path -leaf $fileName
    write-host "Building $name"
    & $msbuild /nologo /verbosity:m /p:Configuration=Debug /p:VisualStudioVersion=12.0 $fileName
}

function test-return() {
    if ($LASTEXITCODE -ne 0) {
        write-error "Command failed with code $LASTEXITCODE"
    }
}

build-project (join-path $rootPath "Src\VsixUtil\VsixUtil.csproj")
build-project (join-path $rootPath "Src\VsVim\VsVim.csproj")

pushd $rootPath
mkdir "Dogfood" -ErrorAction SilentlyContinue | out-null
rm "Dogfood\*"
copy Src\VsVim\bin\Debug\* "Dogfood"
copy Src\VsixUtil\bin\Debug\* "Dogfood"

& ".\Dogfood\VsixUtil.exe" "Dogfood\VsVim.vsix"
popd
