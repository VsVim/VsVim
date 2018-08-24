using System;
using Microsoft.VisualStudio.Text;

VimBuffer.KeyIntercept = true;
VimBuffer.KeyInputIntercept += OnKeyInputIntercept;
VimBuffer.Closed += OnBufferClosed;

private void OnKeyInputIntercept(object sender, KeyInputEventArgs e)
{
    if (e.KeyInput.Char == 'j')
    {
        TextView.ViewScroller.ScrollViewportVerticallyByPixels(-10);
    }
    else if (e.KeyInput.Char == 'k')
    {
        TextView.ViewScroller.ScrollViewportVerticallyByPixels(10);
    }
    else if (e.KeyInput.Char == 'd')
    {
        TextView.ViewScroller.ScrollViewportVerticallyByPixels(-50);
    }
    else if (e.KeyInput.Char == 'u')
    {
        TextView.ViewScroller.ScrollViewportVerticallyByPixels(50);
    }
    else
    {
        var count = TextView.TextViewLines.Count;
        count = count / 2;
        var line = TextView.TextViewLines[count];

        var lineNumber = line.Start.GetContainingLine().LineNumber;
        var snapshotLine = TextView.TextSnapshot.GetLineFromLineNumber(lineNumber);
        var point = new SnapshotPoint(TextView.TextSnapshot, snapshotLine.Start.Position);
        TextView.Caret.MoveTo(new SnapshotPoint(TextView.TextSnapshot, point));

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
