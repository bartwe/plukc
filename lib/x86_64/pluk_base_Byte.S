        .file   "pluk_base_Byte.S"
        .text
.globl pluk_base_Byte__FromIntUnsafe
pluk_base_Byte__FromIntUnsafe:
.globl _pluk_base_Byte__FromIntUnsafe
_pluk_base_Byte__FromIntUnsafe:
	pushq	%rbp
	movq	%rsp, %rbp
	mov	0x28(%rbp),%rdx
	xorq	%rax, %rax
	movb    0x10(%rbp),%al
 	movq    %rbp,%rsp
 	popq    %rbp
 	retq    $0x20
