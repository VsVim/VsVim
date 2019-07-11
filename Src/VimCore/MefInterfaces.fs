#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Tagging
open System
open System.Configuration

/// Used to determine if a completion window is active for a given view
type IDisplayWindowBroker =

    /// TextView this broker is associated with
    abstract TextView: ITextView 

    /// Is there currently a completion window active on the given ITextView
    abstract IsCompletionActive: bool

    /// Is signature help currently active
    abstract IsSignatureHelpActive: bool

    // Is Quick Info active
    abstract IsQuickInfoActive: bool 

    /// Dismiss any completion windows on the given ITextView
    abstract DismissDisplayWindows: unit -> unit

type IDisplayWindowBrokerFactoryService  =

    /// Get the display broker for the provided ITextView
    abstract GetDisplayWindowBroker: textView: ITextView -> IDisplayWindowBroker

/// What type of tracking are we doing
[<RequireQualifiedAccess>]
[<NoComparison>]
[<NoEquality>]
type LineColumnTrackingMode = 

    /// By default a 1TrackingLineColumn will be deleted if the line it 
    /// tracks is deleted
    | Default 

    /// ITrackingLineColumn should survive the deletion of it's line
    /// and just treat it as a delta adjustment
    | SurviveDeletes 

    /// Same as Survives delete but it resets the column to 0 in this 
    /// case
    | LastEditPoint

/// Tracks a line number and column across edits to the ITextBuffer.  This auto tracks
/// and will return information against the current ITextSnapshot for the 
/// ITextBuffer
type ITrackingLineColumn =

    /// ITextBuffer this ITrackingLineColumn is tracking against
    abstract TextBuffer: ITextBuffer

    /// Returns the LineColumnTrackingMode for this instance
    abstract TrackingMode: LineColumnTrackingMode

    /// Get the point as it relates to current Snapshot.
    abstract Point: SnapshotPoint option 

    /// Get the point as a VirtualSnapshot point on the current ITextSnapshot
    abstract VirtualPoint: VirtualSnapshotPoint option

    /// Get the column as it relates to current Snapshot.
    abstract Column: SnapshotColumn option 

    /// Get the column as a VirtualSnapshotColumn point on the current ITextSnapshot
    abstract VirtualColumn: VirtualSnapshotColumn option

    /// Needs to be called when you are done with the ITrackingLineColumn
    abstract Close: unit -> unit

/// Tracks a VisualSpan across edits to the underlying ITextBuffer.
type ITrackingVisualSpan = 

    /// The associated ITextBuffer instance
    abstract TextBuffer: ITextBuffer

    /// Get the VisualSpan as it relates to the current ITextSnapshot
    abstract VisualSpan: VisualSpan option

    /// Needs to be called when the consumer is finished with the ITrackingVisualSpan
    abstract Close: unit -> unit

/// Tracks a Visual Selection across edits to the underlying ITextBuffer.  This tracks both
/// the selection area and the caret within the selection
type ITrackingVisualSelection = 

    /// The SnapshotPoint for the caret within the current ITextSnapshot
    abstract CaretPoint: SnapshotPoint option

    /// The associated ITextBuffer instance
    abstract TextBuffer: ITextBuffer

    /// Get the Visual Selection as it relates to the current ITextSnapshot
    abstract VisualSelection: VisualSelection option

    /// Needs to be called when the consumer is finished with the ITrackingVisualSpan
    abstract Close: unit -> unit

type IBufferTrackingService = 

    /// Create an ITrackingLineColumn at the given position in the buffer.  
    abstract CreateLineOffset: textBuffer: ITextBuffer -> lineNumber: int -> offset: int -> mode: LineColumnTrackingMode -> ITrackingLineColumn

    /// Create an ITrackingLineColumn at the given SnaphsotColumn
    abstract CreateColumn: column: SnapshotColumn -> mode: LineColumnTrackingMode -> ITrackingLineColumn

    /// Create an ITrackingVisualSpan for the given VisualSpan
    abstract CreateVisualSpan: visualSpan: VisualSpan -> ITrackingVisualSpan

    /// Create an ITrackingVisualSelection for the given Visual Selection
    abstract CreateVisualSelection: visualSelection: VisualSelection -> ITrackingVisualSelection

    /// Does the given ITextBuffer have any out standing tracking instances 
    abstract HasTrackingItems: textBuffer: ITextBuffer -> bool

