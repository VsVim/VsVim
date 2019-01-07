using System;
using System.Globalization;
using System.Windows.Media.TextFormatting;

using Microsoft.VisualStudio.Text.Formatting;

namespace Vim.UI.Wpf.RelativeLineNumbers
{
    public class LineNumberTextSource : TextSource
    {
        private readonly string _text;

        private readonly TextFormattingRunProperties _formatting;

        public LineNumberTextSource(string text, TextFormattingRunProperties formatting)
        {
            _text = text;
            _formatting = formatting;
        }

        public override TextSpan<CultureSpecificCharacterBufferRange> GetPrecedingText(
            int textSourceCharacterIndexLimit)
        {
            var bufferRange = new CultureSpecificCharacterBufferRange(
                CultureInfo.CurrentUICulture,
                new CharacterBufferRange(string.Empty, 0, 0));

            return new TextSpan<CultureSpecificCharacterBufferRange>(0, bufferRange);
        }

        public override int GetTextEffectCharacterIndexFromTextSourceCharacterIndex(
            int textSourceCharacterIndex)
        {
            if (textSourceCharacterIndex < 0 || _text.Length <= textSourceCharacterIndex)
            {
                throw new ArgumentOutOfRangeException(nameof(textSourceCharacterIndex));
            }

            return textSourceCharacterIndex;
        }

        public override TextRun GetTextRun(int textSourceCharacterIndex)
        {
            if (textSourceCharacterIndex < 0 || _text.Length < textSourceCharacterIndex)
            {
                throw new ArgumentOutOfRangeException(nameof(textSourceCharacterIndex));
            }

            if (textSourceCharacterIndex == _text.Length)
            {
                return new TextEndOfLine(1);
            }

            return new TextCharacters(_text.Substring(textSourceCharacterIndex), _formatting);
        }
    }
}