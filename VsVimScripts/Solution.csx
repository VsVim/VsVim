using System;
using System.Linq;
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
        var selectedItem = selectedItems?.FirstOrDefault();
        if (selectedItem == null || 
            selectedItem.UIHierarchyItems == null || 
            (selectedItem.UIHierarchyItems.Count == 0 && (!(selectedItem.Object is EnvDTE.Project) && !(selectedItem.Object is EnvDTE.Solution))))
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
