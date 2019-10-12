using System;
using System.Runtime.InteropServices;
using AppKit;

static class OsxClipboard
{
    const string textType = "public.utf8-plain-text";

    public static string GetText()
    {
        return NSPasteboard.GeneralPasteboard.GetStringForType(textType);
    }

    public static void SetText(string text)
    {
        NSPasteboard.GeneralPasteboard.ClearContents();
        NSPasteboard.GeneralPasteboard.SetStringForType(text, textType);
    }
}
