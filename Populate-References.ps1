$script:scriptPath = split-path -parent $MyInvocation.MyCommand.Definition 
$refPath = join-path $scriptPath "References"
$coreDllList = @(   "Microsoft.VisualStudio.CoreUtility.dll",
                "Microsoft.VisualStudio.Editor.dll",
                "Microsoft.VisualStudio.Editor.Implementation.dll",
                "Microsoft.VisualStudio.Language.Intellisense.dll",
                "Microsoft.VisualStudio.Language.StandardClassification.dll",
                "Microsoft.VisualStudio.Platform.VSEditor.dll",
                "Microsoft.VisualStudio.Text.Data.dll",
                "Microsoft.VisualStudio.Text.Logic.dll",
                "Microsoft.VisualStudio.Text.UI.dll",
                "Microsoft.VisualStudio.Text.UI.Wpf.dll" )

function Get-ProgramFiles32() {
    if ( test-path (join-path $env:WinDir "SysWow64") ) {
        return ${env:ProgramFiles(x86)}
    }
    
    return $env:ProgramFiles
}

function CopyTo-References() {
    param ( [string]$source = $(throw "Need a source") )
    if ( -not (test-path $source)) {
        write-error "Not found $source"
    }
    copy $source $refPath
}

$progPath = Get-ProgramFiles32
$vsPath = join-path $progPath "Microsoft Visual Studio 10.0\Common7\ide\CommonExtensions\microsoft\Editor"
foreach ( $dll in $coreDllList) {
    $fullPath = join-Path $vsPath $dll
    CopyTo-References $fullPath
}

$idePath= join-path $progPath "Microsoft Visual Studio 10.0\Common7\IDE"
CopyTo-References (join-path $idePath "Microsoft.VisualStudio.Shell.ViewManager.dll")

$privPath = join-path $progPath "Microsoft Visual Studio 10.0\Common7\IDE\PrivateAssemblies"
CopyTo-References (join-path $privPath "Microsoft.VisualStudio.Text.Internal.dll" )
CopyTo-References (join-path $privPath "Microsoft.VisualStudio.Platform.VSEditor.Interop.dll" )

$fullPath = join-path $progPath "Reference Assemblies\Microsoft\FSharp\2.0\Runtime\v4.0\FSharp.Core.dll"
CopyTo-References $fullPath


