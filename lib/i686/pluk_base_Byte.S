        .file   "pluk_base_Byte.S"
        .text

.globl pluk_base_Byte__FromIntUnsafe
pluk_base_Byte__FromIntUnsafe:
.globl _pluk_base_Byte__FromIntUnsafe
_pluk_base_Byte__FromIntUnsafe:
	pushl	%ebp
	movl	%esp, %ebp
	movl	0x14(%ebp), %edx
	xorl	%eax, %eax
	movb    0x8(%ebp),%al
 	movl    %ebp,%esp
 	popl    %ebp
 	retl    $0x10
