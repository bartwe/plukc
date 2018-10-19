using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace Compiler.Binary
{
    // return param in eax (lsdw) edx(msdw)
    // all paramterers are int64 for cdecl conversions
    // expressions use eax, edx as output, expressions that take a parameter use eax, edx as input, except call, which takes everything on the stack
    // eax, edx and ecx or scratch registers
    public class AssemblerX86 : Compiler.Assembler
    {
        private int variables;
        private int parameters;
        private bool parameterMode = true;
        private Region region;
        private bool variablesFixed;
        private bool stackReturn;
        private int inExceptHandler = 0;

        public override Region Region { get { return region; } }

        public AssemblerX86(Region region, bool stackReturn)
        {
            if (region == null)
                throw new ArgumentNullException("writer");
            this.stackReturn = stackReturn;

            this.region = region;
        }

        public override void Break()
        {
            byte[] setup = new byte[] {
                0xcc // int3
            };
            region.Write(setup);
        }

        public override void StackRoot()
        {
            byte[] code = new byte[] {
                0x8b, 0xEc,// mov ebp, esp
                0x55,      // push ebp
                0x31, 0xED // xor ebp, ebp
            };
            region.Write(code);
        }

        public override void StartFunction()
        {
            variablesFixed = true;
            byte[] setup = new byte[] {
                0x55,         // push ebp
                0x8b, 0xec }; // mov ebp, esp
            region.Write(setup);
            if (variables > 0)
            {
                byte[] stackSkip = new byte[] {
                    0x31, 0xC0 }; // xor eax, eax
                region.Write(stackSkip);
                stackSkip = new byte[] {
                    0x50, 0x50 }; // push eax; push eax
                for (int i = 0; i < variables; ++i)
                    region.Write(stackSkip);
            }
        }

        public override void StopFunction()
        {
            if (inExceptHandler != 0)
                Require.Implementation("Returning from a function while still inside an exception context not implemented.");
            byte[] setup = new byte[] {
                0x8b, 0xe5, // mov esp, ebp
                0x5d };     // pop ebp
            region.Write(setup);

            if (parameters > 0)
            {
                int parametersSize = parameters * 8;
                region.WriteByte((byte)0xc2); // ret [IMMU16]
                region.WriteByte((byte)(parametersSize & 0xff));
                region.WriteByte((byte)((parametersSize >> 8) & 0xff));
            }
            else
                region.WriteByte((byte)0xc3); // ret
        }

        public override int AddParameter()
        {
            Require.False(variablesFixed);
            Require.True(parameterMode);
            return parameters++;
        }

        public override int AddVariable()
        {
            Require.False(variablesFixed);
            parameterMode = false;
            return parameters + (variables++);
        }

        public override int SlotCount()
        {
            return parameters + variables;
        }

        private int StackOffset(int slot)
        {
            if (slot < parameters)
                return (parameters - slot) * 8;
            else
                return ((parameters - slot) - 1) * 8;
        }

        public override void RetrieveVariable(int slot)
        {
            int lsdw = StackOffset(slot);
            int msdw = lsdw + 4;
            if ((sbyte.MaxValue >= lsdw) && (sbyte.MinValue <= lsdw))
            {
                region.Write(new byte[] { 0x8b, 0x45 }); // mov eax, [ebp +imms8]
                region.WriteInt8(lsdw);
            }
            else
            {
                region.Write(new byte[] { 0x8b, 0x85 }); // mov eax, [ebp +imms8]
                region.WriteInt32(lsdw);
            }
            if ((sbyte.MaxValue >= msdw) && (sbyte.MinValue <= msdw))
            {
                region.Write(new byte[] { 0x8b, 0x55 }); // mov edx, [ebp +imms8]
                region.WriteInt8(msdw);
            }
            else
            {
                region.Write(new byte[] { 0x8b, 0x95 }); // mov edx, [ebp +imms8]
                region.WriteInt32(msdw);
            }
        }

        public override void StoreVariable(int slot)
        {
            int lsdw = StackOffset(slot);
            int msdw = lsdw + 4;
            if ((sbyte.MaxValue >= lsdw) && (sbyte.MinValue <= lsdw))
            {
                region.Write(new byte[] { 0x89, 0x45 }); // mov [ebp +imms8], eax
                region.WriteInt8(lsdw);
            }
            else
            {
                region.Write(new byte[] { 0x89, 0x85 }); // mov [ebp +imms8], eax
                region.WriteInt32(lsdw);
            }
            if ((sbyte.MaxValue >= msdw) && (sbyte.MinValue <= msdw))
            {
                region.Write(new byte[] { 0x89, 0x55 }); // mov [ebp +imms8], edx
                region.WriteInt8(msdw);
            }
            else
            {
                region.Write(new byte[] { 0x89, 0x95 }); // mov [ebp +imms32], edx
                region.WriteInt32(msdw);
            }
        }

        public override void FetchMethod(int typeSlot)
        {
            int offset = typeSlot * 4;

            if ((sbyte.MaxValue >= offset) && (sbyte.MinValue <= offset))
            {
                region.Write(new byte[] { 0x8b, 0x52 }); // mov edx, [edx +imms8]
                region.WriteInt8(offset);
            }
            else
            {
                region.Write(new byte[] { 0x8b, 0x92 }); // mov edx, [edx +imms8]
                region.WriteInt32(offset);
            }
        }

        public override void TypeConversion(int typeSlot)
        {
            int offset = typeSlot * 4;
            if ((sbyte.MaxValue >= offset) && (sbyte.MinValue <= offset))
            {
                region.Write(new byte[] { 
                    0x21, 0xd2,  // and edx, edx
                    0x74, 0x03, // je +3
                    0x8b, 0x52  // mov edx, [edx +imms8]
                });
                region.WriteInt8(offset);
            }
            else
            {
                region.Write(new byte[] { 
                    0x21, 0xd2,  // and edx, edx
                    0x74, 0x06, // je +6
                    0x8b, 0x92  // mov edx, [edx +imms32]
                });
                region.WriteInt32(offset);
            }
        }

        public override void PushValue()
        {
            byte[] code = new byte[] {
                    0x52, // push edx
                    0x50, // push eax
            };
            region.Write(code);
        }

        public override void PopValue()
        {
            byte[] code = new byte[] {
                0x58, // pop eax
                0x5a // pop edx
            };
            region.Write(code);
        }

        public override void PeekValue(int depth)
        {
            int offset = depth * 8 + 4;
            Require.True((sbyte.MaxValue >= offset) && (sbyte.MinValue <= offset));
            byte[] code;
            unchecked
            {
                code = new byte[] {
                    0x8b, 0x44, 0x24, (byte)(offset-4),  //IMMS8 mov eax, [esp+IMMS8]
                    0x8b, 0x54, 0x24, (byte)(offset) //IMMS8 mov edx, [eax+0x14]
                };
            }
            region.Write(code);
        }

        public override void DropStackTop()
        {
            region.Write(new byte[] {
                0x59, 0x59 // pop ecx; pop ecx
            });
        }

        public override Placeholder CallFromStack(int parameterCount)
        {
            int offset = parameterCount * 8 + 4;
            Require.True((sbyte.MaxValue >= offset) && (sbyte.MinValue <= offset));
            byte[] code;
            unchecked
            {
                code = new byte[] {
                    0x8b, 0x44, 0x24, (byte)offset,  //IMMS8 mov eax, [esp+IMMS8]
                    0x8b, 0x50, 0x14, //IMMS8 mov edx, [eax+0x14]
                    0x89, 0x54, 0x24, (byte)offset, //IMMS8 mov [esp+IMMS8], edx
                    0xFF, 0x50, 0x10// call [eax+0x10]
                };
            }
            region.Write(code);
            return region.CurrentLocation;
        }

        public override Placeholder CallDirect(Placeholder function)
        {
            region.Write(new byte[] { 0xb9 });  // mov ecx, imm32
            region.WritePlaceholder(function);
            region.Write(new byte[] {
                    0xff, 0xd1, // call ecx
            });
            return region.CurrentLocation;
        }

        // only suited for static methods
        public override void LoadMethodStruct(Placeholder methodStruct)
        {
            Require.Assigned(methodStruct);
            byte[] code = new byte[] {
                0xba, // mov edx, IMM32
            };
            region.Write(code);
            region.WritePlaceholder(methodStruct);
            code = new byte[] {
                0x31, 0xC0,// xor eax, eax
            };
            region.Write(code);
        }

        public override void CallAllocator(Placeholder allocator, int size, Placeholder type)
        {
            Require.Assigned(allocator);
            Require.Assigned(type);
            // fake callframe
            Region.Write(new byte[] {
                0x55, // push ebp
                0x55, // push ebp
                0x89, 0xE5 // mov ebp, esp
            });
            region.Write(new byte[] { 0x55 }); // push ebp
            region.WriteByte(0x68); // push IMM32
            region.WriteInt32(0);
            region.WriteByte(0x68); // push IMM32
            region.WriteInt32(size);
            region.Write(new byte[] { 0xff, 0x15 });  // call [IMM32]
            region.WritePlaceholder(allocator);
            region.WriteByte(0xBA); // mov edx, IMM32
            region.WritePlaceholder(type);
            region.Write(new byte[] { 0x83, 0xC4, 0x10 }); // add esp, 0x10
            region.Write(new byte[] { 0x5d }); // pop ebp
        }


        public override void SetTypePart(Placeholder type)
        {
            Require.Assigned(type);
            region.WriteByte(0xBA); // mov edx, IMM32
            region.WritePlaceholder(type);
        }

        public override void PushValuePart()
        {
            byte[] code = new byte[] {
                    0x50, // push eax
            };
            region.Write(code);
        }

        public override void SetValue(Placeholder type, Placeholder value)
        {
            Require.Assigned(type);
            region.WriteByte(0xB8); // mov eax, IMM32
            region.WritePlaceholder(value);
            region.WriteByte(0xBA); // mov edx, IMM32
            region.WritePlaceholder(type);
        }

        public override void SetImmediateValue(Placeholder type, long value)
        {
            Require.Assigned(type);
            Require.True(value <= int.MaxValue);
            Require.True(value >= int.MinValue);
            region.WriteByte(0xB8); // mov eax, IMM32
            region.WriteInt32((int)value);
            region.WriteByte(0xBA); // mov edx, IMM32
            region.WritePlaceholder(type);
        }

        public override void SetOnlyValue(long value)
        {
            Require.True(value <= int.MaxValue);
            Require.True(value >= int.MinValue);
            byte[] code = new byte[] {
                0x31, 0xD2 // xor edx, edx
            };
            region.Write(code);
            region.WriteByte(0xB8); // mov eax, IMM32
            region.WriteInt32((int)value);
        }

        public override void Empty()
        {
            byte[] code = new byte[] {
                0x31, 0xC0,// xor eax, eax
                0x31, 0xD2 // xor edx, edx
            };
            region.Write(code);
        }

        public override void StoreInFieldOfSlot(Placeholder touch, int slot)
        {
            int offset = slot * 4;
            region.Write(new byte[] {
                    0x8B, 0x4c, 0x24, 0x04,     // mov ecx, [esp+4]
                });
            if ((sbyte.MaxValue >= offset) && (sbyte.MinValue <= offset))
            {
                region.Write(new byte[] {
                    0x8b, 0x49   // mov ecx, [ecx+imms8]
                });
                region.WriteInt8(offset);
            }
            else
            {
                region.Write(new byte[] {
                    0x8b, 0x89  // mov ecx, [ecx+imms32]
                });
                region.WriteInt32(offset);
            }
            region.Write(new byte[] {
                0x03, 0x0c, 0x24,           // add ecx, [esp]
                0x89, 0x01,                 // mov [ecx], eax
                0x89, 0x51, 0x04,           // mov [ecx+4], edx
                0x51, // push ecx
                0xff, 0x15 // call [IMM32]
            });
            region.WritePlaceholder(touch);
            region.Write(new byte[] {
                0x59,                       // pop ecx
                0x59,                       // pop ecx
                0x59                        // pop ecx
            });
        }

        public override void StoreInFieldOfSlotNoTouch(int slot)
        {
            int offset = slot * 4;
            region.Write(new byte[] { 0x8B, 0x4c, 0x24, 0x04 });     // mov ecx, [esp+4]
            if ((sbyte.MaxValue >= offset) && (sbyte.MinValue <= offset))
            {
                region.Write(new byte[] { 0x8b, 0x49 }); // mov ecx, [ecx+offset]
                region.WriteByte((byte)offset);
            }
            else
            {
                region.Write(new byte[] { 0x8b, 0x89 });
                region.WriteInt32(offset);
            }
            region.Write(new byte[] {
                0x03, 0x0c, 0x24,           // add ecx, [esp]
                0x89, 0x01,                 // mov [ecx], eax
                0x89, 0x51, 0x04,           // mov [ecx+4], edx
                0x59,                       // pop ecx
                0x59                        // pop ecx
            });
        }

        public override void FetchField(int valueSlot)
        {
            int offset = valueSlot * 4;
            if ((sbyte.MaxValue >= offset) && (sbyte.MinValue <= offset))
            {
                region.Write(new byte[] { 0x8b, 0x4A }); // mov ecx, [edx+offset]
                region.WriteByte((byte)offset);
            }
            else
            {
                region.Write(new byte[] { 0x8b, 0x8A });
                region.WriteInt32(offset);
            }
            region.Write(new byte[] {
                0x8B, 0x54, 0x01, 0x04,   // mov edx, [eax+ecx+4]
                0x8B, 0x04, 0x01,         // mov eax, [eax+ecx]
            });
        }

        public override Compiler.JumpToken CreateJumpToken()
        {
            return new JumpToken();
        }

        public override void SetDestination(Compiler.JumpToken token)
        {
            if (token == null)
                throw new ArgumentNullException("token");
            ((JumpToken)token).SetDestination(region.CurrentLocation);
        }

        public override void SetDestination(Compiler.PlaceholderRef token)
        {
            if (token == null)
                throw new ArgumentNullException("token");
            token.Placeholder = region.CurrentLocation;
        }

        public override void Jump(Compiler.JumpToken token)
        {
            JumpToken t = (JumpToken)token;
            t.SetKind(JumpTokenKind.Relative);
            region.WriteByte(0xe9); // relative offset next instruction
            t.SetJumpSite(region.InsertIntToken());
        }

        public override void JumpIfTrue(Compiler.JumpToken token)
        {
            JumpToken t = (JumpToken)token;
            t.SetKind(JumpTokenKind.Relative);
            region.Write(new byte[] {
                0x21, 0xc0, // and eax, eax
                0x0f, 0x85  // jnz 
            });
            t.SetJumpSite(region.InsertIntToken());
        }

        public override void JumpIfFalse(Compiler.JumpToken token)
        {
            JumpToken t = (JumpToken)token;
            t.SetKind(JumpTokenKind.Relative);
            region.Write(new byte[] {
                0x21, 0xc0, // and eax, eax
                0x0f, 0x84  // jz 
            });
            t.SetJumpSite(region.InsertIntToken());
        }

        public override void JumpIfAssigned(Compiler.JumpToken token)
        {
            JumpToken t = (JumpToken)token;
            t.SetKind(JumpTokenKind.Relative);
            region.Write(new byte[] {
                0x21, 0xd2, // and edx, edx
                0x0f, 0x85  // jnz 
            });
            t.SetJumpSite(region.InsertIntToken());
        }

        public override void JumpIfUnassigned(Compiler.JumpToken token)
        {
            JumpToken t = (JumpToken)token;
            t.SetKind(JumpTokenKind.Relative);
            region.Write(new byte[] {
                0x21, 0xd2, // and edx, edx
                0x0f, 0x84  // jz 
            });
            t.SetJumpSite(region.InsertIntToken());
        }

        public override void TypeConversionNotNull(int typeSlot)
        {
            int offset = typeSlot * 4;
            if ((sbyte.MaxValue >= offset) && (sbyte.MinValue <= offset))
            {
                region.Write(new byte[] { 0x8b, 0x52 }); // mov edx, [edx +imms8]
                region.WriteInt8(offset);
            }
            else
            {
                region.Write(new byte[] { 0x8b, 0x92 }); // mov edx, [edx +imms32]
                region.WriteInt32(offset);
            }
        }

        public override void Raw(byte[] code)
        {
            region.Write(code);
        }

        public override void BooleanNot()
        {
            byte[] code = new byte[] {
                0x83, 0xf0, 0x01 //  xor eax, 1
            };
            region.Write(code);
        }

        public override void IsNotNull()
        {
            JumpToken zeroJump = new JumpToken();
            zeroJump.SetKind(JumpTokenKind.Relative);
            region.Write(new byte[] {
                0x31, 0xC0,// xor eax, eax
                0x21, 0xd2, // and edx, edx
                0x0f, 0x84  // jz 
            });
            zeroJump.SetJumpSite(region.InsertIntToken());
            region.Write(new byte[] {
                0x83, 0xf0, 0x01 //  xor eax, 1
            });
            zeroJump.SetDestination(region.CurrentLocation);
        }

        public override void CallBuildIn(Placeholder indirectFunction, Placeholder[] arguments)
        {
            if (arguments == null)
                throw new ArgumentNullException("arguments");
            Require.Assigned(indirectFunction);
            foreach (Placeholder argument in arguments)
                Require.Assigned(argument);
            Array.Reverse(arguments);
            foreach (Placeholder argument in arguments)
            {
                region.WriteByte(0x68); // push IMM32
                region.WritePlaceholder(argument);
            }
            byte[] code = new byte[] {
                0xff, 0x15 // call [IMM32]
            };
            region.Write(code);
            region.WritePlaceholder(indirectFunction);
            if (arguments.Length > 0)
            {
                region.Write(new byte[] { 0x83, 0xC4 }); // add esp, IMM8
                region.WriteInt8(arguments.Length * 4);
            }
        }

        public override void JumpBuildIn(Placeholder indirectFunction)
        {
            byte[] code = new byte[] {
                0xff, 0x25 // jmp [IMM32]
            };
            region.Write(code);
            region.WritePlaceholder(indirectFunction);
        }

        public override void CallNative(Placeholder function, int argumentCount, bool stackFrame, bool trampoline)
        {
            if (stackReturn && !trampoline)
            {
                region.Write(new byte[] { 0x8d, 0x4c, 0x24 });  // lea ecx, [esp+imm8]
                region.WriteInt8((argumentCount + (stackFrame ? 1 : 0)) * 4);
                region.Write(new byte[] { 0x51 }); // push ecx
            }
            region.Write(new byte[] { 0xb9 });  // mov ecx, imm32
            region.WritePlaceholder(function);
            if (trampoline)
            {
                Require.False(stackFrame);
                Require.True(argumentCount == 0);
                region.Write(new byte[] { 0xff, 0x21 }); // jmp [ecx]
            }
            else
                region.Write(new byte[] { 0xff, 0x11 }); // call [ecx]
            if (argumentCount > 0)
            {
                region.Write(new byte[] { 0x83, 0xC4 }); // add esp, imm8
                region.WriteInt8((argumentCount + (stackFrame ? 1 : 0)) * 4);
            }
            if (stackReturn && !trampoline)
            {
                region.Write(new byte[] {
                    0x58, // pop eax
                    0x5a // pop edx
                });
            }
        }

        public override void SetupNativeStackFrameArgument(int argumentCount)
        {
            region.Write(new byte[] { 0x55 }); // push ebp
        }

        public override void SetNativeArgument(int slot, int index, int count)
        {
            // reverse arguments for c
            index = count - index - 1;
            int lsdw = StackOffset(slot);
            int msdw = lsdw + (region.Is64Bit ? 8 : 4);
            PushEbpImm(msdw);
            PushEbpImm(lsdw);
        }

        private void PushEbpImm(int value)
        {
            if ((value >= -128) && (value <= 127))
            {
                region.Write(new byte[] { 0xff, 0x75 }); // push [ebp + IMM8]
                region.WriteInt8(value);
            }
            else
            {
                region.Write(new byte[] { 0xff, 0xb5 }); // push [ebp + IMM32]
                region.WriteInt32(value);
            }
        }

        public override void PopNativeArgument()
        {
        }

        public override void SetupNativeReturnSpace()
        {
            if (stackReturn)
            {
                region.Write(new byte[] { 
                    0x31, 0xc9, // xor ecx, ecx
                    0x51, 0x51 // push ecx; push ecx
                });
            }
        }

        public override void CrashIfNull()
        {
            region.Write(new byte[] { 0x8b, 0x0a }); // mov ecx, [edx]
        }

        public override void IntegerNegate()
        {
            region.Write(new byte[] { 0xf7, 0xd8 }); // neg eax
        }

        public override void IntegerEquals()
        {
            IntegerCompare(0x75);
        }

        public override void IntegerNotEquals()
        {
            IntegerCompare(0x74);
        }

        public override void IntegerGreaterThan()
        {
            IntegerCompare(0x7e);
        }

        public override void IntegerLessThan()
        {
            IntegerCompare(0x7d);
        }

        public override void IntegerGreaterEquals()
        {
            IntegerCompare(0x7c);
        }

        public override void IntegerLessEquals()
        {
            IntegerCompare(0x7f);
        }

        private void IntegerCompare(byte op)
        {
            //compare the value in the accumulator with the first value on the stack, ignores the type part
            region.Write(new byte[] {
                0x89, 0xc2, // mov edx, eax
                0x31, 0xc0, // xor eax, eax
                0x59, // pop ecx (eax)
                0x39, 0xd1, //cmp ecx, edx
            });
            region.WriteByte(op);
            region.Write(new byte[] {
                0x03, // j* +3
                0x83, 0xf0, 0x01, // xor eax, 1
                0x59, // pop ecx (edx)
            });
        }

        public override void IntegerAdd()
        {
            region.Write(new byte[] {
                0x89, 0xc2, // mov edx, eax
                0x58, // pop eax
                0x01, 0xd0, // add eax, edx
                0x5a // pop edx
            });
        }

        public override Placeholder CheckOverflow(Placeholder overflowException)
        {
            region.Write(new byte[] {
                    0x71, 0x06, // jno +6
                    0xff, 0x15 // call [IMM32]
                });
            region.WritePlaceholder(overflowException);
            return region.CurrentLocation;
        }

        public override void IntegerSubtract()
        {
            region.Write(new byte[] {
                0x89, 0xc2, // mov edx, eax
                0x58, // pop eax
                0x29, 0xd0, // sub eax, edx
                0x5a // pop edx
            });
        }

        public override void IntegerMultiply()
        {
            region.Write(new byte[] {
                0x89, 0xc2, // mov edx, eax
                0x58, // pop eax
                0xf7, 0xea, // imul edx (eax implicit)
                0x5a // pop edx
            });
        }

        public override void IntegerDivide()
        {
            region.Write(new byte[] {
                0x89, 0xc1, // mov ecx, eax
                0x58, // pop eax
                0x99, // cltd
                0xf7, 0xf9, // idiv %ecx (edx:eax implicit)
                0x5a // pop edx
            });
        }

        public override void IntegerModulo()
        {
            region.Write(new byte[] {
                0x89, 0xc1, // mov ecx, eax
                0x58, // pop eax
                0x99, // cltd
                0xf7, 0xf9, // idiv %ecx (edx:eax implicit)
                0x89, 0xd0, // mov eax, edx
                0x5a // pop edx
            });
        }

        public override void IntegerLeft()
        {
            region.Write(new byte[] {
                0x89, 0xc1, // mov ecx, eax
                0x58, // pop eax
                0xd3, 0xe0, // sal eax, cl
                0x5a // pop edx
            });
        }

        public override void IntegerRight()
        {
            region.Write(new byte[] {
                0x89, 0xc1, // mov ecx, eax
                0x58, // pop eax
                0xd3, 0xf8, // sar eax, cl
                0x5a // pop edx
            });
        }

        public override void ArrayFetchByte()
        {
            region.Write(new byte[] {
                0x89, 0xc2, // mov edx, eax
                0x58, // pop eax
                0x8b, 0x00, // mov eax, [eax]
                0x8a, 0x04, 0x02, // mov al, [eax+edx]
                0x0f, 0xb6, 0xc0, // movzx eax, al
                0x5a // pop edx
            });
        }

        public override void ArrayStoreByte()
        {
            region.Write(new byte[] {
                0x89, 0xc1, // mov ecx, eax
                0x58, // pop eax
                0x5a, // pop edx
                0x89, 0xc2, // mov edx, eax
                0x58, // pop eax
                0x8b, 0x00, // mov eax, [eax]
                0x88, 0x0c, 0x02, // mov [eax+edx], cl
                0x5a // pop edx
            });
        }


        public override void ArrayFetchInt()
        {
            region.Write(new byte[] {
                0x89, 0xc2, // mov edx, eax
                0x58, // pop eax
                0x8b, 0x00, // mov eax, [eax]
                0x8b, 0x04, 0x90, // mov eax, [eax+edx*4]
                0x5a // pop edx
            });
        }

        public override void ArrayStoreInt()
        {
            region.Write(new byte[] {
                0x89, 0xc1, // mov ecx, eax
                0x58, // pop eax
                0x5a, // pop edx
                0x89, 0xc2, // mov edx, eax
                0x58, // pop eax
                0x8b, 0x00, // mov eax, [eax]
                0x89, 0x0c, 0x90, // mov [eax+edx*4], ecx
                0x5a // pop edx
            });
        }
        public override void ExceptionHandlerSetup(PlaceholderRef site)
        {
            inExceptHandler++;
            region.Write(new byte[] { 
                0x31, 0xc9, // xor ecx, ecx
                0x51 // push ecx
            });
            region.Write(new byte[] { 0x68 }); // push imm32
            region.WritePlaceholderRef(site);
            region.Write(new byte[] { 
                0x51, // push ecx
                0xff, 0x75, 0x00, // push [ebp]
                0x89, 0x65, 0x00 // mov [ebp], esp
            });
        }

        public override void ExceptionHandlerRemove()
        {
            inExceptHandler--;
            region.Write(new byte[] {
                0x8f, 0x45, 0x00, // pop [rbp]
                0x59, 0x59, 0x59 // pop rcx; pop rcx; pop rcx
            });
        }

        public override void ExceptionHandlerInvoke()
        {
            region.Write(new byte[] {
                0x8b, 0x4d, 0x00,  //  a:       mov    0x0(%ebp),%ecx
                0x8b, 0x49, 0x04,  //           mov    0x4(%ecx),%ecx 
                0x85, 0xc9,        //           test   %ecx,%ecx
                0x74, 0x05,        //  	        je     b:
                0x8b, 0x6d, 0x00,  //           mov    0x0(%ebp),%ebp
                0xeb, 0xf1,        //           jmp    a:
                0x8b, 0x4d, 0x00,  //  b:       mov    0x0(%ebp),%ecx
                0x89, 0xcc,        //           mov    %ecx,%esp
                0x8f, 0x45, 0x00,  //           popq   0x0(%ebp)
                0x8b, 0x4c, 0x24, 0x04, //      mov    0x4(%esp),%ecx
                0x83, 0xc4, 0x0c, //            add    $0xc,%esp
                0xff, 0xe1 //                   jmp    *%ecx
            });
        }

        public override void TypeConversionDynamicNotNull(long typeId)
        {
            byte[] code = new byte[] {
                    0x8B, 0x4A, 0x04,// mov ecx, [edx+4]
                    0x52, // push edx
                    0x50, // push eax
                    0x31, 0xD2, // xor edx, edx
                    0xB8, // mov eax, IMM32
            };
            region.Write(code);
            region.WriteInt32((int)typeId);
            code = new byte[] {
                    0x52, // push edx
                    0x50, // push eax
                    0xff, 0xd1, // call ecx
            };
            region.Write(code);
        }


        public override void Load(Placeholder location)
        {
            region.Write(new byte[] { 0xb9 });  // mov ecx, imm32
            region.WritePlaceholder(location);
            region.Write(new byte[] {
                0x8b, 0x01,                 // mov eax, [ecx]
                0x8b, 0x51, 0x04            // mov edx, [ecx+4]
            });
        }

        public override void Store(Placeholder location)
        {
            region.Write(new byte[] { 0xb9 });  // mov ecx, imm32
            region.WritePlaceholder(location);
            region.Write(new byte[] {
                0x89, 0x01,                 // mov [ecx], eax
                0x89, 0x51, 0x04            // mov [ecx+4], edx
            });
        }

        public override void SetupFpu()
        {
            region.Write(
                            new byte[] {
                                0xDB, 0xE2, //fclex
                                0xB8, 0x3f, 0x13, 0x00, 0x00, // mov eax, $133f
                                0x50, // push eax
                                0x8d, 0x04, 0x24, // lea eax, [esp]
                                0xd9, 0x28, // fldcw [eax]
                                0x58, // pop eax
                            });
        }

        public override void MarkType()
        {
            region.Write(new byte[] {
                0x83, 0xCA, 0x01 // or edx, 1
            });
        }

        public override void UnmarkType()
        {
            region.Write(new byte[] {
                0x83, 0xE2, 0xFE // and edx, -2
            });
        }

        public override void JumpIfNotMarked(Compiler.JumpToken token)
        {
            region.Write(new byte[] {
                0xF7, 0xC2, 0x01, 0x00, 0x00, 0x00 // test edx, 1
            });
            JumpToken t = (JumpToken)token;
            t.SetKind(JumpTokenKind.Relative);
            region.Write(new byte[] {
                0x0f, 0x84  // jz  
            });
            t.SetJumpSite(region.InsertIntToken());
        }
    }
}
