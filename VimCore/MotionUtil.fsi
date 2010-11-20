#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

/// Contains motion actions which are generally useful to components
module MotionUtil = 

    /// Get the paragraphs starting at the given SnapshotPoint
    val GetParagraphs : SnapshotPoint -> Direction -> seq<Paragraph>

    /// Get the paragraphs which are contained within the specified SnapshotSpan
    val GetParagraphsInSpan : SnapshotSpan -> Direction -> seq<Paragraph>

    /// Get the full paragrah.  This operation doesn't distinguish boundaries so we
    /// just return the span
    val GetFullParagraph : SnapshotPoint -> SnapshotSpan

    /// Get the sentences starting at the given SnapshotPoint
    val GetSentences : SnapshotPoint -> Direction -> seq<SnapshotSpan>

    /// Get the full sentence on which the given point resides
    val GetSentenceFull : SnapshotPoint -> SnapshotSpan 

