### Welcome to VsVim
All code in this project is covered under the Apache 2 license a copy of which is available in the same directory under the name License.txt.

AppVeyor Status: [![Build status](https://ci.appveyor.com/api/projects/status/gf5rlu19syrja9lr)](https://ci.appveyor.com/project/jaredpar/vsvim)

### Building

1. Install the Visual Studio SDK 
2. Open the Solution VsVim.sln
3. Build

VsVim.sln will work with any version of Visual Studio since 2010.  The SKU must be professional or higher due to the use of VSIX projects.  

### Branching Structure

 - master: Stable branch 
 - staging: Used for releasing new versions
 - fixes*: Both short and long term fixes
 - dead*: Branches which will never integrate with master again.  

