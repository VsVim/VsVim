using System;

using CoreGraphics;
using Foundation;

namespace Vim.UI.Cocoa.Implementation.RelativeLineNumbers
{
    internal static class CocoaTextManager
    {
        private static readonly NSString AppleFontSmoothing = new NSString(nameof(AppleFontSmoothing));
        private static readonly IDisposable smoothingObserver;
        private static int smoothing;

        public static event EventHandler DefaultsChanged;

        static CocoaTextManager()
        {
            UpdateSmoothing(NSUserDefaults.StandardUserDefaults.ValueForKey(AppleFontSmoothing));
            smoothingObserver = NSUserDefaults.StandardUserDefaults.AddObserver(
                AppleFontSmoothing,
                NSKeyValueObservingOptions.New,
                change => UpdateSmoothing(change?.NewValue));
        }

        private static void UpdateSmoothing(NSObject value)
        {
            var newSmoothing = value is NSNumber number
                ? number.Int32Value
                : -1;

            if (newSmoothing == smoothing)
                return;

            smoothing = newSmoothing;

            DefaultsChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void ConfigureContext(CGContext context)
        {
            if (context == null)
                return;

            context.SetShouldAntialias(true);
            context.SetShouldSubpixelPositionFonts(true);

            if (MacRuntimeEnvironment.MojaveOrNewer)
            {
                context.SetAllowsFontSmoothing(smoothing < 0);
            }
            else
            {
                // NOTE: we cannot do proper subpixel AA because the layer/context
                // needs a background color which is not available in the text layer.
                // Selections and highlights are separate layers in the editor. If
                // we had reliable background colors, we could enable subpixel AA
                // "smoothing" by setting this to true and ensuring the context
                // had a background color (by way of the target layer or calling
                // the private CGContextSetFontSmoothingBackgroundColor.
                context.SetShouldSmoothFonts(false);
            }
        }
    }
}
