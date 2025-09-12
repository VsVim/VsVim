VsVim
===
VsVim is a free vim emulator for Visual Studio 2019 through to 2022.

[![Build Status](https://github.com/VsVim/VsVim/actions/workflows/main.yml/badge.svg?branch=master)](https://github.com/VsVim/VsVim/actions/workflows/main.yml?branch=master)

## Features
- Emulates Vim key bindings and modes within Visual Studio.
- Supports Visual Studio versions 2019 and 2022.
- Provides seamless integration with Visual Studio editor.
- Includes extensive unit tests for reliability.
- Supports multiple Visual Studio versions with conditional compilation.

## Prerequisites
- Visual Studio 2022 (recommended) or Visual Studio 2019.
- .NET Desktop Development workload.
- F# Language support.
- Visual Studio Extension Development workload.

## Developing
VsVim can be developed using Visual Studio 2022. The details of the 
development process can be found in
[Developing.md](https://github.com/VsVim/VsVim/blob/master/Documentation/Developing.md)

When developing please follow the
[coding guidelines](https://github.com/VsVim/VsVim/blob/master/Documentation/CodingGuidelines.md)

## Building and Testing
- Use `Build.cmd -build` to build the solution.
- Use `Test.cmd` to run unit tests.
- For extra verification, use `Build.ps1 -testExtra`.
- Ensure all dependencies are restored before building.

## Contributing
Please refer to the [CONTRIBUTING.md](CONTRIBUTING.md) file for contribution guidelines.

## License

All code in this project is covered under the Apache 2 license a copy of which 
is available in the same directory under the name License.txt.

## Latest Builds

The build representing the latest source code can be downloaded from the
[Open Vsix Gallery](http://vsixgallery.com/extension/VsVim.Microsoft.e214908b-0458-4ae2-a583-4310f29687c3/).  

For Chinese Version: [中文版本](README.ch.md)