type IVimBufferCreationListener =

    /// Called whenever an IVimBuffer is created
    abstract VimBufferCreated: vimBuffer: IVimBuffer -> unit

/// Supports the creation and deletion of folds within a ITextBuffer.  Most components
/// should talk to IFoldManager directly
type IFoldData = 

    /// Associated ITextBuffer the data is over
    abstract TextBuffer: ITextBuffer

    /// Gets snapshot spans for all of the currently existing folds.  This will
    /// only return the folds explicitly created by vim.  It won't return any
    /// collapsed regions in the ITextView
    abstract Folds: SnapshotSpan seq

    /// Create a fold for the given line range
    abstract CreateFold: SnapshotLineRange -> unit 

    /// Delete a fold which crosses the given SnapshotPoint.  Returns false if 
    /// there was no fold to be deleted
    abstract DeleteFold: SnapshotPoint -> bool

    /// Delete all of the folds which intersect the given SnapshotSpan
    abstract DeleteAllFolds: SnapshotSpan -> unit

    /// Raised when the collection of folds are updated for any reason
    [<CLIEvent>]
    abstract FoldsUpdated: IDelegateEvent<System.EventHandler>

/// Supports the creation and deletion of folds within a ITextBuffer
///
/// TODO: This should become a merger between folds and outlining regions in 
/// an ITextBuffer / ITextView
type IFoldManager = 

    /// Associated ITextView
    abstract TextView: ITextView

    /// Create a fold for the given line range.  The fold will be created in a closed state.
    abstract CreateFold: range: SnapshotLineRange -> unit 

    /// Close 'count' fold values under the given SnapshotPoint
    abstract CloseFold: point: SnapshotPoint -> count: int -> unit

    /// Close all folds which intersect the given SnapshotSpan
    abstract CloseAllFolds: span: SnapshotSpan -> unit

    /// Delete a fold which crosses the given SnapshotPoint.  Returns false if 
    /// there was no fold to be deleted
    abstract DeleteFold: point: SnapshotPoint -> unit

    /// Delete all of the folds which intersect the SnapshotSpan
    abstract DeleteAllFolds: span: SnapshotSpan -> unit

    /// Toggle fold under the given SnapshotPoint
    abstract ToggleFold: point: SnapshotPoint -> count: int -> unit

    /// Toggle all folds under the given SnapshotPoint
    abstract ToggleAllFolds: span: SnapshotSpan -> unit

    /// Open 'count' folds under the given SnapshotPoint
    abstract OpenFold: point: SnapshotPoint -> count: int -> unit

    /// Open all folds which intersect the given SnapshotSpan
    abstract OpenAllFolds: span: SnapshotSpan -> unit

/// Supports the get and creation of IFoldManager for a given ITextBuffer
type IFoldManagerFactory =

    /// Get the IFoldData for this ITextBuffer
    abstract GetFoldData: textBuffer: ITextBuffer -> IFoldData

    /// Get the IFoldManager for this ITextView.
    abstract GetFoldManager: textView: ITextView -> IFoldManager

/// Used because the actual Point class is in WPF which isn't available at this layer.
[<Struct>]
type VimPoint = {
    X: double
    Y: double
}

/// Abstract representation of the mouse
type IMouseDevice = 
    
    /// Is the left button pressed
    abstract IsLeftButtonPressed: bool

    /// Is the right button pressed
    abstract IsRightButtonPressed: bool

    /// Get the position of the mouse position within the ITextView
    abstract GetPosition: textView: ITextView -> VimPoint option

    /// Is the given ITextView in the middle fo a drag operation?
    abstract InDragOperation: textView: ITextView -> bool

/// Abstract representation of the keyboard 
type IKeyboardDevice = 

    /// Is the given key pressed
    abstract IsArrowKeyDown: bool

    /// The modifiers currently pressed on the keyboard
    abstract KeyModifiers: VimKeyModifiers

/// Tracks changes to the associated ITextView
type ILineChangeTracker =

    /// Swap the most recently changed line with its saved copy
    abstract Swap: unit -> bool

/// Manages the ILineChangeTracker instances
type ILineChangeTrackerFactory =

    /// Get the ILineChangeTracker associated with the given vim buffer information
    abstract GetLineChangeTracker: vimBufferData: IVimBufferData -> ILineChangeTracker

