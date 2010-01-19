$script:scriptPath = split-path -parent $MyInvocation.MyCommand.Definition 
$refPath = join-path $scriptPath "References"
$coreDllList = @(   "Microsoft.VisualStudio.CoreUtility.dll",
                "Microsoft.VisualStudio.Editor.dll",
                "Microsoft.VisualStudio.Editor.Implementation.dll",
                "Microsoft.VisualStudio.Language.Intellisense.dll",
                "Microsoft.VisualStudio.Language.StandardClassification.dll",
                "Microsoft.VisualStudio.Platform.VSEditor.dll",
                "Microsoft.VisualStudio.Platform.VSEditor.Interop.dll",
                "Microsoft.VisualStudio.Text.Data.dll",
                "Microsoft.VisualStudio.Text.Logic.dll",
                "Microsoft.VisualStudio.Text.UI.dll",
                "Microsoft.VisualStudio.Text.UI.Wpf.dll",
                "Microsoft.VisualStudio.UI.Undo.dll" )

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

$fullPath = join-path $progPath "Microsoft Visual Studio 10.0\Common7\IDE\PrivateAssemblies\Microsoft.VisualStudio.Text.Internal.dll"
CopyTo-References $fullPath

$fullPath = join-path $progPath "Reference Assemblies\Microsoft\F#\1.0\Runtime\v4.0\FSharp.Core.dll"
CopyTo-References $fullPath


