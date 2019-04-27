C# Scripting
===

VsVim 2.7.0 adds the feature to run C# scripts.  
This feature is supported in Visual Studio 2017 and later.

## Execution method

Create the `VsVimScripts` folder in the user profile folder.  
Place the script file directly under it.  
The extension of the script file is `csx`.  

The command to execute the script is as follows.   

`csx <script file name>`  
`csxe <script file name>`  

When you enter a command, the file name does not need an extension.   

`csx` reuses compiled objects.  
You can use the static variable to hold information that was last executed.

`csxe` compiles every time the command is executed.  
It is assumed to be used for debugging.  

## First script

create the following script file.  

```csharp
//Hello.csx

using System.Windows;

MessageBox.Show("Hello, World!");

```
Place this file in the `VsVimScripts` folder.  
Type `csx hello` in command mode.  
If the message box is displayed, it is successful.  

This command is implemented using [Microsoft.CodeAnalysis.CSharp.Scripting](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp.Scripting/).   
For information on C# script and `Microsoft.CodeAnalysis.CSharp.Scripting` refer to the following URL.  

- [C# Scripting](https://msdn.microsoft.com/en-us/magazine/mt614271.aspx)
- [Scripting API Samples](https://github.com/dotnet/roslyn/wiki/Scripting-API-Samples)  

## Let's use Visual Studio SDK

This command sets the assembly folder of Visual Studio as a path for searching dll.  
Therefore, many Visual Studio SDK dll can be used.  

Execute the following script.

```csharp
//Sdk.csx

#r "EnvDTE.dll"
#r "EnvDTE80.dll"
#r "Microsoft.VisualStudio.Shell.11.0.dll"

using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

var DTE = Package.GetGlobalService(typeof(DTE)) as DTE2;
var textDoc = (TextDocument)DTE.ActiveDocument.Object("TextDocument");
var editPoint = (EditPoint)textDoc.StartPoint.CreateEditPoint();
editPoint.Insert("Hello, World!");

```

If "Hello, World!" Is written in the first line of the editor, it is successful.      
With the Visual Studio SDK, you can create a something of extensions.  
If you want to use the Visual Studio SDK, the following sentences will be helpful.  

- [Resources for writing Visual Studio Extensions](https://github.com/jaredpar/VsVim/wiki/Resources-for-writing-Visual-Studio-Extensions)

## Let's operate VsVim

C# script has the following global objects defined.  

- Name - Script name.
- Arguments - Argument when command is executed.
- LineRange - Selection information when command is executed.
- IsScriptLocal - Presence or absence of `<SID>` prefix.
- Vim - An object of type `IVim`.
- VimBuffer - An object of type `IVimBuffer`.

The most important of these is `Vim` and `VimBuffer`.  
You can use these objects to operate VsVim.   

`IVimBuffer` is the main interface of the Vim editor.  
And it is created each time the editor is opened.  
The VimBuffer defined here is the Buffer that executed the csx command.  
The following methods are defined in `IVimBuffer`.  

- SwitchMode - Switch modes.
- Process - Analyze and execute Vim's command.

Let's operate VsVim using these two methods.

The following script write "Hello, World!" 10 times.  

```csharp
//VsVim.csx
#r "Microsoft.VisualStudio.CoreUtility.dll"

using Vim;
using Vim.Extensions;

for (var count = 0; count < 10; count++)
{
    VimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
    VimBuffer.Process("j", enter: false);
    VimBuffer.Process("I", enter: false);
    VimBuffer.Process("Hello, World!", enter: true);
}
public static void Process(this IVimBuffer vimBuffer, string input, bool enter = false)
{
    foreach (var c in input)
    {
        var ki = KeyInputUtil.CharToKeyInput(c);
        vimBuffer.Process(ki);
    }

    if (enter)
    {
        vimBuffer.Process(KeyInputUtil.EnterKey);
    }
}
```

`IVimBuffer` has an event.  
By registering for the event, the script can realize something like a new mode.  
When you run the script below, VsVim switches to `scroll mode`.  
If you type `j` or `k`, the editor scrolls.   
When you type another key it returns to normal mode.  

```csharp
//Scroll.csx

#r "Microsoft.VisualStudio.CoreUtility.dll"
#r "Microsoft.VisualStudio.Text.UI.dll"
#r "Microsoft.VisualStudio.Text.UI.Wpf.dll"

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using Vim;
using Vim.Extensions;

IWpfTextView textView = VimBuffer.TextView as IWpfTextView;
if (textView == null)
{
    VimBuffer.VimBufferData.StatusUtil.OnError("Can not get WpfTextView");
    return;
}

VimBuffer.KeyInputStart += OnKeyInputStart;
VimBuffer.Closed += OnBufferClosed;

private void OnKeyInputStart(object sender, KeyInputStartEventArgs e)
{
    e.Handled = true;

    if (e.KeyInput.Char == 'j')
    {
        textView.ViewScroller.ScrollViewportVerticallyByPixels(-50);
    }
    else if (e.KeyInput.Char == 'k')
    {
        textView.ViewScroller.ScrollViewportVerticallyByPixels(50);
    }
    else
    {
        var count = textView.TextViewLines.Count;
        var index = count / 2;
        if (textView.TextViewLines.Count <= index)
        {
            index = 0;
        }
        var line = textView.TextViewLines[index];

        var lineNumber = line.Start.GetContainingLine().LineNumber;
        var snapshotLine = textView.TextSnapshot.GetLineFromLineNumber(lineNumber);
        var point = new SnapshotPoint(textView.TextSnapshot, snapshotLine.Start.Position);
        textView.Caret.MoveTo(new SnapshotPoint(textView.TextSnapshot, point));

        EndIntercept();
        return;
    }
}
private void EndIntercept()
{
    VimBuffer.KeyInputStart -= OnKeyInputStart;
    VimBuffer.Closed -= OnBufferClosed;
}
private void OnBufferClosed(object sender, EventArgs e)
{
    EndIntercept();
}
```

If you use the `Process` method in the `KeyInputStart` event,
Note the recursion of the event.  
If you use `Process`, you need to write as follows.  

```csharp

public void OnKeyInputStart(object sender, KeyInputStartEventArgs e)
{

    e.Handled = true;
    if (e.KeyInput.Char == 'j')
    {
        //Deleted an event temporarily.
        VimBuffer.KeyInputStart -= OnKeyInputStart;

        var ki = KeyInputUtil.CharToKeyInput('k');
        VimBuffer.Process(ki); 

        //Registered an event again.
        VimBuffer.KeyInputStart += OnKeyInputStart;
    }
    else if (e.KeyInput.Key == VimKey.Escape)
    {
        VimBuffer.KeyInputStart -= OnKeyInputStart;
    }
}
```

VsVim's API reference is currently not exist.  
Therefore, to use the object defined in VsVim, you need to read the code.  
However, by understanding some classes, interfaces, and methods, you should be able to do a variety of things.   
Reading the following code may be useful when writing a script.

- VimCore\CoreInterfaces.fs - IVim and IVimBuffer are defined here. 
- VimCore\Vim.fs - IVim is implemented here.
- VimCore\VimBuffer.fs - IVimBuffer is implemented here.  
- VimCoreTest\VimBufferTest.cs - VimBuffer test code is described.  
