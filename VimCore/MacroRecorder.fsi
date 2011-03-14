
namespace Vim

type internal MacroRecorder =
    interface IMacroRecorder
    interface IVimBufferCreationListener

    new : IRegisterMap -> MacroRecorder
