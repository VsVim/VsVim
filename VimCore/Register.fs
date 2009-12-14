#light

namespace Vim

/// Value stored in the register.  Contains contextual information on how the data
/// was yanked from the buffer
type RegisterValue = {
    Value : string;
    MotionKind : MotionKind;
    OperationKind : OperationKind;
}

type Register(_name:char) = 
    let mutable _value = { Value=System.String.Empty; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.CharacterWise}
    member x.Name = _name
    member x.StringValue = _value.Value
    member x.Value = _value
    member x.UpdateValue v = _value <- v