/// Provides access to the system clipboard 
type IClipboardDevice =

    /// Whether to report errors that occur when using the clipboard
    abstract ReportErrors: bool with get, set

    /// The text contents of the clipboard device
    abstract Text: string with get, set

[<RequireQualifiedAccess>]
[<NoComparison>]
[<NoEquality>]
type Result = 
    | Succeeded
    | Failed of Error: string

[<Flags>]
type ViewFlags = 
    | None = 0 

    /// Ensure the context point is visible in the ITextView
    | Visible = 0x01

    /// If the context point is inside a collapsed region then it needs to be exapnded
    | TextExpanded = 0x02

    /// Using the context point as a reference ensure the scroll respects the 'scrolloff'
    /// setting
    | ScrollOffset = 0x04

    /// Possibly move the caret to account for the 'virtualedit' setting
    | VirtualEdit = 0x08

    /// Standard flags: 
    /// Visible ||| TextExpanded ||| ScrollOffset
    | Standard = 0x07

    /// All flags
    /// Visible ||| TextExpanded ||| ScrollOffset ||| VirtualEdit
    | All = 0x0f

[<RequireQualifiedAccess>]
[<NoComparison>]
type RegisterOperation = 
    | Yank
    | Delete

    /// Force the operation to be treated like a big delete even if it's a small one.
    | BigDelete

