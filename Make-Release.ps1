$script:scriptPath = split-path -parent $MyInvocation.MyCommand.Definition 

$target = join-path $scriptPath Release
if ( test-path $target ) { 
    rm -re -fo $target;
}
mkdir $target | out-null;
copy -re VimCore $target
copy -re VsVim $target
pushd $target
gci -re -in .svn | rm -re -fo 
gci -re -in bin | rm -re -fo 
gci -re -in obj | rm -re -fo 
popd
