#light

namespace Vim
open System.Windows
open System.Windows.Controls
open System.Windows.Media
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Formatting
open System
open System.Runtime.InteropServices
open System.Windows.Threading
open Microsoft.VisualStudio.Text.Classification

/// Standard implementation of IBlockCaret which draws a block caret vs. the pipe style caret
/// which is used in several Vim modes
type internal BlockCaret
    (
        _view : IWpfTextView,
        _formatMap : IEditorFormatMap,
        _layer : IAdornmentLayer) as this =
    let _caretOpacity = 0.65

    /// Tag used to identify our items in the adornment layer 
    let _tag = System.Guid.NewGuid()

    let mutable _blinkTimer : DispatcherTimer = null

    /// Information about the caret, will be empty if the caret is not currently being displayed
    let mutable _caretData : option<CaretData> = None
    
    /// Does the consumer of IBlock caret want us to be in control of displaying the caret
    let mutable _isShown : bool = false

    [<DllImport("user32.dll")>]
    static let GetCaretBlinkTime() : int = failwith "Shouldn't fail because it's PInvoke"         

    do
        _view.LayoutChanged.AddHandler(new System.EventHandler<TextViewLayoutChangedEventArgs>(this.OnLayoutChanged))
        _view.Caret.PositionChanged.AddHandler(new System.EventHandler<CaretPositionChangedEventArgs>(this.OnCaretChanged))

        let caretBlinkTime = GetCaretBlinkTime()
        let caretBlinkTimeSpan = System.TimeSpan(0,0,0,0,caretBlinkTime)
        _blinkTimer <- new DispatcherTimer(
            caretBlinkTimeSpan,
            DispatcherPriority.Normal,
            new System.EventHandler(this.OnCaretBlinkTimer),
            Dispatcher.CurrentDispatcher)

    new (view:IWpfTextView,map) =
        let layer = view.GetAdornmentLayer("foo")
        BlockCaret(view, map, layer)

    new (view:IWpfTextView,adornmentLayerName:string,map:IEditorFormatMap) =
        let layer = view.GetAdornmentLayer(adornmentLayerName)
        let tag = System.Guid.NewGuid()
        BlockCaret(view, map, layer)

    /// Is the real caret visible in some way 
    member private x.IsRealCaretVisible = 
        let caret = _view.Caret
        let line = caret.ContainingTextViewLine
        line.VisibilityState <> VisibilityState.Unattached

    member private x.OnCaretBlinkTimer _ _ = 
        match _isShown,_caretData with
        | true,Some(data) -> 
            data.Image.Opacity <- 
                if data.Image.Opacity = 0.0 then _caretOpacity
                else 0.0
        | _ -> ()

    member x.DestroyCaret() =
        _layer.RemoveAdornmentsByTag(_tag)
        _caretData <- None

    member x.MaybeDestroyCaret() =
        if Option.isSome _caretData then
            x.DestroyCaret()

    /// Attempt to get the real caret color and copy it 
    member x.GetRealCaretBrushColor() = 
        let properties = _formatMap.GetProperties("Caret")
        let key = "ForegroundColor"
        if properties.Contains(key) then
            Some(properties.Item(key) :?> Color)
        else
            None

    member x.GetRealCaretVisualPoint() = Point(_view.Caret.Left, _view.Caret.Top)
    
    member x.NeedRecreateCaret() = 
        match _caretData with
        | None -> true
        | Some(data) ->
            data.Color <> x.GetRealCaretBrushColor() || data.Point <> _view.Caret.Position.BufferPosition

    member x.MoveCaretImageToCaret() =
        let point = x.GetRealCaretVisualPoint()
        let data = Option.get _caretData
        Canvas.SetLeft(data.Image, point.X)
        Canvas.SetTop(data.Image, point.Y)

    /// Get the size that should be used for the caret
    member x.GetOptimalCaretSize() =
        let caret = _view.Caret
        let line = caret.ContainingTextViewLine
        let defaultSize = 
            Size(
                float 5, 
                if line.IsValid then line.Height else float 10)
        if not x.IsRealCaretVisible then defaultSize
        else
            let point = caret.Position.BufferPosition
            let bounds = line.GetCharacterBounds(point)
            Size(bounds.Width,bounds.Height)

    member x.CreateCaretData() =
        let color = x.GetRealCaretBrushColor()
        let brush = 
            match color with 
            | Some(color) -> SolidColorBrush(color)
            | None -> SolidColorBrush(Colors.Black)
        brush.Freeze()
        let pen = Pen(brush, 1.0)

        let rect = Rect( x.GetRealCaretVisualPoint(), x.GetOptimalCaretSize())
        let geometry = RectangleGeometry(rect)
        let drawing = GeometryDrawing(brush, pen, geometry)
        drawing.Freeze()

        let drawingImage = DrawingImage(drawing)
        drawingImage.Freeze()

        let image = Image()
        image.Opacity <- _caretOpacity
        image.Source <- drawingImage

        let point = ViewUtil.GetCaretPoint _view
        let data = {Image=image;Color=color;Point=point}
        _caretData <- Some(data)
        _layer.AddAdornment(
            AdornmentPositioningBehavior.TextRelative,
            Nullable(SnapshotSpan(point,0)),
            _tag,
            image,
            new AdornmentRemovedCallback((fun _ _ -> _caretData <- None))) |> ignore
        x.MoveCaretImageToCaret()


    member private x.UpdateCaret() = 
        if _isShown then
            if not x.IsRealCaretVisible then
                x.MaybeDestroyCaret()
            elif x.NeedRecreateCaret() then
                x.MaybeDestroyCaret()
                x.CreateCaretData()
            else
                x.MoveCaretImageToCaret()

    member private x.OnLayoutChanged _ _ = x.UpdateCaret()
    member private x.OnCaretChanged _ _ = x.UpdateCaret()

    member private x.HideCore() =
        if _isShown then
            _isShown <- false
            _blinkTimer.IsEnabled <- false
            _view.Caret.IsHidden <- false
            x.DestroyCaret()

    member private x.ShowCore() =
        if not _isShown then
            _isShown <- true
            if x.IsRealCaretVisible then
                x.CreateCaretData()
            _blinkTimer.IsEnabled <- true
            _view.Caret.IsHidden <- true

    interface IBlockCaret with
        member x.TextView = _view :> ITextView
        member x.Hide() = x.HideCore()
        member x.Show() = x.ShowCore()
        member x.Destroy() =
            x.HideCore()
            _view.LayoutChanged.RemoveHandler(new System.EventHandler<TextViewLayoutChangedEventArgs>(x.OnLayoutChanged))
            _view.Caret.PositionChanged.RemoveHandler(new System.EventHandler<CaretPositionChangedEventArgs>(x.OnCaretChanged))


