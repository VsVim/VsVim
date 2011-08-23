#light
namespace Vim

type MarkMap =
    interface IMarkMap

    new : IBufferTrackingService -> MarkMap
