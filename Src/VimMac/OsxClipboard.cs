using System;
using System.Runtime.InteropServices;

static class OsxClipboard
{
    static IntPtr nsString = objc_getClass("NSString");
    static IntPtr nsPasteboard = objc_getClass("NSPasteboard");
    static IntPtr nsStringPboardType;
    static IntPtr utfTextType;
    static IntPtr generalPasteboard;
    static IntPtr initWithUtf8Register = sel_registerName("initWithUTF8String:");
    static IntPtr allocRegister = sel_registerName("alloc");
    static IntPtr setStringRegister = sel_registerName("setString:forType:");
    static IntPtr stringForTypeRegister = sel_registerName("stringForType:");
    static IntPtr utf8Register = sel_registerName("UTF8String");
    static IntPtr generalPasteboardRegister = sel_registerName("generalPasteboard");
    static IntPtr clearContentsRegister = sel_registerName("clearContents");

    static OsxClipboard()
    {
        utfTextType = objc_msgSend(objc_msgSend(nsString, allocRegister), initWithUtf8Register, "public.utf8-plain-text");
        nsStringPboardType = objc_msgSend(objc_msgSend(nsString, allocRegister), initWithUtf8Register, "NSStringPboardType");

        generalPasteboard = objc_msgSend(nsPasteboard, generalPasteboardRegister);
    }

    public static string GetText()
    {
        var ptr = objc_msgSend(generalPasteboard, stringForTypeRegister, nsStringPboardType);
        var charArray = objc_msgSend(ptr, utf8Register);
        return Marshal.PtrToStringAnsi(charArray);
    }

    public static void SetText(string text)
    {
        IntPtr str = default;
        try
        {
            str = objc_msgSend(objc_msgSend(nsString, allocRegister), initWithUtf8Register, text);
            objc_msgSend(generalPasteboard, clearContentsRegister);
            objc_msgSend(generalPasteboard, setStringRegister, str, utfTextType);
        }
        finally
        {
            if (str != default)
            {
                objc_msgSend(str, sel_registerName("release"));
            }
        }
    }

    [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
    static extern IntPtr objc_getClass(string className);

    [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
    static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
    static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, string arg1);

    [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
    static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
    static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

    [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
    static extern IntPtr sel_registerName(string selectorName);
}
