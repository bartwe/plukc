using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace Compiler.Binary
{
    public class AssemblerX86_64 : Compiler.Assembler
    {
        private Region region;

        private int variables;
        private int parameters;
        private bool parameterMode = true;
        private bool variablesFixed;

        public override Region Region { get { return region; } }

        public AssemblerX86_64(Region region)
        {
            Require.Assigned(region);
            this.region = region;
        }

        /// <summary>
        /// Adds a parameter to the function that follows.
        /// Places the new parameter to the right of the previous one.
        /// </summary>
        /// <returns>slot used to address this slot from inside the function.</returns>
        public override int AddParameter()
        {
            Require.False(variablesFixed);
            Require.True(parameterMode);
            return parameters++;
        }

        /// <summary>
        /// Adds a parameter to the function that follows.
        /// </summary>
        /// <returns>slot used to address this slot from inside the function.</returns>
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

        /// <summary>
        /// Prepares the stack and context for stacktraces and garbage collection.
        /// </summary>
        public override void StackRoot()
        {
            Region.Write(new byte[] { 
                0x48, 0x31, 0xED, //    xor rbp, rbp
                0x48, 0x89, 0xe7  //    mov rdi, rsp
            });
        }

        /// <summary>
        /// Generates the preamble of the function.
        /// Includes stack frame and reserving stack for the variables
        /// </summary>
        public override void StartFunction()
        {
            variablesFixed = true;
            Region.Write(new byte[] {
                0x55, // push rbp
                0x48, 0x89, 0xE5 // mov rbp, rsp
            });

            if (variables > 0)
            {
                Region.Write(new byte[] { 0x48, 0x31, 0xC0 }); // xor rax, rax
                for (int i = 0; i < variables; ++i)
                    Region.Write(new byte[] { 0x50, 0x50 }); // push rax; push rax
            }
        }

        /// <summary>
        /// Tests the content of the accumulator for nullness and puts the result as a boolean in the accumulator.
        /// Note: the type part of the boolean is missing.
        /// </summary>
        public override void IsNotNull()
        {
            JumpToken zeroJump = new JumpToken();
            zeroJump.SetKind(JumpTokenKind.Relative);
            region.Write(new byte[] {
                0x48, 0x31, 0xC0,// xor rax, rax
                0x48, 0x21, 0xd2, // and rdx, rdx
                0x0f, 0x84  // jz 
            });
            zeroJump.SetJumpSite(region.InsertIntToken());
            region.Write(new byte[] {
                0x48, 0x83, 0xf0, 0x01 // xor rax, 1
            });
            zeroJump.SetDestination(region.CurrentLocation);
        }
        /// <summary>
        /// Pushes the value part of the accumulator.
        /// Usefull for spec conversions.
        /// </summary>
        public override void PushValuePart()
        {
            byte[] code = new byte[] {
                    0x50, // push rax
            };
            region.Write(code);
        }

        /// <summary>
        /// Overwrites the type part of the accumulator.
        /// Usefull for callspec conversions.
        /// </summary>
        /// <param name="type"></param>
        public override void SetTypePart(Placeholder type)
        {
            Require.Assigned(type);
            region.Write(new byte[] { 0x48, 0x8d, 0x15 }); // lea rdx, [rip+disp]
            region.WritePlaceholderDisplacement32(type);
        }


        /// <summary>
        /// Treat the value in the accumulator as a boolean and invert it.
        /// </summary>
        public override void BooleanNot()
        {
            region.Write(new byte[] {
                0x48, 0x83, 0xf0, 0x01 // xor rax, 1 
            });
        }

        /// <summary>
        /// Writes the supplied opcodes into the binary, obviously non portable and all that.
        /// </summary>
        /// <param name="code"></param>
        public override void Raw(byte[] code)
        {
            region.Write(code);
        }

        /// <summary>
        /// Converts the value in the accumulator using the type slot offset on the value.
        /// Does nothing for null reference.
        /// Only used for down casts, upcasts requires a more dynamic approach to finding the type runtimestruct.
        /// </summary>
        /// <param name="typeSlot">offset in the type runtimestructure containing the type that we are converting to.</param>
        public override void TypeConversion(int typeSlot)
        {
            long offset = typeSlot * 8;
            Require.True((int.MaxValue >= offset) && (int.MinValue <= offset));
            region.Write(new byte[] { 
                0x48, 0x21, 0xd2, // and rdx, rdx
                0x74, 0x07, // je +7
                0x48, 0x8b, 0x92 // mov rdx, [rdx+sint32]
            });
            region.WriteInt32(offset);
        }

        /// <summary>
        /// Converts the value in the accumulator using the type slot offset on the value.
        /// Only used for down casts, upcasts requires a more dynamic approach to finding the type runtimestruct.
        /// </summary>
        /// <param name="typeSlot">offset in the type runtimestructure containing the type that we are converting to.</param>
        public override void TypeConversionNotNull(int typeSlot)
        {
            long offset = typeSlot * 8;
            Require.True((int.MaxValue >= offset) && (int.MinValue <= offset));
            region.Write(new byte[] { 
                0x48, 0x8b, 0x92 // mov rdx, [rdx+sint32]
            });
            region.WriteInt32(offset);
        }

        /// <summary>
        /// Does a normal ABI call to the pointer to function pointer
        /// </summary>
        /// <param name="indirectFunction">pointer to function pointer to call</param>
        /// <param name="arguments">array of arguments</param>
        public override void CallBuildIn(Placeholder indirectFunction, Placeholder[] arguments)
        {
            if (arguments == null)
                throw new ArgumentNullException("arguments");
            Require.Assigned(indirectFunction);
            foreach (Placeholder argument in arguments)
                Require.Assigned(argument);
            // alligncallstack
            int stackReserve = 0;
            if ((arguments.Length > 6) && ((arguments.Length & 2) != 0))
            {
                stackReserve++;
                region.Write(new byte[] { 0x6a, 0x00 }); // push 0
            }
            for (int a = arguments.Length - 1; a >= 6; --a)
            {
                stackReserve++;
                region.Write(new byte[] { 0x48, 0xb8 }); // mov rax, imm64
                region.WritePlaceholder(arguments[a]);
                region.Write(new byte[] { 0x50 }); // push rax
            }
            if (arguments.Length >= 6)
            {
                region.Write(new byte[] { 0x49, 0xb9 }); // mov r9, imm64
                region.WritePlaceholder(arguments[5]);
            }
            if (arguments.Length >= 5)
            {
                region.Write(new byte[] { 0x49, 0xb8 }); // mov r8, imm64
                region.WritePlaceholder(arguments[4]);
            }
            if (arguments.Length >= 4)
            {
                region.Write(new byte[] { 0x48, 0xb9 }); // mov rcx, imm64
                region.WritePlaceholder(arguments[3]);
            }
            if (arguments.Length >= 3)
            {
                region.Write(new byte[] { 0x48, 0xba }); // mov rdx, imm64
                region.WritePlaceholder(arguments[2]);
            }
            if (arguments.Length >= 2)
            {
                region.Write(new byte[] { 0x48, 0xbe }); // mov rsi, imm64
                region.WritePlaceholder(arguments[1]);
            }
            if (arguments.Length >= 1)
            {
                region.Write(new byte[] { 0x48, 0xbf }); // mov rdi, imm64
                region.WritePlaceholder(arguments[0]);
            }
            region.Write(new byte[] { 0x4c, 0x8d, 0x1d }); // lea r11, [rip+disp]
            region.WritePlaceholderDisplacement32(indirectFunction);
            region.Write(new byte[] { 0x41, 0xff, 0x13 }); // call [r11]
            if (stackReserve > 0)
            {
                region.Write(new byte[] { 0x48, 0x81, 0xc4 }); //add %rsp, imm32
                region.WriteInt32(stackReserve * 8);
            }
        }

        public override void JumpBuildIn(Placeholder indirectFunction)
        {
            region.Write(new byte[] { 0x48, 0xb9 }); // mov rcx, imm64
            region.WritePlaceholder(indirectFunction);
            region.Write(new byte[] { 0xff, 0x21 }); // jmp [rcx]
        }

        /// <summary>
        /// Creates a token that can be used for jumps.
        /// </summary>
        public override Compiler.JumpToken CreateJumpToken()
        {
            return new JumpToken();
        }

        /// <summary>
        /// Assosiates a JumpToken with a location in the code stream.
        /// </summary>
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

        /// <summary>
        /// Pushes the value in the Accumulator unto the stack.
        /// Used to prepare for a function call, or for field assignment.
        /// </summary>
        public override void PushValue()
        {
            region.Write(new byte[] {
                    0x52, // push rdx
                    0x50, // push rax
            });
        }

        public override void PopValue()
        {
            region.Write(new byte[] {
                0x58, // pop rax
                0x5a // pop rdx
            });
        }

        public override void PeekValue(int depth)
        {
            int offset = depth * 16 + 8;
            Require.True((sbyte.MaxValue >= offset) && (sbyte.MinValue <= offset));
            byte[] code;
            unchecked
            {
                code = new byte[] {
                    0x48, 0x8b, 0x44, 0x24,(byte)(offset-8), //IMMS8 mov eax, [esp+IMMS8]
                    0x48, 0x8b, 0x54, 0x24,(byte)(offset), //IMMS8 mov edx, [eax+0x14]
                };
            }
            region.Write(code);
        }

        public override void DropStackTop()
        {
            region.Write(new byte[] {
                0x59, 0x59 // pop rcx; pop rcx
            });
        }

        /// <summary>
        /// Stores the literal object value in the accumulator
        /// </summary>
        /// <param name="type">Placeholder of the type of the value</param>
        /// <param name="value">Placeholder of the value</param>
        public override void SetValue(Placeholder type, Placeholder value)
        {
            Require.Assigned(type);
            Require.Assigned(value);
            region.Write(new byte[] { 0x48, 0x8d, 0x05 }); // lea rax, [rip+disp]
            region.WritePlaceholderDisplacement32(value);
            region.Write(new byte[] { 0x48, 0x8d, 0x15 }); // lea rdx, [rip+disp]
            region.WritePlaceholderDisplacement32(type);
        }

        /// <summary>
        /// Stores the literal object value in the accumulator
        /// </summary>
        /// <param name="type">Placeholder of the type of the value</param>
        /// <param name="value">Value of the value</param>
        public override void SetImmediateValue(Placeholder type, long value)
        {
            Require.Assigned(type);
            if ((value < int.MinValue) || (value > int.MaxValue))
            {
                region.Write(new byte[] { 0x48, 0xb8 }); // mov rax, imm64
                region.WriteInt64(value);
            }
            else if (value == 0)
            {
                region.Write(new byte[] { 0x48, 0x31, 0xC0 }); // xor rax, rax
            }
            else
            {
                region.Write(new byte[] { 0x48, 0xc7, 0xc0 }); // mov rax, imm32
                region.WriteInt32(value);
            }
            region.Write(new byte[] { 0x48, 0x8d, 0x15 }); // lea rdx, [rip+disp]
            region.WritePlaceholderDisplacement32(type);
        }

        public override void SetOnlyValue(long value)
        {
            if ((value < int.MinValue) || (value > int.MaxValue))
            {
                region.Write(new byte[] { 0x48, 0xb8 }); // mov rax, imm64
                region.WriteInt64(value);
            }
            else if (value == 0)
            {
                region.Write(new byte[] { 0x48, 0x31, 0xC0 }); // xor rax, rax
            }
            else
            {
                region.Write(new byte[] { 0x48, 0xc7, 0xc0 }); // mov rax, imm32
                region.WriteInt32(value);
            }
            byte[] code = new byte[] {
                0x48, 0x31, 0xd2, // xor rdx, rdx
            };
            region.Write(code);
        }

        /// <summary>
        /// Explicitly marks the content of the Accumulator as empty/null.
        /// Usefull for gc purposes, but mostly used for null literals.
        /// </summary>
        public override void Empty()
        {
            region.Write(new byte[] {
                0x48, 0x31, 0xC0,// xor rax, rax
                0x48, 0x31, 0xd2, // xor rdx, rdx
            });
        }

        /// <summary>
        /// Generates a runtime breakpoint for debugging purposes.
        /// </summary>
        public override void Break()
        {
            region.Write(new byte[] {
                0xcc // int3
            });
        }

        /// <summary>
        /// Conditional version of Jump().
        /// Reads a boolean value from the accumulator
        /// </summary>
        public override void JumpIfTrue(JumpToken token)
        {
            token.SetKind(JumpTokenKind.Relative);
            region.Write(new byte[] {
                0x48, 0x21, 0xc0, // and rax, rax
                0x0f, 0x85  // jnz 
            });
            token.SetJumpSite(region.InsertIntToken());
        }

        /// <summary>
        /// Conditional version of Jump().
        /// Reads a boolean value from the accumulator
        /// </summary>
        public override void JumpIfFalse(JumpToken token)
        {
            token.SetKind(JumpTokenKind.Relative);
            region.Write(new byte[] {
                0x48, 0x21, 0xc0, // and rax, rax
                0x0f, 0x84  // jz 
            });
            token.SetJumpSite(region.InsertIntToken());
        }

        /// <summary>
        /// Conditional version of Jump().
        /// Takes the jump if the type part of the accumulator is empty
        /// </summary>
        public override void JumpIfAssigned(JumpToken token)
        {
            token.SetKind(JumpTokenKind.Relative);
            region.Write(new byte[] {
                0x48, 0x21, 0xd2, // and rdx, rdx
                0x0f, 0x85  // jnz 
            });
            token.SetJumpSite(region.InsertIntToken());
        }


        /// <summary>
        /// Conditional version of Jump().
        /// Takes the jump if the type part of the accumulator is empty
        /// </summary>
        public override void JumpIfUnassigned(JumpToken token)
        {
            token.SetKind(JumpTokenKind.Relative);
            region.Write(new byte[] {
                0x48, 0x21, 0xd2, // and rdx, rdx
                0x0f, 0x84  // jz 
            });
            token.SetJumpSite(region.InsertIntToken());
        }

        /// <summary>
        /// Inserts an unconditional jump to the location described by the jump token.
        /// This may be a forward or backward jump, but should be local to the Assembler.
        /// </summary>
        public override void Jump(Compiler.JumpToken token)
        {
            JumpToken t = (JumpToken)token;
            t.SetKind(JumpTokenKind.Relative);
            region.WriteByte(0xe9); // relative offset next instruction
            t.SetJumpSite(region.InsertIntToken());
        }

        /// <summary>
        /// Generate the postamble of the function.
        /// </summary>
        public override void StopFunction()
        {
            region.Write(new byte[] {
                    0xc9, // leave (mov esp, ebp; pop ebp)
                });
            if (parameters == 0)
            {
                region.Write(new byte[] {
                    0xc3 // ret
                });
            }
            else
            {
                region.Write(new byte[] {
                    0xc2 // ret [IMMU16]
                });
                region.WriteInt16((short)(parameters * 16));
            }
        }

        private int StackOffset(int slot)
        {
            if (slot < parameters)
                return (parameters - slot) * 16;
            else
                return ((parameters - slot) - 1) * 16;
        }

        /// <summary>
        // Retrieves the value in the slot slot and puts it in the Accumulator.
        /// </summary>
        /// <param name="slot">Id of the slot on the stack to use.</param>
        public override void RetrieveVariable(int slot)
        {
            int lsdw = StackOffset(slot);
            int msdw = lsdw + 8;
            if ((lsdw > 127) || (lsdw < -128))
            {
                region.Write(new byte[] { 0x48, 0x8b, 0x85 }); // mov rax, [rbp + IMM32]
                region.WriteInt32(lsdw);
            }
            else
            {
                region.Write(new byte[] { 0x48, 0x8b, 0x45 }); // mov rax, [rbp + IMM8]
                region.WriteInt8(lsdw);
            }
            if ((msdw > 127) || (msdw < -128))
            {
                region.Write(new byte[] { 0x48, 0x8b, 0x95 }); // mov rdx, [rbp + IMM32]
                region.WriteInt32(msdw);
            }
            else
            {
                region.Write(new byte[] { 0x48, 0x8b, 0x55 }); // mov rdx, [rbp + IMM8]
                region.WriteInt8(msdw);
            }
        }

        /// <summary>
        /// Stores the value in the Accumulator in the slot slot.
        /// </summary>
        /// <param name="slot">Id of the slot on the stack to use.</param>
        public override void StoreVariable(int slot)
        {
            int lsdw = StackOffset(slot);
            int msdw = lsdw + 8;
            if ((lsdw > 127) || (lsdw < -128))
            {
                region.Write(new byte[] { 0x48, 0x89, 0x85 }); // mov [rbp + IMM32], rax
                region.WriteInt32(lsdw);
            }
            else
            {
                region.Write(new byte[] { 0x48, 0x89, 0x45 }); // mov [rbp + IMM8], rax
                region.WriteInt8(lsdw);
            }
            if ((msdw > 127) || (msdw < -128))
            {
                region.Write(new byte[] { 0x48, 0x89, 0x95 }); // mov [rbp + IMM32], rdx
                region.WriteInt32(msdw);
            }
            else
            {
                region.Write(new byte[] { 0x48, 0x89, 0x55 }); // mov [rbp + IMM8], rdx
                region.WriteInt8(msdw);
            }
        }

        /// <summary>
        /// Fetches the value from the nth slot of the current value in the Accumulator, and places the result into the Accumulator
        /// </summary>
        /// <param name="valueSlot">Id of the slot on the value to use.</param>
        public override void FetchField(int valueSlot)
        {
            int offset = valueSlot * 8;

            Require.True((int.MaxValue >= offset) && (int.MinValue <= offset));

            region.Write(new byte[] { 0x48, 0x8b, 0x8A }); // mov rcx, [rdx+imm32]
            region.WriteInt32(offset);
            region.Write(new byte[] {
                0x48, 0x8b, 0x54, 0x08, 0x08, // mov rdx [rax+rcx+8]
                0x48, 0x8b, 0x04, 0x08 // mov rax, [rax+rcx]
            });
        }

        /// <summary>
        /// Fetches the value from the nth type slot of the current value in the Accumulator,
        /// and places the result in the type part of the current Accumulator, preserving the valuepart
        /// </summary>
        /// <param name="typeSlot">Id of the slot on the type to use.</param>
        public override void FetchMethod(int typeSlot)
        {
            int offset = typeSlot * 8;
            Require.True((int.MaxValue >= offset) && (int.MinValue <= offset));

            region.Write(new byte[] {
                0x48, 0x8b, 0x92  // mov rdx, [rdx+imm32]
            });
            region.WriteInt32(offset);
        }

        /// <summary>
        /// Calls the method that was pushed as the zeroth argument.
        /// Arguments were pushed left to right.
        /// The zeroth argument is converted back to the type backing the method type.
        /// </summary>
        /// <param name="parameterCount">one less than the number of arguments (zero is the this parameter)</param>
        public override Placeholder CallFromStack(int parameterCount)
        {
            int offset = parameterCount * 16 + 8;
            Require.True((int.MaxValue >= offset) && (int.MinValue <= offset));
            if ((offset < -128) || (offset > 127))
            {
                region.Write(new byte[] { 0x48, 0x8b, 0x84, 0x24 }); // mov rax, [rsp+imm32]
                region.WriteInt32(offset);
            }
            else
            {
                region.Write(new byte[] { 0x48, 0x8b, 0x44, 0x24 }); // mov rax [rsp+imm8]
                region.WriteInt8(offset);
            }
            region.Write(new byte[] {
                0x48, 0x8b, 0x50, 0x28, // mov rdx, [rax + 0x28]
            });
            if ((offset < -128) || (offset > 127))
            {
                region.Write(new byte[] { 0x48, 0x89, 0x94, 0x24 }); // mov [rsp+imm32], rax
                region.WriteInt32(offset);
            }
            else
            {
                region.Write(new byte[] { 0x48, 0x89, 0x54, 0x24 }); // mov [rsp+imm8], rax
                region.WriteInt8(offset);
            }
            region.Write(new byte[] {
                0xff, 0x50, 0x20 // call [rax+20]
            });
            return region.CurrentLocation;
        }

        public override Placeholder CallDirect(Placeholder function)
        {
            region.Write(new byte[] { 0x48, 0xb9 }); // mov rcx, imm64
            region.WritePlaceholder(function);
            region.Write(new byte[] {
                    0xff, 0xd1, // callq  *%rcx 
            });
            return region.CurrentLocation;
        }

        /// <summary>
        /// Places the methodStruct as the type part of the Accumulator.
        /// The value part of the Accumulator is cleared.
        /// Only suited for static methods.
        /// </summary>
        /// <param name="methodStruct">Pointer placeholder to a method type struct.</param>
        public override void LoadMethodStruct(Placeholder methodStruct)
        {
            Require.Assigned(methodStruct);
            region.Write(new byte[] { 0x48, 0x8d, 0x15 }); // lea rdx, [rip+disp]
            region.WritePlaceholderDisplacement32(methodStruct);
            region.Write(new byte[] { 0x48, 0x31, 0xC0 }); // xor rax, rax
        }

        /// <summary>
        /// Generates code that calls the allocator with the desired size and the places the memory as the value part in the Accumulator
        /// the type part of the Accumulator is filled with the type pointer placeholder.
        /// </summary>
        /// <param name="allocator">Placeholder to the allocator implementation</param>
        /// <param name="size">Size in bytes to allocate</param>
        /// <param name="type">Placeholder to put in the type part of the new value in the Accumulator</param>
        public override void CallAllocator(Placeholder allocator, int size, Placeholder type)
        {
            Require.Assigned(allocator);
            Require.Assigned(type);
            //some objects have no fields            Require.True(size > 0);

            region.Write(new byte[] { 0x48, 0xc7, 0xc7 }); // mov rdi, imm32
            region.WriteInt32(size);

            region.Write(new byte[] { 0x48, 0x31, 0xf6 }); // xor rsi, rsi

            // fake callframe
            Region.Write(new byte[] {
                0x55, // push rbp
                0x55, // push rbp
                0x48, 0x89, 0xE5 // mov rbp, rsp
            });
            region.Write(new byte[] { 0x48, 0x89, 0xEA }); // mov rdx, rbp

            region.Write(new byte[] { 0x4c, 0x8d, 0x1d }); // lea r11, [rip+disp]
            region.WritePlaceholderDisplacement32(allocator);
            region.Write(new byte[] { 0x41, 0xff, 0x13 }); // call [r11]
            region.Write(new byte[] { 0x48, 0x8d, 0x15 }); // lea rdx, [rip+disp]
            region.WritePlaceholderDisplacement32(type);
            region.Write(new byte[] { 0x5d, 0x5d }); // pop ebp; pop ebp
        }


        /// <summary>
        /// Stores the Accumulator into a field of the top of the stack.
        /// </summary>
        /// <param name="slot">Field number of the slot in Accumulator</param>
        public override void StoreInFieldOfSlotNoTouch(int slot)
        {
            int offset = slot * 8;
            Require.True((int.MaxValue >= offset) && (int.MinValue <= offset));
            region.Write(new byte[] {
                0x48, 0x8b, 0x4c, 0x24, 0x8, // mov rcx, [rsp + 8]
                0x48, 0x8b, 0x89 // mov rcx [rcx+imm32]
                });
            region.WriteInt32(offset);
            region.Write(new byte[] {
                0x48, 0x03, 0x0c, 0x24, // add rcx , [rsp]
                0x48, 0x89, 01, // mov [rcx], rax
                0x48, 0x89, 0x51, 0x08, // mov [rcx+8] rdx
                0x59, 0x59 // pop rcx; pop rcx
            });
        }

        /// <summary>
        /// Stores the Accumulator into a field of the top of the stack.
        /// </summary>
        /// <param name="touch">Touch function of the garbage collector, may be 0 for non gc'ed types.</param>
        /// <param name="slot">Field number of the slot in Accumulator</param>
        public override void StoreInFieldOfSlot(Placeholder touch, int slot)
        {
            int offset = slot * 8;
            Require.True((int.MaxValue >= offset) && (int.MinValue <= offset));
            region.Write(new byte[] {
                0x48, 0x8b, 0x4c, 0x24, 0x8, // mov rcx, [rsp + 8]
                0x48, 0x8b, 0x89 // mov rcx [rcx+imm32]
                });
            region.WriteInt32(offset);
            region.Write(new byte[] {
                0x48, 0x03, 0x0c, 0x24, // add rcx , [rsp]
                0x48, 0x89, 01, // mov [rcx], rax
                0x48, 0x89, 0x51, 0x08, // mov [rcx+8] rdx
            });
            region.Write(new byte[] { 0x48, 0x89, 0xcf }); // mov rdi, rcx
            region.Write(new byte[] { 0x4c, 0x8d, 0x1d }); // lea r11, [rip+disp]
            region.WritePlaceholderDisplacement32(touch);
            region.Write(new byte[] { 0x41, 0xff, 0x13 }); // call [r11]
            region.Write(new byte[] {
                0x59, 0x59 // pop rcx; pop rcx
            });
        }

        public override void CallNative(Placeholder function, int argumentCount, bool stackFrame, bool trampoline)
        {
            region.Write(new byte[] { 0x4c, 0x8d, 0x1d }); // lea r11, [rip+disp]
            region.WritePlaceholderDisplacement32(function);

            if (trampoline)
            {
                Require.False(stackFrame);
                Require.True(argumentCount == 0);
                region.Write(new byte[] { 0x41, 0xff, 0x23 }); // jmp [r11]
            }
            else
            {
                region.Write(new byte[] { 0x41, 0xff, 0x13 }); // call [r11]
            }
            if (argumentCount >= 3)
            {
                region.Write(new byte[] {
                    0x59, 0x59 // pop rcx; pop rcx
                });
            }
        }

        public override void SetupNativeStackFrameArgument(int argumentCount)
        {
            if (argumentCount == 0)
                region.Write(new byte[] { 0x48, 0x89, 0xef }); // mov rdi, rbp                                                                                                                   
            if (argumentCount == 1)
                region.Write(new byte[] { 0x48, 0x89, 0xea }); // mov rdx, rbp                                                                                                                   
            if (argumentCount == 2)
                region.Write(new byte[] { 0x49, 0x89, 0xe8 }); // mov r8, rbp                                                                                                                   
            if (argumentCount >= 3)
            {
                region.Write(new byte[] { 0x55 }); // push ebp
                region.Write(new byte[] { 0x55 }); // push ebp
            }
        }

        public override void SetNativeArgument(int slot, int index, int count)
        {
            // reverse arguments for c
            index = count - index - 1;
            int lsdw = StackOffset(slot);
            int msdw = lsdw + 8;
            if (index == 0)
            {
                region.Write(new byte[] { 0x48, 0x8b, 0xbd }); // mov rdi, [rbp + IMM32]
                region.WriteInt32(lsdw);
                region.Write(new byte[] { 0x48, 0x8b, 0xb5 }); // mov rsi, [rbp + IMM32]
                region.WriteInt32(msdw);
            }
            else if (index == 1)
            {
                region.Write(new byte[] { 0x48, 0x8b, 0x95 }); // mov rdx, [rbp + IMM32]
                region.WriteInt32(lsdw);
                region.Write(new byte[] { 0x48, 0x8b, 0x8d }); // mov rcx, [rbp + IMM32]
                region.WriteInt32(msdw);
            }
            else if (index == 2)
            {
                region.Write(new byte[] { 0x4c, 0x8b, 0x85 }); // mov r8, [rbp + IMM32]
                region.WriteInt32(lsdw);
                region.Write(new byte[] { 0x4c, 0x8b, 0x8d }); // mov r9, [rbp + IMM32]
                region.WriteInt32(msdw);
            }
            else
            {
                PushRbpImm(msdw);
                PushRbpImm(lsdw);
            }
        }

        private void PushRbpImm(int value)
        {
            if ((value >= -128) && (value <= 127))
            {
                region.Write(new byte[] { 0xff, 0x75 }); // push [rbp + IMM8]
                region.WriteInt8(value);
            }
            else
            {
                region.Write(new byte[] { 0xff, 0xb5 }); // push [rbp + IMM32]
                region.WriteInt32(value);
            }
        }

        public override void PopNativeArgument()
        {
            region.Write(new byte[] { 0x5e, 0x5f }); // pop rsi; pop rdi
        }

        public override void SetupNativeReturnSpace()
        {
        }

        public override void CrashIfNull()
        {
            region.Write(new byte[] { 0x48, 0x8b, 0x0a }); // mov rcx, [rdx]
        }

        public override void IntegerNegate()
        {
            region.Write(new byte[] { 0x48, 0xf7, 0xd8 }); // neg rax
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
                0x48, 0x89, 0xc2, // mov rdx, rax
                0x48, 0x31, 0xc0, // xor rax, rax
                0x59, // pop rcx (rax)
                0x48, 0x39, 0xd1, //cmp rcx, rdx
            });
            region.WriteByte(op);
            region.Write(new byte[] {
                0x04, // j* +4
                0x48, 0x83, 0xf0, 0x01, // xor rax, 1
                0x59, // pop rcx (rdx)
            });
        }

        public override void IntegerAdd()
        {
            region.Write(new byte[] {
                0x48, 0x89, 0xc2, // mov rdx, rax
                0x58, // pop rax
                0x48, 0x01, 0xd0, // add rax, rdx
                0x5a // pop rdx
            });
        }

        public override Placeholder CheckOverflow(Placeholder overflowException)
        {
            region.Write(new byte[] { 0x71, 0x0a }); // jno +10
            region.Write(new byte[] { 0x4c, 0x8d, 0x1d }); // lea r11, [rip+disp]
            region.WritePlaceholderDisplacement32(overflowException);
            region.Write(new byte[] { 0x41, 0xff, 0x13 }); // call [r11]
            return region.CurrentLocation;
        }

        public override void IntegerSubtract()
        {
            region.Write(new byte[] {
                0x48, 0x89, 0xc2, // mov rdx, rax
                0x58, // pop rax
                0x48, 0x29, 0xd0, // sub rax, rdx
                0x5a // pop rdx
            });
        }

        public override void IntegerMultiply()
        {
            region.Write(new byte[] {
                0x48, 0x89, 0xc2, // mov rdx, rax
                0x58, // pop rax
                0x48, 0xf7, 0xea, // imul rdx (rax implicit)
                0x5a // pop rdx
            });
        }

        public override void IntegerDivide()
        {
            region.Write(new byte[] {
                0x48, 0x89, 0xc1, // mov rcx, rax
                0x58, // pop rax
                0x48, 0x99, // cqto
                0x48, 0xf7, 0xf9, // idiv %ecx (edx:eax implicit)
                0x5a // pop rdx
            });
        }

        public override void IntegerModulo()
        {
            region.Write(new byte[] {
                0x48, 0x89, 0xc1, // mov rcx, rax
                0x58, // pop rax
                0x48, 0x99, // cqto
                0x48, 0xf7, 0xf9, // idiv %ecx (edx:eax implicit)
                0x48, 0x89, 0xd0, // mov rax, rdx
                0x5a // pop rdx
            });
        }

        public override void IntegerLeft()
        {
            region.Write(new byte[] {
                0x48, 0x89, 0xc1, // mov ecx, eax
                0x58, // pop rax
                0x48, 0xd3, 0xe0, // sal rax, cl
                0x5a // pop rdx
            });
        }

        public override void IntegerRight()
        {
            region.Write(new byte[] {
                0x48, 0x89, 0xc1, // mov rcx, rax
                0x58, // pop rax
                0x48, 0xd3, 0xf8, // sar rax, cl
                0x5a // pop rdx
            });
        }

        public override void ArrayFetchByte()
        {
            region.Write(new byte[] {
                0x48, 0x89, 0xc2, // mov rdx, rax
                0x58, // pop rax
                0x48, 0x8b, 0x00, // mov rax, [rax]
                0x8a, 0x04, 0x10, // mov al, [rax+rdx]
                0x48, 0x0f, 0xbe, 0xc0, // mov rax, al
                0x5a // pop rdx
            });
        }

        public override void ArrayStoreByte()
        {
            region.Write(new byte[] {
                0x48, 0x89, 0xc1, // mov rcx, rax
                0x58, // pop rax
                0x5a, // pop rdx
                0x48, 0x89, 0xc2, // mov rdx, rax
                0x58, // pop rax
                0x48, 0x8b, 0x00, // mov rax, [rax]
                0x88, 0x0c, 0x10, // mov [rax+rdx], cl
                0x5a // pop rdx
            });
        }

        public override void ArrayFetchInt()
        {
            region.Write(new byte[] {
                0x48, 0x89, 0xc2, // mov rdx, rax
                0x58, // pop rax
                0x48, 0x8b, 0x00, // mov rax, [rax]
                0x48, 0x8b, 0x04, 0xd0, // mov rax, (rax+rdx*8)
                0x5a // pop rdx
            });
        }

        public override void ArrayStoreInt()
        {
            region.Write(new byte[] {
                0x48, 0x89, 0xc1, // mov rcx, rax
                0x58, // pop rax
                0x5a, // pop rdx
                0x48, 0x89, 0xc2, // mov rdx, rax
                0x58, // pop rax
                0x48, 0x8b, 0x00, // mov rax, [rax]
                0x48, 0x89, 0x0c, 0xd0, // mov [rax+rdx*8], rcx
                0x5a // pop rdx
            });
        }

        public override void ExceptionHandlerSetup(PlaceholderRef site)
        {
            region.Write(new byte[] { 
                0x48, 0x31, 0xc9, // xor rcx, rcx
                0x51 // push rcx
            });
            region.Write(new byte[] { 0x48, 0xb9 }); // mov rcx, imm64
            region.WritePlaceholderRef(site);
            region.Write(new byte[] { 
                0x51, // push rcx
                0x48, 0x31, 0xc9, // xor rcx, rcx
                0x51, // push rcx
                0xff, 0x75, 0x00, // push [rbp]
                0x48, 0x89, 0x65, 0x00 // mov [rbp], rsp
            });
        }

        public override void ExceptionHandlerRemove()
        {
            region.Write(new byte[] {
                0x8f, 0x45, 0x00, // pop [rbp]
                0x59, 0x59, 0x59 // pop rcx; pop rcx; pop rcx
            });
        }

        public override void ExceptionHandlerInvoke()
        {
            region.Write(new byte[] {
                0x48, 0x8b, 0x4d, 0x00,  //        	mov    0x0(%rbp),%rcx
                0x48, 0x8b, 0x49, 0x08,  //        	mov    0x8(%rcx),%rcx 
                0x48, 0x85, 0xc9,        //     	test   %rcx,%rcx
                0x74, 0x06,              //  	je     40049c <.done>
                0x48, 0x8b, 0x6d, 0x00,  //        	mov    0x0(%rbp),%rbp
                0xeb, 0xed,              //  	jmp    400489 <.unwind>
                0x48, 0x8b, 0x4d, 0x00,  //        	mov    0x0(%rbp),%rcx
                0x48, 0x89, 0xcc,        //     	mov    %rcx,%rsp
                0x8f, 0x45, 0x00,        //     	popq   0x0(%rbp)
                0x59,                    // 	pop    %rcx
                0x41, 0x5b,              //  	pop    %r11
                0x59,                    // 	pop    %rcx
                0x41, 0xff, 0xe3,        //     	jmpq   *%r11
            });
        }

        public override void TypeConversionDynamicNotNull(long typeId)
        {
            byte[] code = new byte[] {
                    0x48, 0x8b, 0x4a, 0x08, // mov    0x8(%rdx),%rcx
                    0x52, // push %rdx
                    0x50, // push %rax
                    0x48, 0x31, 0xD2, // xor    %rdx,%rdx
                    0x48, 0xb8, // mov rax, IMM64
            };
            region.Write(code);
            region.WriteInt64(typeId);
            code = new byte[] {
                    0x52, // push %rdx
                    0x50, // push %rax
                    0xff, 0xd1, // callq  *%rcx
            };
            region.Write(code);
        }

        public override void Load(Placeholder location)
        {
            region.Write(new byte[] {
                0x48, 0x8d, 0x0d //               lea IMM32(rip), rcx
            });
            region.WritePlaceholderDisplacement32(location);
            region.Write(new byte[] {
              0x48, 0x8b, 0x01, //                mov    0(%rcx),%rax
              0x48, 0x8b, 0x51, 0x08  //          mov    8(%rcx),%rdx
            });
        }

        public override void Store(Placeholder location)
        {
            region.Write(new byte[] {
                0x48, 0x8d, 0x0d //               lea IMM32(rip), rcx
            });
            region.WritePlaceholderDisplacement32(location);
            region.Write(new byte[] {
              0x48, 0x89, 0x01, //                mov    %rax,0(%rcx)
              0x48, 0x89, 0x51, 0x08  //          mov    %rdx,8(%rcx)
            });
        }

        public override void SetupFpu()
        {
            region.Write(
                            new byte[] {
                                0x9b, 0xDB, 0xE2, //fclex
                                0x48, 0xc7, 0xc0, 0x3f, 0x13, 0x00, 0x00, // mov eax, $133f
                                0x50, // push eax
                                0x48, 0x8d, 0x04, 0x24, // lea eax, [esp]
                                0xd9, 0x28, // fldcw [eax]
                                0x58, // pop eax
                            });
            
        }

        public override void MarkType()
        {
            region.Write(new byte[] {
                0x48, 0x83, 0xCA, 0x01 // or rdx, 1
            });
        }

        public override void UnmarkType()
        {
            region.Write(new byte[] {
                0x48, 0x83, 0xE2, 0xFE // and rdx, -2
            });
        }

        public override void JumpIfNotMarked(Compiler.JumpToken token)
        {
            region.Write(new byte[] {
                0x48, 0xF7, 0xC2, 0x01, 0x00, 0x00, 0x00 // test rdx, 1
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
