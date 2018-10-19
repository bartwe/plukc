using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public class LambdaAssembler : Assembler
    {
        private Assembler code;
        private LambdaExpression expression;

        public LambdaAssembler(LambdaExpression expression, Assembler code)
        {
            this.expression = expression;
            this.code = code;
        }

        public override int AddVariable()
        {
            return expression.AddVariable(code.AddVariable());
        }

        public override int SlotCount()
        {
            return expression.SlotCount();
        }

        public override void RetrieveVariable(int slot)
        {
            if (expression.IsClosureSlot(slot))
            {
                int thisSlot = expression.LocalSlot(expression.ClosureSlot);
                code.RetrieveVariable(thisSlot);
                int field = expression.ClosureField(slot);
                code.FetchField(field);
            }
            else
            {
                int actualSlot = expression.LocalSlot(slot);
                code.RetrieveVariable(actualSlot);
            }
        }

        public override void StoreVariable(int slot)
        {
            if (expression.IsClosureSlot(slot))
            {
                Require.NotCalled();
            }
            else
            {
                int actualSlot = expression.LocalSlot(slot);
                code.StoreVariable(actualSlot);
            }
        }

        public override void SetNativeArgument(int slot, int index, int count)
        {
            Require.Implementation("SetNativeArgument");
        }

        public override int AddParameter() { Require.NotCalled(); return 0; }

        public override Region Region { get { return code.Region; } }
        public override void StackRoot() { Require.NotCalled(); }
        public override void StartFunction() { code.StartFunction(); }
        public override void StopFunction() { code.StopFunction(); }
        public override void FetchField(int valueSlot) { code.FetchField(valueSlot); }
        public override void FetchMethod(int typeSlot) { code.FetchMethod(typeSlot); }
        public override void PushValue() { code.PushValue(); }
        public override void PopValue() { code.PopValue(); }
        public override void PeekValue(int depth) { code.PeekValue(depth); }
        public override void DropStackTop() { code.DropStackTop(); }
        public override Placeholder CallFromStack(int parameterCount) { return code.CallFromStack(parameterCount); }
        public override Placeholder CallDirect(Placeholder function) { return code.CallDirect(function); }
        public override void LoadMethodStruct(Placeholder methodStruct) { code.LoadMethodStruct(methodStruct); }
        public override void CallAllocator(Placeholder allocator, int size, Placeholder type)
        { code.CallAllocator(allocator, size, type); }
        public override void Empty() { code.Empty(); }
        public override void StoreInFieldOfSlot(Placeholder touch, int slot) { code.StoreInFieldOfSlot(touch, slot); }
        public override void StoreInFieldOfSlotNoTouch(int slot) { code.StoreInFieldOfSlotNoTouch(slot); }
        public override void SetValue(Placeholder type, Placeholder value) { code.SetValue(type, value); }
        public override void SetImmediateValue(Placeholder type, long value) { code.SetImmediateValue(type, value); }
        public override void SetOnlyValue(long value) { code.SetOnlyValue(value); }
        public override void Break() { code.Break(); }
        public override void Jump(JumpToken token) { code.Jump(token); }
        public override void JumpIfTrue(JumpToken token) { code.JumpIfTrue(token); }
        public override void JumpIfFalse(JumpToken token) { code.JumpIfFalse(token); }
        public override void JumpIfAssigned(JumpToken token) { code.JumpIfAssigned(token); }
        public override void JumpIfUnassigned(JumpToken token) { code.JumpIfUnassigned(token); }
        public override JumpToken CreateJumpToken() { return code.CreateJumpToken(); }
        public override void SetDestination(JumpToken token) { code.SetDestination(token); }
        public override void SetDestination(PlaceholderRef place) { code.SetDestination(place); }
        public override void CallBuildIn(Placeholder indirectFunction, Placeholder[] arguments)
        { code.CallBuildIn(indirectFunction, arguments); }
        public override void TypeConversion(int typeSlot) { code.TypeConversion(typeSlot); }
        public override void TypeConversionNotNull(int typeSlot) { code.TypeConversionNotNull(typeSlot); }
        public override void TypeConversionDynamicNotNull(long typeId) { code.TypeConversionDynamicNotNull(typeId); }
        public override void Raw(byte[] code) { this.code.Raw(code); }
        public override void BooleanNot() { code.BooleanNot(); }
        public override void SetTypePart(Placeholder type) { code.SetTypePart(type); }
        public override void PushValuePart() { code.PushValuePart(); }
        public override void IsNotNull() { code.IsNotNull(); }
        public override void SetupNativeReturnSpace() { code.SetupNativeReturnSpace(); }
        public override void SetupNativeStackFrameArgument(int argumentCount) { code.SetupNativeStackFrameArgument(argumentCount); }
        public override void CallNative(Placeholder function, int argumentCount, bool stackFrame, bool trampoline) { code.CallNative(function, argumentCount, stackFrame, trampoline); }
        public override void PopNativeArgument() { code.PopNativeArgument(); }
        public override void CrashIfNull() { code.CrashIfNull(); }
        public override void IntegerNegate() { code.IntegerNegate(); }
        public override void IntegerEquals() { code.IntegerEquals(); }
        public override void IntegerNotEquals() { code.IntegerNotEquals(); }
        public override void IntegerGreaterThan() { code.IntegerGreaterThan(); }
        public override void IntegerLessThan() { code.IntegerLessThan(); }
        public override void IntegerGreaterEquals() { code.IntegerGreaterEquals(); }
        public override void IntegerLessEquals() { code.IntegerLessEquals(); }
        public override Placeholder CheckOverflow(Placeholder overflowException) { return code.CheckOverflow(overflowException); }
        public override void IntegerAdd() { code.IntegerAdd(); }
        public override void IntegerSubtract() { code.IntegerSubtract(); }
        public override void IntegerLeft() { code.IntegerLeft(); }
        public override void IntegerRight() { code.IntegerRight(); }
        public override void IntegerMultiply() { code.IntegerMultiply(); }
        public override void IntegerDivide() { code.IntegerDivide(); }
        public override void IntegerModulo() { code.IntegerModulo(); }
        public override void ArrayFetchByte() { code.ArrayFetchByte(); }
        public override void ArrayStoreByte() { code.ArrayStoreByte(); }
        public override void ArrayFetchInt() { code.ArrayFetchInt(); }
        public override void ArrayStoreInt() { code.ArrayStoreInt(); }
        public override void ExceptionHandlerSetup(PlaceholderRef site) { code.ExceptionHandlerSetup(site); }
        public override void ExceptionHandlerRemove() { code.ExceptionHandlerRemove(); }
        public override void ExceptionHandlerInvoke() { code.ExceptionHandlerInvoke(); }
        public override void Load(Placeholder location) { code.Load(location); }
        public override void Store(Placeholder location) { code.Store(location); }
        public override void JumpBuildIn(Placeholder location) { code.JumpBuildIn(location); }
        public override void SetupFpu() { code.SetupFpu(); }
        public override void MarkType() { code.MarkType(); }
        public override void JumpIfNotMarked(Compiler.JumpToken token) { code.JumpIfNotMarked(token); }
        public override void UnmarkType() { code.UnmarkType(); }
    }
}
