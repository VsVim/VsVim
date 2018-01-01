VsVim Project Goals
===

As VsVim grows as a vim emulator it's important to outline the goals, and non-goals, of the project. This is meant to help customers when filing bugs or looking to add features / bug fixes to the project.

At it's core VsVim is meant to be a vim emulator, not a complete implementation of vim. To succeed it needs to support as much of the keyboard driven experience of vim as possible in the Visual Studio environment. Only deviating from the vim experience when there is a specific Visual Studio feature that supercedes the vim equivalent. For example: Intellisense, code formatting, etc ... 

This puts the emphasis of the effort on features like motions, macros, registers, modes, etc ... These are key to the core experience of vim and impact a significant number of users. There is less of an emphasis on features that have Visual Studio equivalents like tags, formatting and plugins. 

## Goals  and non-goals of the project:

- Goal: Motions
- Goal: Key Mappings
- Goal: Loading of vimrc files 
- Goal: Enough Vim Script support to load common, straight forward, vimrc files
- Non-goal: Full implementation of Vim Script
- Goal: Adding emulations for popular plugins 
- Non-goal: Loading vim plugins directly

## Vim Script / Plugins
There are occasional asks for plugins and / or extended vim script support in issues. Enough that I wanted to explain in a bit more detail why neither is a goal of the project. 

Vim script is a fully featured language and implementing it is a considerable under taking. This is further complicated because the language is highly integrated with the development environment. The use of buffers and windowing layout gives the language almost unlimited control on the visual experience of the editor. 

Many of the visual customizations it can do simply can't be easily emulated in Visual Studio. Windowing in particular, which is central to many plugins, is very limited. In Visual Studio a window can be split horizontally once over the same buffer. While in vim it can be split horizontally or vertically any number of times and each window can load a different buffer.

Taken together it would be a huge effort that ultimately wouldn't be useful in the way users want it to be. Running advantaged plugins natively just doesn't seem realistic. Instead the goal is to provide quality emulations of popular plugins within VsVim (using the standard extension points that exist today)
