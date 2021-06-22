# Vim Editor Host

This is a library designed to make it easy to host `IVimBuffer` instances. It facilitates both the 
`VimApp` project as well as hosting in the unit tests

This is provided as a shared source project so that the editor behavior can be easily customized with
`#if` calls based on the specific editor we are creating

Logic and helpers here should be minimal. The goal is **only** for hosting the editor and vim

