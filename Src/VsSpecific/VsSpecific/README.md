# VS Specific

VsVim supports multiple versions of Visual Studio.	The majority of the code is inside assemblies 
that load in all supported versions of Visual Studio. That means it must uses the minimum API 
surface of all the versions which can be limiting at times.

The code in this project though is compiled separately into a distinct DLL for each supported 
version of Visual Studio using the reference assemblies for that version. That means it is not 
limited to the minimum surface area. This allows for VsVim to support features like async 
completion even though it's not available before VS2019.

At runtime the VS layer of VsVim will pick the `ISharedService` instance from the DLL which 
matches up with the current version of Visual Studio. That is the interface is then used to get 
all of the other VS specific services to use in VsVim.

There are a couple of important points to remember when authoring code here.

## Type load will fail on wrong VS versions
The types being used in this layer fall into one of the following categories:

1. Specific to a Visual Studio version: these are types which come from non-versioned assemblies.
Hence they will only be avaiable in the exact instance of Visual Studio they're compiled for.
1. Have a minimum Visual Studio version: these are types like `IAsyncCompletionBroker`. They 
appear in versioned assemblies but they first appeared in a version above the minimum 
Visual Studio version that VsVim supports.

The type load failures are okay. They only happen VsSpecific DLLs that are not the target DLL for
the current version. Hence we never use their `ISharedService` instance anyways.

The important behavior though is to make sure those type loads don't result in unhandeld 
exceptions. Doing so will cause an "Extension threw an Exception" dialog to be shown to customers.

## MEF composition happens on every DLL
Every VsSpecific DLL will be a part of the MEF composition process. This means even though we only 
use the target VsSpecific DLL in VsVim every `Export` for every VsSpecific will be a part of the 
composition. Hence there are a few rules we need to keep in mind here when using MEF at this 
layer.

1. **Do not** have an `[Export(typeof(T))]` for types `T` defined in VsSpecific. Doing so means 
there would be an multiple exports for a single fully qualified name (FQN) but having different 
assembly qualified names (AQN). MEF does not support this and will silently error when this happens.
1. **Do not** have `[Export(typeof(T))]` for types `T` defined in other assemblies when the consuming
code uses `[Import(typeof(T))]`. Each VsSpecific DLL will export an instance of `T` and hence this
will break the cardinality of the `[Import]`.
1. **Do** have `[Export(typeof(T))]` for types `T` defined in other assemblies when the consuming
code uses `[ImportMany]`. The implementation though must be careful to disable functionality when 
it is loaded in the wrong VS version. For instance `ICompletionProvider` should only return instances
when VsSpecific is loading in the correct Visual Studio version.

When stuck on MEF issues inside of Visual Studio take a look at the composition error file as it will
usually have a detailed explanation of the error: 

- Open `%LOCALAPPDATA%\Microsoft\VisualStudio`
- Open the directory matching `16.*Exp`
- Open `ComponentModelCache\Microsoft.VisualStudio.Default.err` 








