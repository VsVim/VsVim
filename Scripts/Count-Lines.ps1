[string]$script:rootPath = split-path -parent $MyInvocation.MyCommand.Definition 
[string]$script:rootPath = resolve-path (join-path $rootPath "..")
$sourceDirs = "VimCore","VimWpf","VsVim"
$testDirs = "VimUnitTestUtils","VimCoreTest","VimWpfTest","VsVimTest"

function count-fsharp() {
    param ([string]$fileName = $(throw "Need a file name"))

    $lines = gc $fileName |
        ?{ $_.Trim() -ne "" } |
        ?{ -not ($_ -match "^\s*//") }
    $lines.Count
}

function count-csharp() {
    param ([string]$fileName = $(throw "Need a file name"))

    $lines = gc $fileName |
        ?{ $_.Trim() -ne "" } |
        ?{ -not ($_ -match "^\s*//") }
    $lines.Count
}

function count-file() {
    param ( [string]$fileName = $(throw "Need a file name"),
            $data) 

    $ext = [IO.Path]::GetExtension($fileName)
    if ( ".cs" -eq $ext ) {
        $data.Lines += count-csharp $fileName 
        $data.Language = "C#"
    }
    else { 
        $data.Lines += count-fsharp $fileName
        $data.Language = "F#"
    }
    $data.Files++
}

function count-dirs() {
    param( [string]$path = $(throw "Need a path"),
           [bool]$isTest)

    foreach ($dirName in gci $path | ?{ $_.PSIsContainer }) { 
        $dirPath = join-path $path $dirName
        $data = @{ Name = $dirName; Language = ""; Lines = 0; Files = 0 };
        write-host "$dir"
        foreach ($file in gci $dirPath -re -in *.cs,*.fs,*.fsi) { 
            count-file $file $data
        }

        write-output $data
    }
}

$all = @()
$all += count-dirs (join-path . "Src") $false
$all += count-dirs (join-path . "Test") $true

foreach ($data in $all) { 
    write-host $data.Name
    write-host "  Source Files: $($data.Files)"
    write-host "  Source Lines: $($data.Lines)"
}

popd
