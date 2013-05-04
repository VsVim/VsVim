#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

type internal MotionUtil =
    new : IVimBufferData * ICommonOperations -> MotionUtil

    interface IMotionUtil