/// This class abstracts out the operations that are common to normal, visual and 
/// command mode.  It usually contains common edit and movement operations and very
/// rarely will deal with caret operations.  That is the responsibility of the 
/// caller
type ICommonOperations =

    /// Run the beep operation
    abstract Beep: unit -> unit

    /// Associated IEditorOperations
    abstract EditorOperations: IEditorOperations

    /// Associated IEditorOptions
    abstract EditorOptions: IEditorOptions

    /// The snapshot point in the buffer under the mouse cursor
    abstract MousePoint: VirtualSnapshotPoint option

    /// Associated ITextView
    abstract TextView: ITextView 

    /// Associated VimBufferData instance
    abstract VimBufferData: IVimBufferData

    /// Adjust the ITextView scrolling to account for the 'scrolloff' setting after a move operation
    /// completes
    abstract AdjustTextViewForScrollOffset: unit -> unit

    /// This is the same function as AdjustTextViewForScrollOffsetAtPoint except that it moves the caret 
    /// not the view port.  Make the caret consistent with the setting not the display 
    abstract AdjustCaretForScrollOffset: unit -> unit

    /// Adjust the caret if it is past the end of line and 'virtualedit=onemore' is not set
    abstract AdjustCaretForVirtualEdit: unit -> unit

    abstract member CloseWindowUnlessDirty: unit -> unit

    /// Create a possibly LineWise register value with the specified string value at the given 
    /// point.  This is factored out here because a LineWise value in vim should always
    /// end with a new line but we can't always guarantee the text we are working with 
    /// contains a new line.  This normalizes out the process needed to make this correct
    /// while respecting the settings of the ITextBuffer
    abstract CreateRegisterValue: point: SnapshotPoint -> stringData: StringData -> operationKind: OperationKind -> RegisterValue

    /// Delete at least count lines from the buffer starting from the provided line.  The 
    /// operation will fail if there aren't at least 'maxCount' lines in the buffer from
    /// the start point.
    ///
    /// This operation is performed against the visual buffer.  
    abstract DeleteLines: startLine: ITextSnapshotLine -> maxCount: int -> registerName: RegisterName option -> unit

    /// Perform the specified action asynchronously using the scheduler
    abstract DoActionAsync: action: (unit -> unit) -> unit

    /// Perform the specified action when the text view is ready
    abstract DoActionWhenReady: action: (unit -> unit) -> unit

    /// Ensure the view properties are met at the caret
    abstract EnsureAtCaret: viewFlags: ViewFlags -> unit

    /// Ensure the view properties are met at the point
    abstract EnsureAtPoint: point: SnapshotPoint -> viewFlags: ViewFlags -> unit

    /// Filter the specified line range through the specified command
    abstract FilterLines: SnapshotLineRange -> command: string -> unit

    /// Format the specified line range
    abstract FormatCodeLines: SnapshotLineRange -> unit

    /// Format the specified line range
    abstract FormatTextLines: SnapshotLineRange -> preserveCaretPosition: bool -> unit

    /// Forward the specified action to the focused window
    abstract ForwardToFocusedWindow: action: (ICommonOperations -> unit) -> unit

    /// Get the new line text which should be used for new lines at the given SnapshotPoint
    abstract GetNewLineText: SnapshotPoint -> string

    /// Get the register to use based on the name provided to the operation.
    abstract GetRegister: name: RegisterName option -> Register

    /// Get the indentation for a newly created ITextSnasphotLine.  The context line is
    /// is provided to calculate the indentation off of 
    ///
    /// Warning: Calling this API can cause the buffer to be edited.  Asking certain 
    /// editor implementations about the indentation, in particular Razor, can cause
    /// an edit to occur.  
    ///
    /// Issue #946
    abstract GetNewLineIndent: contextLine: ITextSnapshotLine -> newLine: ITextSnapshotLine -> int option

    /// Get the standard ReplaceData for the given SnapshotPoint
    abstract GetReplaceData: point: SnapshotPoint -> VimRegexReplaceData

    /// Get the current number of spaces to caret we are maintaining
    abstract GetSpacesToCaret: unit -> int

    /// Get the number of spaces (when tabs are expanded) that is necessary to get to the 
    /// specified point on it's line
    abstract GetSpacesToPoint: point: SnapshotPoint -> int

    /// Get the point that visually corresponds to the specified column on its line
    abstract GetColumnForSpacesOrEnd: contextLine: ITextSnapshotLine -> spaces: int -> SnapshotColumn

    /// Get the number of spaces (when tabs are expanded) that is necessary to get to the
    /// specified virtual point on it's line
    abstract GetSpacesToVirtualColumn: column: VirtualSnapshotColumn -> int

    /// Get the virtual point that visually corresponds to the specified column on its line
    abstract GetVirtualColumnForSpaces: contextLine: ITextSnapshotLine -> spaces: int -> VirtualSnapshotColumn

    /// Attempt to GoToDefinition on the current state of the buffer.  If this operation fails, an error message will 
    /// be generated as appropriate
    abstract GoToDefinition: unit -> Result

    /// Go to the file named in the word under the cursor
    abstract GoToFile: unit -> unit

    /// Go to the file name specified as a paramter
    abstract GoToFile: string -> unit

    /// Go to the file named in the word under the cursor in a new window
    abstract GoToFileInNewWindow: unit -> unit

    /// Go to the file name specified as a paramter in a new window
    abstract GoToFileInNewWindow: string -> unit

    /// Go to the local declaration of the word under the cursor
    abstract GoToLocalDeclaration: unit -> unit

    /// Go to the global declaration of the word under the cursor
    abstract GoToGlobalDeclaration: unit -> unit

    /// Go to the "count" next tab window in the specified direction.  This will wrap 
    /// around
    abstract GoToNextTab: path: SearchPath -> count: int -> unit

    /// Go the nth tab.  This uses vim's method of numbering tabs which is a 1 based list.  Both
    /// 0 and 1 can be used to access the first tab
    abstract GoToTab: int -> unit

    /// Using the specified base folder, go to the tag specified by ident
    abstract GoToTagInNewWindow: folder: string -> ident: string -> Result

    /// Convert any virtual spaces into real spaces / tabs based on the current settings.  The caret 
    /// will be positioned at the end of that change
    abstract FillInVirtualSpace: unit -> unit

    /// Joins the lines in the range
    abstract Join: SnapshotLineRange -> JoinKind -> unit

    /// Load a file into a new window, optionally moving the caret to the first
    /// non-blank on a specific line or to a specific line and column
    abstract LoadFileIntoNewWindow: file: string -> lineNumber: int option -> columnNumber: int option -> Result

    /// Move the caret in the specified direction
    abstract MoveCaret: caretMovement: CaretMovement -> bool

    /// Move the caret in the specified direction with an arrow key
    abstract MoveCaretWithArrow: caretMovement: CaretMovement -> bool

    /// Move the caret to a given point on the screen and ensure the view has the specified
    /// properties at that point 
    abstract MoveCaretToColumn: column: SnapshotColumn -> viewFlags: ViewFlags -> unit

    /// Move the caret to a given virtual point on the screen and ensure the view has the specified
    /// properties at that point
    abstract MoveCaretToVirtualColumn: column: VirtualSnapshotColumn -> viewFlags: ViewFlags -> unit

    /// Move the caret to a given point on the screen and ensure the view has the specified
    /// properties at that point 
    abstract MoveCaretToPoint: point: SnapshotPoint -> viewFlags: ViewFlags -> unit

    /// Move the caret to a given virtual point on the screen and ensure the view has the specified
    /// properties at that point
    abstract MoveCaretToVirtualPoint: point: VirtualSnapshotPoint -> viewFlags: ViewFlags -> unit

    /// Move the caret to the MotionResult value
    abstract MoveCaretToMotionResult: motionResult: MotionResult -> unit

    /// Navigate to the given point which may occur in any ITextBuffer.  This will not update the 
    /// jump list
    abstract NavigateToPoint: VirtualSnapshotPoint -> bool

    /// Normalize the spaces and tabs in the string
    abstract NormalizeBlanks: text: string -> spacesToColumn: int -> string

    /// Normalize the spaces and tabs in the string at the given column in the buffer
    abstract NormalizeBlanksAtColumn: text: string -> column: SnapshotColumn -> string

    /// Normalize the spaces and tabs in the string for a new tabstop
    abstract NormalizeBlanksForNewTabStop: text: string -> spacesToColumn: int -> tabStop: int -> string

    /// Normalize the set of spaces and tabs into spaces
    abstract NormalizeBlanksToSpaces: text: string -> spacesToColumn: int -> string

    /// Display a status message and fit it to the size of the window
    abstract OnStatusFitToWindow: message: string -> unit

    /// Open link under caret
    abstract OpenLinkUnderCaret: unit -> Result

    /// Put the specified StringData at the given point.
    abstract Put: SnapshotPoint -> StringData -> OperationKind -> unit

    /// Raise the error / warning messages for the given SearchResult
    abstract RaiseSearchResultMessage: SearchResult -> unit

    /// Record last change start and end positions
    abstract RecordLastChange: oldSpan: SnapshotSpan -> newSpan: SnapshotSpan -> unit

    /// Record last yank start and end positions
    abstract RecordLastYank: span: SnapshotSpan -> unit

    /// Redo the buffer changes "count" times
    abstract Redo: count:int -> unit

    /// Restore spaces to caret, or move to start of line if 'startofline' is set
    abstract RestoreSpacesToCaret: spacesToCaret: int -> useStartOfLine: bool -> unit

    /// Scrolls the number of lines given and keeps the caret in the view
    abstract ScrollLines: ScrollDirection -> count:int -> unit

    /// Update the register with the specified value
    abstract SetRegisterValue: name: RegisterName option -> operation: RegisterOperation -> value: RegisterValue -> unit

    /// Shift the block of lines to the left by shiftwidth * 'multiplier'
    abstract ShiftLineBlockLeft: SnapshotSpan seq -> multiplier: int -> unit

    /// Shift the block of lines to the right by shiftwidth * 'multiplier'
    abstract ShiftLineBlockRight: SnapshotSpan seq -> multiplier: int -> unit

    /// Shift the given line range left by shiftwidth * 'multiplier'
    abstract ShiftLineRangeLeft: SnapshotLineRange -> multiplier: int -> unit

    /// Shift the given line range right by shiftwidth * 'multiplier'
    abstract ShiftLineRangeRight: SnapshotLineRange -> multiplier: int -> unit

    /// Sort the given line range
    abstract SortLines: SnapshotLineRange -> reverseOrder: bool -> SortFlags -> pattern: string option -> unit

    /// Substitute Command implementation
    abstract Substitute: pattern: string -> replace: string -> SnapshotLineRange -> flags: SubstituteFlags -> unit

    /// Toggle the use of typing language characters
    abstract ToggleLanguage: isForInsert: bool -> unit

    /// Map the specified point with negative tracking to the current snapshot
    abstract MapPointNegativeToCurrentSnapshot: point: SnapshotPoint -> SnapshotPoint

    /// Map the specified point with positive tracking to the current snapshot
    abstract MapPointPositiveToCurrentSnapshot: point: SnapshotPoint -> SnapshotPoint

    /// Undo the buffer changes "count" times
    abstract Undo: count: int -> unit

