	.file	"fiber.s"
	.text
.globl _fiber_setup
_fiber_setup:
.globl fiber_setup
fiber_setup:
	pushl	%ebp
	movl	%esp, %ebp
	movl	8(%ebp), %eax

# flip the stack to the fiber
	movl	%esp, %ecx
	movl	(%eax), %esp

#add a footer to the stack
	xorl    %edx, %edx
	pushl   %edx
	pushl   %edx
# push the entrypoint on the stack
	movl	16(%eax), %edx
	pushl	%edx
	movl	20(%eax), %edx
	pushl	%edx
	
# push to entrypoint callable unto the stack to be called by a later ret in
# a switch
# reusing ebp to save eax(fiber record) and ecx(main stack)
	movl	4(%esp), %ebp
	movl    0x14(%ebp), %edx
	movl	%edx, 4(%esp)
# push a minus one, this represents the return address that would have been pushed if we had used call
# needs to be nonzero due to the exceptionhandler
	xorl	%edx, %edx
	dec	%edx
	pushl	%edx 
# ret point for the fictional call through the ret
	movl	0x10(%ebp), %ebp
# we ret into this callable on the next resume
	pushl	%ebp
# the leaves the ebp to be cleaned in the stack to mark the end for the GC
	pushl	$fiber_setup_clear_ebp

# save fiber stack and switch back
	movl	%esp, (%eax)
	movl	%ecx, %esp

	popl	%ebp
	ret
fiber_setup_clear_ebp:
	xorl	%ebp, %ebp
	ret

.globl _fiber_switch
_fiber_switch:
.globl fiber_switch
fiber_switch:
	pushl	%ebp
	movl	%esp, %ebp
	call	fiber_switch_inner
	popl	%ebp
	ret

#swaps out the stack pointer, which causes the return to go to the other
#fiber, which fixes to ebp
# so in this way we save:
# eip in the stack for the fiber_switch_inner call.
# ebp in the stack of fiber_switch and esp in the fiber data
fiber_switch_inner:
	movl	8(%ebp), %eax
	movl	12(%ebp), %edx
	movl	%edx, 4(%eax)
	movl	(%eax), %edx
	movl	%esp, (%eax)
	movl	%edx, %esp
	ret

