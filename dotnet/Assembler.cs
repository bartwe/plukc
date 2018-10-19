using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler
{
    /// <summary>
    /// Used to generate machine code for a specific architecture.
    /// Some general assumptions are made with this interface
    /// probably there are some x86-isms but i tried to avoid getting to specific i hope
    /// the result of the previous operation (if any) is kept for the next operation, in the Accumulator (not necesarily in a register, it can also be the top of the stack or something else)
    /// The assembler is used for a single function block
    /// </summary>
    public abstract class Assembler
    {
        /// <summary>
        /// Region where the Assembler puts its code
        /// </summary>
        public abstract Region Region { get; }

        /// <summary>
        /// Adds a parameter to the function that follows.
        /// Places the new parameter to the right of the previous one.
        /// </summary>
        /// <returns>slot used to address this slot from inside the function.</returns>
        public abstract int AddParameter();

        /// <summary>
        /// Adds a parameter to the function that follows.
        /// </summary>
        /// <returns>slot used to address this slot from inside the function.</returns>
        public abstract int AddVariable();

        /// <summary>
        /// Returns a number that is higher than the highest variable and parameter slot.
        /// </summary>
        public abstract int SlotCount();

        /// <summary>
        /// Prepares the stack and context for stacktraces and garbage collection.
        /// </summary>
        public abstract void StackRoot();

        /// <summary>
        /// Generates the preamble of the function.
        /// Includes stack frame and reserving stack for the variables
        /// </summary>
        public abstract void StartFunction();

        /// <summary>
        /// Generate the postamble of the function.
        /// </summary>
        public abstract void StopFunction();

        /// <summary>
        /// Retrieves the value in the slot slot and puts it in the Accumulator.
        /// </summary>
        /// <param name="slot">Id of the slot on the stack to use.</param>
        public abstract void RetrieveVariable(int slot);

        /// <summary>
        /// Stores the value in the Accumulator in the slot slot.
        /// </summary>
        /// <param name="slot">Id of the slot on the stack to use.</param>
        public abstract void StoreVariable(int slot);

        /// <summary>
        /// Fetches the value from the nth slot of the current value in the Accumulator, and places the result into the Accumulator
        /// </summary>
        /// <param name="valueSlot">Id of the slot on the value to use.</param>
        public abstract void FetchField(int valueSlot);

        /// <summary>
        /// Fetches the value from the nth type slot of the current value in the Accumulator,
        /// and places the result in the type part of the current Accumulator, preserving the valuepart
        /// </summary>
        /// <param name="typeSlot">Id of the slot on the type to use.</param>
        public abstract void FetchMethod(int typeSlot);

        /// <summary>
        /// Pushes the value in the Accumulator unto the stack.
        /// Used to prepare for a function call, or for field assignment.
        /// </summary>
        public abstract void PushValue();

        public abstract void PopValue();

        public abstract void PeekValue(int depth);

        public abstract void DropStackTop();

        /// <summary>
        /// Calls the method that was pushed as the zeroth argument.
        /// Arguments were pushed left to right.
        /// The zeroth argument is converted back to the type backing the method type.
        /// </summary>
        /// <param name="parameterCount">one less than the number of arguments (zero is the this parameter)</param>
        public abstract Placeholder CallFromStack(int parameterCount);

        /// <summary>
        /// Calls the method which start position was passed.
        /// Arguments were pushed left to right.
        /// </summary>
        /// <param name="function">location of the start of the function</param>
        public abstract Placeholder CallDirect(Placeholder function);

        /// <summary>
        /// Places the methodStruct as the type part of the Accumulator.
        /// The value part of the Accumulator is cleared.
        /// Only suited for static methods.
        /// </summary>
        /// <param name="methodStruct">Pointer placeholder to a method type struct.</param>
        public abstract void LoadMethodStruct(Placeholder methodStruct);

        /// <summary>
        /// Generates code that calls the allocator with the desired size and the places the memory as the value part in the Accumulator
        /// the type part of the Accumulator is filled with the type pointer placeholder.
        /// </summary>
        /// <param name="allocator">Placeholder to the allocator implementation</param>
        /// <param name="size">Size in bytes to allocate</param>
        /// <param name="type">Placeholder to put in the type part of the new value in the Accumulator</param>
        public abstract void CallAllocator(Placeholder allocator, int size, Placeholder type);

        /// <summary>
        /// Explicitly marks the content of the Accumulator as empty/null.
        /// Usefull for gc purposes, but mostly used for null literals.
        /// </summary>
        public abstract void Empty();

        /// <summary>
        /// Stores the Accumulator into a field of the top of the stack.
        /// </summary>
        /// <param name="touch">Touch function of the garbage collector.</param>
        /// <param name="slot">Field number of the slot in Accumulator</param>
        public abstract void StoreInFieldOfSlot(Placeholder touch, int slot);

        /// <summary>
        /// Stores the Accumulator into a field of the top of the stack.
        /// This version should only be used for types that are not garage collected.
        /// </summary>
        /// <param name="slot">Field number of the slot in Accumulator</param>
        public abstract void StoreInFieldOfSlotNoTouch(int slot);

        /// <summary>
        /// Stores the literal object value in the accumulator
        /// </summary>
        /// <param name="type">Placeholder of the type of the value</param>
        /// <param name="value">Placeholder of the value</param>
        public abstract void SetValue(Placeholder type, Placeholder value);

        /// <summary>
        /// Stores the literal object value in the accumulator
        /// </summary>
        /// <param name="type">Placeholder of the type of the value</param>
        /// <param name="value">Value of the value</param>
        public abstract void SetImmediateValue(Placeholder type, long value);

        public abstract void SetOnlyValue(long value);


        /// <summary>
        /// Generates a runtime breakpoint for debugging purposes.
        /// </summary>
        public abstract void Break();

        /// <summary>
        /// Inserts an unconditional jump to the location described by the jump token.
        /// This may be a forward or backward jump, but should be local to the Assembler.
        /// </summary>
        public abstract void Jump(JumpToken token);

        /// <summary>
        /// Conditional version of Jump().
        /// Reads a boolean value from the accumulator
        /// </summary>
        public abstract void JumpIfTrue(JumpToken token);

        /// <summary>
        /// Conditional version of Jump().
        /// Reads a boolean value from the accumulator
        /// </summary>
        public abstract void JumpIfFalse(JumpToken token);

        /// <summary>
        /// Conditional version of Jump().
        /// Takes the jump if the type part of the accumulator is empty
        /// </summary>
        public abstract void JumpIfAssigned(JumpToken token);

        /// <summary>
        /// Conditional version of Jump().
        /// Takes the jump if the type part of the accumulator is empty
        /// </summary>
        public abstract void JumpIfUnassigned(JumpToken token);

        /// <summary>
        /// Creates a token that can be used for jumps.
        /// </summary>
        public abstract JumpToken CreateJumpToken();

        /// <summary>
        /// Assosiates a JumpToken with a location in the code stream.
        /// </summary>
        public abstract void SetDestination(JumpToken token);
        public abstract void SetDestination(PlaceholderRef place);

        /// <summary>
        /// Does a normal stdcall to the pointer to function pointer, with the arguments in normal order ont he stack (not pluk order)
        /// </summary>
        /// <param name="indirectFunction">pointer to function pointer to call</param>
        /// <param name="arguments">array of arguments</param>
        public abstract void CallBuildIn(Placeholder indirectFunction, Placeholder[] arguments);

        /// <summary>
        /// Converts the value in the accumulator using the type slot offset on the value.
        /// Does nothing for null reference.
        /// Only used for down casts, upcasts requires a more dynamic approach to finding the type runtimestruct.
        /// </summary>
        /// <param name="typeSlot">offset in the type runtimestructure containing the type that we are converting to.</param>
        public abstract void TypeConversion(int typeSlot);

        /// <summary>
        /// Converts the value in the accumulator using the type slot offset on the value.
        /// Only used for down casts, upcasts requires a more dynamic approach to finding the type runtimestruct.
        /// </summary>
        /// <param name="typeSlot">offset in the type runtimestructure containing the type that we are converting to.</param>
        public abstract void TypeConversionNotNull(int typeSlot);

        /// <summary>
        /// Converts the value in the accumulator using the its cast functionality and the provided typeName id.
        /// If the typeName is not supported the accumulator is cleared.
        /// </summary>
        /// <param name="typeId">Id of the typeName to convert to.</param>
        public abstract void TypeConversionDynamicNotNull(long typeId);

        /// <summary>
        /// Writes the supplied opcodes into the binary, obviously non portable and all that.
        /// </summary>
        /// <param name="code"></param>
        public abstract void Raw(byte[] code);

        /// <summary>
        /// Treat the value in the accumulator as a boolean and invert it.
        /// </summary>
        public abstract void BooleanNot();

        /// <summary>
        /// Overwrites the type part of the accumulator.
        /// Usefull for callspec conversions.
        /// </summary>
        /// <param name="type"></param>
        //TODO: change to put the literal/pointer in de data section
        public abstract void SetTypePart(Placeholder type);

        /// <summary>
        /// Pushes the value part of the accumulator.
        /// Usefull for callspec conversions.
        /// </summary>
        public abstract void PushValuePart();

        /// <summary>
        /// Tests the content of the accumulator for nullness and puts the result as a boolean in the accumulator.
        /// </summary>
        public abstract void IsNotNull();

        /// <summary>
        /// Some calling conventions, specificly cdecl on 32bit linux uses a register based storage unit if the result doesn't fit in a single register
        /// </summary>
        public abstract void SetupNativeReturnSpace();

        public abstract void SetupNativeStackFrameArgument(int argumentCount);

        public abstract void CallNative(Placeholder function, int argumentCount, bool stackFrame, bool trampoline);

        public abstract void SetNativeArgument(int slot, int index, int count);

        public abstract void PopNativeArgument();

        public abstract void CrashIfNull();

        public abstract void IntegerNegate();

        public abstract void IntegerEquals();

        public abstract void IntegerNotEquals();

        public abstract void IntegerGreaterThan();

        public abstract void IntegerLessThan();

        public abstract void IntegerGreaterEquals();

        public abstract void IntegerLessEquals();

        public abstract Placeholder CheckOverflow(Placeholder overflowException);

        public abstract void IntegerAdd();

        public abstract void IntegerSubtract();

        public abstract void IntegerLeft();

        public abstract void IntegerRight();
        
        public abstract void IntegerMultiply();

        public abstract void IntegerDivide();

        public abstract void IntegerModulo();
        
        public abstract void ArrayFetchByte();

        public abstract void ArrayStoreByte();

        public abstract void ArrayFetchInt();

        public abstract void ArrayStoreInt();

        public abstract void ExceptionHandlerSetup(PlaceholderRef site);

        public abstract void ExceptionHandlerRemove();

        public abstract void ExceptionHandlerInvoke();

        /// <summary>
        /// Load the accumulator with the value at the placeholder
        /// </summary>
        public abstract void Load(Placeholder location);

        /// <summary>
        /// Store the accumulator into the value at the placeholder
        /// </summary>
        public abstract void Store(Placeholder location);

        /// <summary>
        /// Jumps to the location stored in the placeholder.
        /// </summary>
        /// <param name="location"></param>
        public abstract void JumpBuildIn(Placeholder location);

        public abstract void SetupFpu();

        // used to mark the lowbit of the type (which is zero due to allignment) so that the exception mechanism can be used when returning a value from inside a try block
        public abstract void MarkType();
        public abstract void UnmarkType();
        public abstract void JumpIfNotMarked(JumpToken token);
    }
}
