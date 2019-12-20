# VsVim Release Checklist

The standard check list for authoring a release of VsVim

1. Ensure `VersionNumber` in [Constants.fs](https://github.com/VsVim/VsVim/blob/master/Src/VimCore/Constants.fs)
is the desired version.
1. Run `Build.cmd -test -testExtra -config Release` and verify everything passes
    1. This creates VsVim.vsix at `Binaries\Deploy\VsVim.vsix`
    1. Install the VSIX locally, restart Visual Studio and ensure everything is
    working as expected
1. Update [Release-Notes.md](https://github.com/VsVim/VsVim/blob/master/Documentation/release-notes.md)
with the features and issues from the milestone
    1. **Commit** the changes.
    1. Tag the commit with `VsVim-[version number]`. 
1. Increment `VersionNumber` in [Constants.fs](https://github.com/VsVim/VsVim/blob/master/Src/VimCore/Constants.fs)
and **commit** the changes.
1. Create a pull request on VsVim with the changes and merge when passing
1. Create a Release on GitHub
    1. Use the tag created above 
    1. Upload the VSIX for the tag
1. Upload the VSIX to the [Visual Studio Gallery](https://marketplace.visualstudio.com/items?itemName=JaredParMSFT.VsVim)

The last few steps can be done in different orders. For example if the PR is 
passing but lacking sign off to merge I will usually go ahead and upload the 
VSIX to the Visual Studio Gallery. The important part of the PR is to ensure 
it's passing so the changes are known to be good.


