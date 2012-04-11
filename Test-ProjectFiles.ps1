###############################################################################
#
# This script is used to validate projects don't inadvertently reference a 
# DLL which isn't legal for the set of Visual Studio versions they project
# must work in
#
###############################################################################
$script:scriptPath = split-path -parent $MyInvocation.MyCommand.Definition 
$script:projectFiles = @{
    "EditorUtils.csproj" = "all";
    "EditorUtilsTest.csproj" = "test";
    "VimCore.fsproj" = "all";
    "VimCoreTest.csproj" = "test";
    "VimWpf.csproj" = "all";
    "VimWpfTest.csproj" = "test";
    "VsVimShared.csproj" = "all";
    "VsVimSharedTest.csproj" = "test";
    "VsVim.csproj" = "all";
    "VsVim10.csproj" = "all";
    "VsVim11.csproj" = "all";
    "VsVimDev10.csproj" = "Dev10";
    "VsVimDev11.csproj" = "Dev11";
}

# Assemblies valid in any version.  These are all versioned assemblies and 
# must either be BCL types or types which are explicitly versioned in the 
# devenv.exe.config file
#
# Note: The Shell.XXX assemblies are all COM assemblies that aren't versioned
# but their COM so they don't change either.  Fine to reference from any 
# version after the one they were defined in
$script:listAll = @(
    "envdte=8.0.0.0",
    "envdte80=8.0.0.0",
    "envdte90=9.0.0.0",
    "envdte100=10.0.0.0",
    "FSharp.Core=4.0.0.0",
    "Microsoft.VisualStudio.CoreUtility=10.0.0.0",
    "Microsoft.VisualStudio.Editor=10.0.0.0",
    "Microsoft.VisualStudio.OLE.Interop=7.1.40304.0"
    "Microsoft.VisualStudio.Language.Intellisense=10.0.0.0",
    "Microsoft.VisualStudio.Language.StandardClassification=10.0.0.0",
    "Microsoft.VisualStudio.Shell=2.0.0.0",
    "Microsoft.VisualStudio.Shell.10.0=10.0.0.0",
    "Microsoft.VisualStudio.Shell.Immutable.10.0=10.0.0.0",
    "Microsoft.VisualStudio.Shell.Interop=7.1.40304.0",
    "Microsoft.VisualStudio.Shell.Interop.8.0=8.0.0.0",
    "Microsoft.VisualStudio.Shell.Interop.9.0=9.0.0.0",
    "Microsoft.VisualStudio.Shell.Interop.10.0=10.0.0.0",
    "Microsoft.VisualStudio.Text.Logic=10.0.0.0",
    "Microsoft.VisualStudio.Text.Data=10.0.0.0",
    "Microsoft.VisualStudio.Text.UI=10.0.0.0",
    "Microsoft.VisualStudio.Text.UI.Wpf=10.0.0.0",
    "Microsoft.VisualStudio.TextManager.Interop=7.1.40304.0",
    "Microsoft.VisualStudio.TextManager.Interop.8.0=8.0.0.0",
    "Microsoft.VisualStudio.Platform.VSEditor.Interop=10.0.0.0",
    "mscorlib=",
    "PresentationCore="
    "PresentationFramework="
    "System=",
    "System.ComponentModel.Composition=",
    "System.Core=",
    "System.Data="
    "System.Drawing="
    "System.Xml=",
    "System.Xaml=",
    "WindowsBase="
)

# Types specific to Dev10
$script:list10 = @(
    "Microsoft.VisualStudio.Platform.WindowManagement=10.0.0.0",
    "Microsoft.VisualStudio.Shell.ViewManager=10.0.0.0"
)
$script:list10 = $list10 + $listAll

# Types specific to Dev11
$script:list11 = @(
    "Microsoft.VisualStudio.Platform.WindowManagement=11.0.0.0",
    "Microsoft.VisualStudio.Shell.11.0=11.0.0.0",
    "Microsoft.VisualStudio.Shell.ViewManager=11.0.0.0"
)
$script:list11 = $list11 + $listAll

# Test assemblies are allowed to reference a couple of DLL's needed
# to host the editor but aren't legal / recomended for noraml 
# code.  These DLL's don't version and should be accessed instead
# through other interfaces
$script:listTest = @(
    "Microsoft.VisualStudio.Platform.VSEditor=10.0.0.0",
    "Moq=4.0.10827.0",
    "nunit.framework=2.6.0.12051"
)
$script:listTest = $listTest + $listAll + $list10

function Test-Reference() {
    param ([string]$dll = $(throw "Need a dll name to check"),
           $list = $(throw "Need a target list"))

    foreach ($validDll in $list) {
        if ($validDll -eq $dll) {
            return $true;
        }
    }

    return $false;
}

function Test-Include() {
    param ([string]$project = $(throw "Need a project string"),
           [string]$reference = $(throw "Need a reference string"),
           $list = $(throw "Need a target list"))
    if (-not ($reference -match "^[^,]*")) {
        write-error "Invalid reference: $reference"
    }

    $dll = $matches[0];
    $version = "";
    if ($reference -match "Version=([\d.]+)") {
        $version = $matches[1]
    }

    $dll = "{0}={1}" -f $dll, $version;
    if (-not (Test-Reference $dll $list)) {
        write-host "Invalid reference $dll in $project"
    }
}

function Test-ProjectFile() {
    param ([string]$path = $(throw "Need a project file path"),
           $list = $(throw "Need a target list"))

    $count = 0;
    $name = split-path -leaf $path
    write-host "Checking $name"
    foreach ($line in gc $path) {
        if ($line -match "<Reference Include=`"([^`"]*)`"") {
            Test-Include $name $matches[1] $list
            $count += 1
        }
    }

    if ($count -eq 0) {
        write-error "Couldn't find any references: $path"
    }
}

foreach ($projectFile in gci -re -in *proj) {
    $name = $projectFile.Name
    $fullName = $projectFile.FullName
    switch ($projectFiles[$name]) {
        "all" { Test-ProjectFile $fullName $listAll }
        "test" { Test-ProjectFile $fullName $listTest }
        "Dev10" { Test-ProjectFile $fullName $list10 }
        "Dev11" { Test-ProjectFile $fullName $list11 }
        default { write-error "Unrecognized file: $name" }
    }
}

