#include <pluk.h>

#include "3rdparty/valgrind/valgrind.h"

//stacklayout
// 0*w stack pointer
// 1*w frame pointer
// at phase 0

// 4*w entryPoint
// 5*w entryPoint

// 6*w valgrindStackId
// 7*w stackSize

// at phase 1

// 2*w gc space0
// 3*w gc space1


// stacked:
// -2*w return into yieldAtTermination
// -1*w zero (marks stack end)

void fiber_setup(size_t* base);
void fiber_switch(size_t* base, size_t* stackTrace);

// from GC.c
void gc_registerFiber(size_t* fiber);
void gc_unregisterFiber(size_t* fiber);

//private extern void Init(int stackSize, void() entrypoint);
pref pluk_base_Fiber__Init(pref this, pref stackSize, pref entryPoint)
{
  pref* holder;
  holder = &fieldFromPref(this, 0);
  holder->type = pluk_base_String; //lying
  holder->value = pluk_allocateGC(0, 8* sizeof(size_t*) + longFromPref(stackSize), 0);
  size_t** s = (size_t**)holder->value;
  
  void* stack = &s[8];
  
  s[0] = (size_t*)((unsigned char*)stack + longFromPref(stackSize));
  s[1] = 0;
  s[2] = 0;
  s[3] = 0;
  s[4] = entryPoint.type;
  s[5] = entryPoint.value;
  s[6] = 0;
  s[7] = (size_t*)longFromPref(stackSize);
  
  s[6] = 
  (size_t*)(size_t)VALGRIND_STACK_REGISTER(
  (void*)((size_t)s[0] - (size_t)s[7])
  , (void*)s[0]);
  
  fiber_setup((size_t*)s);
  
  return nullToPref();
}

//private extern void SwitchToMain();
pref pluk_base_Fiber__SwitchToMain(pref this, size_t* stackTrace)
{
  size_t* store;
  store = fieldFromPref(this, 0).value;
  fiber_switch(store, stackTrace);
  return nullToPref();
}

//private extern void SwitchToFiber();
pref pluk_base_Fiber__SwitchToFiber(pref this, size_t* stackTrace)
{
  size_t* store;
  store = fieldFromPref(this, 0).value;
  fiber_switch(store, stackTrace);
  return nullToPref();
}
//private extern void RegisterStack();
pref pluk_base_Fiber__RegisterStack(pref this)
{
  size_t* store;
  store = (size_t*)fieldFromPref(this, 0).value;
  store[6] = VALGRIND_STACK_REGISTER((void*)store[0], (void*)store[0] + store[7]);
  gc_registerFiber(store);
  return nullToPref();
}

//private extern void UnregisterStack();
pref pluk_base_Fiber__UnregisterStack(pref this)
{
  size_t* store;
  store = (size_t*)fieldFromPref(this, 0).value;
  VALGRIND_STACK_DEREGISTER(store[6]);
  gc_unregisterFiber(store);
  return nullToPref();
}
