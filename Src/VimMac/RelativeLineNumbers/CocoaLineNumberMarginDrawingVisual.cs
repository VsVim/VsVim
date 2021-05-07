using System;
using System.Globalization;

using AppKit;
using CoreAnimation;
using CoreGraphics;
using CoreText;
using Foundation;
using Vim.UI.Wpf.Implementation.RelativeLineNumbers;

namespace Vim.UI.Cocoa.Implementation.RelativeLineNumbers
{
    internal sealed class CocoaLineNumberMarginDrawingVisual : CALayer, ICALayerDelegate
    {
        NSStringAttributes stringAttributes;
        CGRect lineBounds;
        nfloat lineAscent;
        CTLine ctLine;

        public int LineNumber { get; private set; }
        public int DisplayNumber { get; private set; }

        public bool InUse
        {
            get => !Hidden;
            set => Hidden = !value;
        }

        public CocoaLineNumberMarginDrawingVisual()
        {
            Delegate = this;
            NeedsDisplayOnBoundsChange = true;
            this.DisableImplicitAnimations();
        }

        internal void Update(
            NSStringAttributes stringAttributes,
            Line line,
            nfloat lineWidth)
        {
            // NOTE: keep this in sync with CocoaRenderedLineVisual regarding any font
            // metric handling, transforms, etc. Ensure that line numbers are always
            // exactly aligned with the actual editor text lines. Test with Fluent
            // Calibri and many other fonts at various sizes.

            if (DisplayNumber != line.DisplayNumber || this.stringAttributes != stringAttributes)
            {
                DisplayNumber = line.DisplayNumber;
                this.stringAttributes = stringAttributes;

                ctLine?.Dispose();
                ctLine = new CTLine(new NSAttributedString(
                    line.DisplayNumber.ToString(CultureInfo.CurrentUICulture.NumberFormat),
                    stringAttributes));

                lineBounds = ctLine.GetBounds(0);
                ctLine.GetTypographicBounds(out lineAscent, out _, out _);

                SetNeedsDisplay();
            }

            AffineTransform = new CGAffineTransform(
                1, 0,
                0, 1,
                0, (nfloat)line.TextTop);

            var transformRect = AffineTransform.TransformRect(new CGRect(
                line.IsCaretLine ? 0 : lineWidth - lineBounds.Width, // right justify
                line.Baseline - lineAscent,
                lineBounds.Width,
                lineBounds.Height));

            Frame = new CGRect(
                NMath.Floor(transformRect.X),
                NMath.Floor(transformRect.Y),
                NMath.Ceiling(transformRect.Width),
                NMath.Ceiling(transformRect.Height));
        }

        [Export("drawLayer:inContext:")]
        void Draw(CALayer _, CGContext context)
        {
            if (ctLine == null)
                return;

            CocoaTextManager.ConfigureContext(context);

            context.TextPosition = new CGPoint(0, -lineAscent);
            context.ScaleCTM(1, -1);

            ctLine.Draw(context);
        }
    }
}
