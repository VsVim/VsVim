[string]$script:rootPath = split-path -parent $MyInvocation.MyCommand.Definition 
[string]$script:rootPath = resolve-path (join-path $rootPath "..")
$sourceDirs = "VimCore","VimWpf","VsVim"
$testDirs = "VimUnitTestUtils","VimCoreTest","VimWpfTest","VsVimTest"

function countFSharp() {
    param ([string]$fileName = $(throw "Need a file name"))

    $lines = gc $fileName |
        ?{ $_.Trim() -ne "" } |
        ?{ -not ($_ -match "^\s*//") }
    $lines.Count
}

function countCSharp() {
    param ([string]$fileName = $(throw "Need a file name"))

    $lines = gc $fileName |
        ?{ $_.Trim() -ne "" } |
        ?{ -not ($_ -match "^\s*//") }
    $lines.Count
}

pushd $rootPath

$fsSourceFileCount = 0
$fsTestFileCount = 0
$fsSourceLines = 0
$fsTestLines = 0
$csSourceFileCount = 0
$csTestFileCount = 0
$csSourceLines = 0
$csTestLines = 0

function countFile() {
    param ( [string]$fileName = $(throw "Need a file name"),
            [bool]$isTest )

    $ext = [IO.Path]::GetExtension($fileName)
    if ( ".cs" -eq $ext ) {
        $lines = countCSharp $fileName 
        if ( $isTest ) {
            $script:csTestFileCount++ 
            $script:csTestLines += $lines
        } else {
            $script:csSourceFileCount++
            $script:csSourceLines += $lines
        }
    } else {
        $lines = countFSharp $fileName 
        if ( $isTest ) {
            $script:fsTestFileCount++ 
            $script:fsTestLines += $lines
        } else {
            $script:fsSourceFileCount++
            $script:fsSourceLines += $lines
        }
    }
}

foreach ( $dir in $sourceDirs ) {
    foreach ( $file in gci $dir -re -in  *.cs,*.fs,*.fsi ) {
        countFile $file $false
    }
}

foreach ( $dir in $testDirs ) {
    foreach ( $file in gci $dir -re -in  *.cs,*.fs,*.fsi ) {
        countFile $file $true
    }
}

write-host "F# Stats"
write-host "  Source Files $fsSourceFileCount"
write-host "  Source Lines $fsSourceLines"
write-host "  Test Files $fsTestFileCount"
write-host "  Test Lines $fsTestLines"
write-host ""

write-host "C# Stats"
write-host "  Source Files $csSourceFileCount"
write-host "  Source Lines $csSourceLines"
write-host "  Test Files $csTestFileCount"
write-host "  Test Lines $csTestLines"

popd
