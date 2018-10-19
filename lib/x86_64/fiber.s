	.file	"fiber.s"
	.text
.globl fiber_setup
fiber_setup:
	pushq	%rbp
	movq	%rsp, %rbp

# flip the stack to the fiber
	movq	%rsp, %rcx
	movq	(%rdi), %rsp

#add a footer to the stack
	xorq	%rdx, %rdx
	pushq	%rdx
	pushq	%rdx

# push the entrypoint on the stack
	movq	32(%rdi), %rdx
	pushq	%rdx
	movq	40(%rdi), %rdx
	pushq	%rdx
	
# push to entrypoint callable unto the stack to be called by a later ret in
# a switch
	movq	8(%rsp), %rax
	movq    0x28(%rax), %rdx
	movq	%rdx, 8(%rsp)
# push a minus one, this represents the return address that would have been pushed if we had used call
# needs to be nonzero due to the exceptionhandler
	xorq	%rdx, %rdx
	dec	%rdx
	pushq	%rdx 
# ret point for the fictional call through the ret
	movq	0x20(%rax), %rax
# we ret into this callable on the next resume
	pushq	%rax
# the leaves the rbp to be cleaned in the stack to mark the end for the GC
	call	fiber_setup_inner@PLT

	xorq	%rbp, %rbp
	ret

# save fiber stack and switch back
fiber_setup_inner:
	movq	%rsp, (%rdi)
	movq	%rcx, %rsp

	popq	%rbp
	ret


.globl fiber_switch
fiber_switch:
	pushq	%rbp
	movq	%rsp, %rbp
	call	fiber_switch_inner@PLT
	popq	%rbp
	ret

#swaps out the stack pointer, which causes the return to go to the other
#fiber, which fixes to rbp
# so in this way we save:
# eip in the stack for the fiber_switch_inner call.
# rbp in the stack of fiber_switch and rsp in the fiber data
fiber_switch_inner:
	movq	%rsi, 8(%rdi)
	movq	(%rdi), %rdx
	movq	%rsp, (%rdi)
	movq	%rdx, %rsp
	ret
