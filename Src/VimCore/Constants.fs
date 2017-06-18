#light 

namespace Vim

module VimConstants =

    /// Content type which Vim hosts should create an IVimBuffer for
    [<Literal>]
    let ContentType = "text"

    /// The decision of the content types on which an IVimBuffer should be created is a decision
    /// which is left up to the host.  The core Vim services such as tagging need to apply to any
    /// situation in which an IVimBuffer is created.  Hence they must apply to the most general
    /// content type which is "any".  The value "text" is insufficient in those circumstances because
    /// it won't apply to projection buffers
    [<Literal>]
    let AnyContentType = "any"

    [<Literal>]
    let DefaultHistoryLength = 20

    [<Literal>]
    let IncrementalSearchTagName = "vsvim_incrementalsearch"

    [<Literal>]
    let HighlightIncrementalSearchTagName = "vsvim_highlightsearch"

    /// <summary>
    /// Name of the main Key Processor
    /// </summary>
    [<Literal>]
    let MainKeyProcessorName = "VsVim";

#if DEBUG
    [<Literal>]
    let VersionNumber = "2.4.99.99 Debug"
#else
    [<Literal>]
    let VersionNumber = "2.4.1.0"
#endif


