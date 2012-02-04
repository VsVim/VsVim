$script:scriptPath = split-path -parent $MyInvocation.MyCommand.Definition 

function Get-ProgramFiles32() {
    if ( test-path (join-path $env:WinDir "SysWow64") ) {
        return ${env:ProgramFiles(x86)}
    }
    
    return $env:ProgramFiles
}

$idePath = join-path (Get-ProgramFiles32) "Microsoft Visual Studio 10.0\Common7\IDE"
$vsixInstaller = join-path $idePath "vsixInstaller.exe"
$devenv = join-path $idePath "devenv.exe"

if (-not (test-path $vsixInstaller)) {
    write-error "Couldn't find $vsixInstaller"
    return
}

gps devenv -ErrorAction SilentlyContinue | kill
& $vsixInstaller /quiet /uninstall:VsVim.Microsoft.e214908b-0458-4ae2-a583-4310f29687c3

$target = resolve-path ~\Dogfood
mkdir $target -ErrorAction SilentlyContinue
pushd $scriptPath
copy VsVim\bin\Debug\* $target
popd
& $vsixInstaller /quiet (join-path $target "VsVim.vsix")
wait-process "vsixInstaller"

# Even though we waited for the installer to finish devenv doesn't
# always seem to pick up the change immediately.  Wait a sec for
# it all to catch up
sleep 2
& $devenv
