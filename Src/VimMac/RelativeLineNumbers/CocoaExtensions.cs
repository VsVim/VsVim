// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

using AppKit;
using CoreAnimation;
using CoreGraphics;
using CoreText;
using Foundation;
using ObjCRuntime;

namespace Vim.UI.Cocoa.Implementation.RelativeLineNumbers
{
    public static class CocoaExtensions
    {
        public static double WidthIncludingTrailingWhitespace(this CTLine line)
            => line.GetBounds(0).Width;

        //Before "optimising" this to `new CGColor(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);`
        //notice that doesn't handle colorspace correctly
        public static CGColor AsCGColor(this Color color)
            => NSColor.FromRgba(color.R, color.G, color.B, color.A).CGColor;

        public static bool IsDarkColor(this CGColor color)
        {
            double red;
            double green;
            double blue;

            var components = color?.Components;

            if (components == null || components.Length == 0)
                return false;

            if (components.Length >= 3)
            {
                red = components[0];
                green = components[1];
                blue = components[2];
            }
            else
            {
                red = green = blue = components[0];
            }

            // https://www.w3.org/WAI/ER/WD-AERT/#color-contrast
            var brightness = (red * 255 * 299 + green * 255 * 587 + blue * 255 * 114) / 1000;
            return brightness <= 155;
        }

        public static bool IsMouseOver(this NSView view)
        {
            var mousePoint = NSEvent.CurrentMouseLocation;
            return IsMouseOver(view, mousePoint);
        }

        public static bool IsMouseOver(this NSView view, CGPoint mousePoint)
        {
            var window = view.Window;
            if (window == null)
            {
                return false;
            }
            var viewScreenRect = window.ConvertRectToScreen(
                view.ConvertRectToView(view.Frame, null));
            return viewScreenRect.Contains(mousePoint);
        }

        public static Rect AsRect(this CGRect rect)
        {
            return new Rect(rect.X, rect.Y, rect.Width, rect.Height);
        }

        public static CGPoint AsCGPoint(this Point point)
        {
            return new CGPoint(point.X, point.Y);
        }

        public static Point PointToScreen(this NSView view, Point point)
        {
            var p = view.ConvertPointToView(new CGPoint(point.X, point.Y), null);
            if (view.Window == null)
                return new Point(p.X, p.Y);
            p = view.Window.ConvertPointToScreen(p);
            return new Point(p.X, p.Y);
        }

        public static CGPoint PointToScreen(this NSView view, CGPoint point)
        {
            var p = view.ConvertPointToView(point, null);
            if (view.Window == null)
                return p;
            p = view.Window.ConvertPointToScreen(p);
            return p;
        }

        private static readonly NSDictionary disableAnimations = new NSDictionary(
            "actions", NSNull.Null,
            "contents", NSNull.Null,
            "hidden", NSNull.Null,
            "onLayout", NSNull.Null,
            "onOrderIn", NSNull.Null,
            "onOrderOut", NSNull.Null,
            "position", NSNull.Null,
            "sublayers", NSNull.Null,
            "transform", NSNull.Null,
            "bounds", NSNull.Null);

        public static void DisableImplicitAnimations(this CALayer layer)
            => layer.Actions = disableAnimations;
    }
}

namespace CoreGraphics
{
    internal static class CoreGraphicsExtensions
    {
        public static CGSize InflateToIntegral(this CGSize size)
            => new CGSize(NMath.Ceiling(size.Width), NMath.Ceiling(size.Height));
    }
}
