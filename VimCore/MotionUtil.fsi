#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

type internal MotionUtil =
    new : ITextView * IMarkMap * IVimLocalSettings * ISearchService * ITextStructureNavigator * IJumpList * IStatusUtil * IWordUtil * IVimData -> MotionUtil

    interface IMotionUtil
