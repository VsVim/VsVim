using System;
using Microsoft.VisualStudio.Text;

VimBuffer.KeyIntercept = true;
VimBuffer.KeyInputIntercept += OnKeyInputIntercept;
VimBuffer.Closed += OnBufferClosed;

private void OnKeyInputIntercept(object sender, KeyInputEventArgs e)
{
    if (e.KeyInput.Char == 'k')
    {
        DTE.ToolWindows.SolutionExplorer.SelectUp(vsUISelectionType.vsUISelectionTypeSelect, 1);
    }
    else if (e.KeyInput.Char == 'j')
    {
        DTE.ToolWindows.SolutionExplorer.SelectDown(vsUISelectionType.vsUISelectionTypeSelect, 1);
    }
    else if (e.KeyInput.Key == VimKey.Enter)
    {
        InterceptEnd();
        DTE.ToolWindows.SolutionExplorer.DoDefaultAction();
        return;
    }
    else
    {
        InterceptEnd();
        return;
    }
}
private void InterceptEnd()
{
    VimBuffer.KeyInputIntercept -= OnKeyInputIntercept;
    VimBuffer.Closed -= OnBufferClosed;
    VimBuffer.KeyIntercept = false;
}
private void OnBufferClosed(object sender, EventArgs e)
{
    InterceptEnd();
}