/// Factory for getting ICommonOperations instances
type ICommonOperationsFactory =

    /// Get the ICommonOperations instance for this IVimBuffer
    abstract GetCommonOperations: IVimBufferData -> ICommonOperations

/// This interface is used to prevent the transition from insert to visual mode
/// when a selection occurs.  In the majority case a selection of text should result
/// in a transition to visual mode.  In some cases though, C# event handlers being
/// the most notable, the best experience is for the buffer to remain in insert 
/// mode
type IVisualModeSelectionOverride =

    /// Is insert mode preferred for the current state of the buffer
    abstract IsInsertModePreferred: textView: ITextView -> bool

/// What source should the synchronizer use as the original settings?  The values
/// in the selected source will be copied over the other settings
[<RequireQualifiedAccess>]
type SettingSyncSource =
    | Editor
    | Vim 

 [<Struct>]
 type SettingSyncData = {
    EditorOptionKey: string 
    GetEditorValue: IEditorOptions -> SettingValue option
    VimSettingNames: string list
    GetVimValue: IVimBuffer -> obj
    SetVimValue: IVimBuffer -> SettingValue -> unit
    IsLocal: bool
} with

    member x.IsWindow = not x.IsLocal

    member x.GetSettings vimBuffer = SettingSyncData.GetSettingsCore vimBuffer x.IsLocal

    static member private GetSettingsCore (vimBuffer: IVimBuffer) isLocal = 
        if isLocal then vimBuffer.LocalSettings :> IVimSettings
        else vimBuffer.WindowSettings :> IVimSettings

    static member GetBoolValueFunc (editorOptionKey: EditorOptionKey<bool>) = 
        (fun editorOptions -> 
            match EditorOptionsUtil.GetOptionValue editorOptions editorOptionKey with
            | None -> None
            | Some value -> SettingValue.Toggle value |> Some)

    static member GetNumberValueFunc (editorOptionKey: EditorOptionKey<int>) = 
        (fun editorOptions -> 
            match EditorOptionsUtil.GetOptionValue editorOptions editorOptionKey with
            | None -> None
            | Some value -> SettingValue.Number value |> Some)

    static member GetStringValue (editorOptionKey: EditorOptionKey<string>) = 
        (fun editorOptions -> 
            match EditorOptionsUtil.GetOptionValue editorOptions editorOptionKey with
            | None -> None
            | Some value -> SettingValue.String value |> Some)

    static member GetSettingValueFunc name isLocal =
        (fun (vimBuffer: IVimBuffer) ->
            let settings = SettingSyncData.GetSettingsCore vimBuffer isLocal
            match settings.GetSetting name with
            | None -> null
            | Some setting -> 
                match setting.Value with 
                | SettingValue.String value -> value :> obj
                | SettingValue.Toggle value -> box value
                | SettingValue.Number value -> box value)

    static member SetVimValueFunc name isLocal =
        fun (vimBuffer: IVimBuffer) value ->
            let settings = SettingSyncData.GetSettingsCore vimBuffer isLocal
            settings.TrySetValue name value |> ignore

    static member Create (key: EditorOptionKey<'T>) (settingName: string) (isLocal: bool) (convertEditorValue: Func<'T, SettingValue>) (convertSettingValue: Func<SettingValue, obj>) =
        {
            EditorOptionKey = key.Name
            GetEditorValue = (fun editorOptions ->
                match EditorOptionsUtil.GetOptionValue editorOptions key with
                | None -> None
                | Some value -> convertEditorValue.Invoke value |> Some)
            VimSettingNames = [settingName]
            GetVimValue = (fun vimBuffer -> 
                let settings = SettingSyncData.GetSettingsCore vimBuffer isLocal 
                match settings.GetSetting settingName with
                | None -> null
                | Some setting -> convertSettingValue.Invoke setting.Value)
            SetVimValue = SettingSyncData.SetVimValueFunc settingName isLocal
            IsLocal = isLocal
        }


/// This interface is used to synchronize settings between vim settings and the 
/// editor settings
type IEditorToSettingsSynchronizer = 
    
    /// Begin the synchronization between the editor and vim settings.  This will 
    /// start by overwriting the editor settings with the current local ones 
    ///
    /// This method can be called multiple times for the same IVimBuffer and it 
    /// will only synchronize once 
    abstract StartSynchronizing: vimBuffer: IVimBuffer -> source: SettingSyncSource -> unit

    abstract SyncSetting: data: SettingSyncData -> unit

/// There are some VsVim services which are only valid in specific host environments. These
/// services will implement and export this interface. At runtime the identifier can be
/// compared to the IVimHost.Identifier to see if it's valid
type IVimSpecificService = 
    abstract HostIdentifier: string

/// This will look for an export of <see cref="IVimSpecificService"\> that is convertible to 
/// 'T and return it
type IVimSpecificServiceHost =

    abstract GetService: unit -> 'T option
