#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Tagging

type internal AdhocOutliner = 
    interface IAdhocOutliner
    interface IBasicTaggerSource<OutliningRegionTag>
    new : ITextBuffer -> AdhocOutliner
