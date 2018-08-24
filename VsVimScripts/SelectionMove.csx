using System;
using Microsoft.VisualStudio.Text;

int lineCount = 0;
int startLine = 0;
int endLine = 0;

if (TextView.Selection.IsEmpty)
    return;

ITextSelection selection = TextView.Selection;
VirtualSnapshotPoint start = selection.Start;
VirtualSnapshotPoint end = selection.End;

startLine = start.Position.GetContainingLine().LineNumber;
endLine = end.Position.GetContainingLine().LineNumber;
lineCount = Math.Abs(endLine - startLine);

VimBuffer.KeyIntercept = true;
VimBuffer.KeyInputIntercept += OnKeyInputIntercept;
VimBuffer.Closed += OnBufferClosed;

private void OnKeyInputIntercept(object sender, KeyInputEventArgs e)
{
    long lineNumber;

    if (e.KeyInput.Char == 'k')
    {
        lineNumber = TextView.Selection.Start.Position.GetContainingLine().LineNumber;
        if (lineNumber == 0)
        {
            return;
        }
        Process(KeyInputUtil.EscapeKey);
        Process(":", enter: false);
        Process("'<,'>move '<-2", enter: true);
        Process(KeyInputUtil.EscapeKey);

        TextView.Selection.Clear();

        Process("0", enter: false);
        Process("V", enter: false);

        if (1 < lineCount)
        {
            Process((lineCount - 1).ToString() + "j", enter: false);
        }
    }
    else if (e.KeyInput.Char == 'j')
    {
        lineNumber = TextView.Selection.End.Position.GetContainingLine().LineNumber;
        if (lineNumber == (TextView.TextSnapshot.LineCount - 1))
        {
            return;
        }

        Process(KeyInputUtil.EscapeKey);
        Process(":", enter: false);
        Process("'<,'>move '>+1", enter: true);
        Process(KeyInputUtil.EscapeKey);

        TextView.Selection.Clear();

        Process("0", enter: false);
        Process("V", enter: false);
        if (1 < lineCount)
        {
           Process((lineCount - 1).ToString() + "j", enter: false);
        }
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
