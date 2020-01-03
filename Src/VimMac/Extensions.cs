using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Utilities;
using Vim.VisualStudio;

namespace Vim.Mac
{
    public static class Extensions
    {
        #region IContentType

        /// <summary>
        /// Does this IContentType represent C++
        /// </summary>
        public static bool IsCPlusPlus(this IContentType contentType)
        {
            return contentType.IsOfType(VsVimConstants.CPlusPlusContentType);
        }

        /// <summary>
        /// Does this IContentType represent C#
        /// </summary>
        public static bool IsCSharp(this IContentType contentType)
        {
            return contentType.IsOfType(VsVimConstants.CSharpContentType);
        }

        public static bool IsFSharp(this IContentType contentType)
        {
            return contentType.IsOfType("F#");
        }

        public static bool IsVisualBasic(this IContentType contentType)
        {
            return contentType.IsOfType("Basic");
        }

        public static bool IsJavaScript(this IContentType contentType)
        {
            return contentType.IsOfType("JavaScript");
        }

        public static bool IsResJSON(this IContentType contentType)
        {
            return contentType.IsOfType("ResJSON");
        }

        public static bool IsHTMLXProjection(this IContentType contentType)
        {
            return contentType.IsOfType("HTMLXProjection");
        }

        /// <summary>
        /// Is this IContentType of any of the specified types
        /// </summary>
        public static bool IsOfAnyType(this IContentType contentType, IEnumerable<string> types)
        {
            foreach (var type in types)
            {
                if (contentType.IsOfType(type))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}
