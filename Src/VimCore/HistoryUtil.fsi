namespace Vim


/// Begin a history related operation

[<Class>]
type internal HistoryUtil =
    static member CreateHistorySession: IHistoryClient<'TData, 'TResult>
         -> initialData:'TData
         -> editableCommand:EditableCommand
         -> localAbbreviationMap:IVimLocalAbbreviationMap -> motionUtil:IMotionUtil -> IHistorySession<'TData, 'TResult>

    /// The set of KeyInput values which history considers to be a valid command
    static member CommandNames: KeyInput list
