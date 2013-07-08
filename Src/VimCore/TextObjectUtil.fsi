#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open System.Collections.Generic
open Vim.Modes
open Vim.StringBuilderExtensions
open Vim.Interpreter
open EditorUtils

[<RequireQualifiedAccess>]
[<NoComparison>]
type internal SentenceKind = 

    /// Default behavior of a sentence as defined by ':help sentence'
    | Default

    /// There is one definition of a sentence in Vim but the implementation indicates there
    /// are actually 2.  In some cases the trailing characters are not considered a part of 
    /// a sentence.
    ///
    /// http://groups.google.com/group/vim_use/browse_thread/thread/d3f28cf801dc2030
    | NoTrailingCharacters

[<RequireQualifiedAccess>]
[<NoComparison>]
type internal SectionKind =

    /// By default a section break happens on a form feed in the first 
    /// column or one of the nroff macros
    | Default

    /// Split on an open brace in addition to default settings
    | OnOpenBrace

    /// Split on a close brace in addition to default settings
    | OnCloseBrace

    /// Split on an open brace or below a close brace in addition to default
    /// settings
    | OnOpenBraceOrBelowCloseBrace

type internal TextObjectUtil =

    new : globalSettings : IVimGlobalSettings * textBuffer : ITextBuffer -> TextObjectUtil

    /// Get the SnapshotSpan values for the paragraph object starting from the given SnapshotPoint
    /// in the specified direction.  
    member GetParagraphs : path : Path -> point : SnapshotPoint -> SnapshotSpan seq

    /// Get the SnapshotLineRange values for the section values starting from the given SnapshotPoint 
    /// in the specified direction.  Note: The full span of the section will be returned if the 
    /// provided SnapshotPoint is in the middle of it
    member GetSectionRanges : sectionKind : SectionKind -> path : Path -> point : SnapshotPoint -> SnapshotLineRange seq

    /// Get the SnapshotSpan values for the section values starting from the given SnapshotPoint 
    /// in the specified direction.  Note: The full span of the section will be returned if the 
    /// provided SnapshotPoint is in the middle of it
    member GetSections : sectionKind : SectionKind -> path : Path -> point : SnapshotPoint -> SnapshotSpan seq

    /// Get the SnapshotSpan values for the sentence values starting from the given SnapshotPoint 
    /// in the specified direction.  Note: The full span of the section will be returned if the 
    /// provided SnapshotPoint is in the middle of it
    member GetSentences : sentenceKind : SentenceKind -> path : Path -> point : SnapshotPoint -> SnapshotSpan seq

    /// Is the SnapshotPoint the start of a sentence 
    member IsSentenceStart : sentenceKind : SentenceKind -> column : SnapshotColumn -> bool

    /// Is the SnapshotPoint in the white space between sentences
    member IsSentenceWhiteSpace : sentenceKind : SentenceKind -> column : SnapshotColumn -> bool
