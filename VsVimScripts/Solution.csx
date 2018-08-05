using System;
using Microsoft.VisualStudio.Text;

VimBuffer.KeyIntercept = true;
VimBuffer.KeyInputIntercept += OnKeyInputIntercept;
VimBuffer.Closed += OnBufferClosed;

private void OnKeyInputIntercept(object sender, KeyInputEventArgs e)
{
    UIHierarchy solutionExplorer =DTE.ToolWindows.SolutionExplorer;
    if (e.KeyInput.Char == 'k')
    {
        solutionExplorer.SelectUp(vsUISelectionType.vsUISelectionTypeSelect, 1);
    }
    else if (e.KeyInput.Char == 'j')
    {
        solutionExplorer.SelectDown(vsUISelectionType.vsUISelectionTypeSelect, 1);
    }
    else if (e.KeyInput.Key == VimKey.Enter)
    {
        var selectedItems = solutionExplorer.SelectedItems as UIHierarchyItem[];
        if (selectedItems == null)
        {
            InterceptEnd();
        }
        solutionExplorer.DoDefaultAction();
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
