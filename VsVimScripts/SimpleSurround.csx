using System;

readonly string START_TAG = "<";
readonly string END_TAG = ">";

bool tagStarted = false;
string addText = string.Empty;

VimBuffer.KeyIntercept = true;
VimBuffer.KeyInputIntercept += OnKeyInputIntercept;
VimBuffer.KeyInputEnd += OnKeyInputEnd;
VimBuffer.Closed += OnBufferClosed;

private void OnKeyInputIntercept(object sender, KeyInputEventArgs e)
{
    if (e.KeyInput.Char == 't' && !tagStarted)
    {
        tagStarted = true;
    }
    else if (e.KeyInput.Key == VimKey.Escape)
    {
        InterceptEnd();
        return;
    }
    else if (e.KeyInput.Key == VimKey.Enter)
    {
        if (tagStarted && !(string.IsNullOrWhiteSpace(addText)))
        {

            //START
            Process("I", enter: false);
            string sendText = START_TAG + addText + END_TAG;
            Process(sendText, enter: false);
            Process(KeyInputUtil.EscapeKey);

            //END
            string tag = addText;
            int idx = addText.IndexOf(' ');
            if (0 < idx)
            {
                tag = addText.Substring(0, idx);
            }
            Process("A", enter: false);
            sendText = START_TAG + "/" + tag + END_TAG;
            Process(sendText, enter: false);
            Process(KeyInputUtil.EscapeKey);
        }
        InterceptEnd();
        return;
    }
    else if (e.KeyInput.Key == VimKey.Back)
    {
        if (1 < addText.Length)
        {
            addText = addText.Substring(0, addText.Length - 1);
        }
        else if (addText.Length == 1)
        {
            addText = string.Empty;
        }
    }
    else
    {
        addText += e.KeyInput.Char;
    }
}
private void OnKeyInputEnd(object sender, KeyInputEventArgs e)
{
    if (tagStarted)
    {
        DisplayStatus(START_TAG + addText);
    }
}
private void InterceptEnd()
{
    VimBuffer.KeyInputIntercept -= OnKeyInputIntercept;
    VimBuffer.KeyInputEnd -= OnKeyInputEnd;
    VimBuffer.Closed -= OnBufferClosed;
    VimBuffer.KeyIntercept = false;
    DisplayStatus(string.Empty);
}
private void OnBufferClosed(object sender, EventArgs e)
{
    InterceptEnd();
}
