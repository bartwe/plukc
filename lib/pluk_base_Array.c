#include <pluk.h>

/* extern void Alloc(Int count, T initialValue) */
pref pluk_base_Array__Alloc(pref this, pref count, pref initialValue)
{
  pref result;
  pref* slot;
  int i;
  result.value = 0;
  result.type = 0;
  this.value[1] = (size_t)this.type;
  slot = (pref*)pluk_allocateGC((size_t)longFromPref(count), 0, 0);
  this.value[0] = (size_t)slot;
  long c = longFromPref(count);
  for (i = 0; i < c; ++i)
    slot[i] = initialValue;
  pluk_touchGC(&initialValue);
  return result;
}

/* extern void InnerOperatorSetIndex(Int index, T value) */
pref pluk_base_Array__InnerOperatorSetIndex(pref this, pref index, pref value)
{
  pref result;
  pref* slot;
  result.value = 0;
  result.type = 0;
  slot = &((pref*)(this.value[0]))[longFromPref(index)];
  slot[0] = value;
  pluk_touchGC(slot);    
  return result;
}

/* extern T InnerOperatorGetIndex(Int index) */
pref pluk_base_Array__InnerOperatorGetIndex(pref this, pref index)
{
  pref* slot;
  slot = &((pref*)(this.value[0]))[longFromPref(index)];
  return slot[0];
}

/*
buildin support for arrays of booleans
*/

pref pluk_base_Array__pluk_base_Bool__Alloc(pref this, pref count, pref initialValue)
{
  pref result;
  signed char* slot;
  result.value = 0;
  result.type = 0;
  this.value[1] = (size_t)this.type;
  slot = (signed char*)pluk_allocateGC(0, (size_t)longFromPref(count), 0);
  this.value[0] = (size_t)slot;
  memset(slot, (unsigned char)longFromPref(initialValue), longFromPref(count));
  return result;
}

pref pluk_base_Array__pluk_base_Bool__InnerOperatorSetIndex(pref this, pref index, pref value)
{
  pref result;
  result.value = 0;
  result.type = 0;
  ((signed char*)(this.value[0]))[longFromPref(index)] = (signed char)boolFromPref(value);
  return result;
}

pref pluk_base_Array__pluk_base_Bool__InnerOperatorGetIndex(pref this, pref index)
{
  pref result;
  result.type = pluk_base_Bool;
  result.value = (size_t*)((size_t)((signed char*)(this.value[0]))[longFromPref(index)]);
  return result;
}

/*
buildin support for arrays of bytes
*/

pref pluk_base_Array__pluk_base_Byte__Alloc(pref this, pref count, pref initialValue)
{
  pref result;
  unsigned char* slot;
  result.value = 0;
  result.type = 0;
  this.value[1] = (size_t)this.type;
  slot = (unsigned char*)pluk_allocateGC(0, (size_t)longFromPref(count), 0);
  this.value[0] = (size_t)slot;
  memset(slot, (unsigned char)longFromPref(initialValue), longFromPref(count));
  return result;
}

pref pluk_base_Array__pluk_base_Byte__InnerOperatorSetIndex(pref this, pref index, pref value)
{
  pref result;
  result.value = 0;
  result.type = 0;
  ((unsigned char*)(this.value[0]))[longFromPref(index)] = (unsigned char)longFromPref(value);
  return result;
}

pref pluk_base_Array__pluk_base_Byte__InnerOperatorGetIndex(pref this, pref index)
{
  pref result;
  result.type = pluk_base_Byte;
  result.value = (size_t*)((size_t)((unsigned char*)(this.value[0]))[longFromPref(index)]);
  return result;
}

/*
buildin support for arrays of integer
*/

pref pluk_base_Array__pluk_base_Int__Alloc(pref this, pref count, pref initialValue)
{
  pref result;
  long* slot;
  long initial;
  int i;
  initial = longFromPref(initialValue);
  result.value = 0;
  result.type = 0;
  this.value[1] = (size_t)this.type;
  slot = (long*)pluk_allocateGC(0, sizeof(long)*(size_t)longFromPref(count), 0);
  this.value[0] = (size_t)slot;
  long c = longFromPref(count);
  for (i = 0; i < c; ++i)
    slot[i] = initial;
  return result;
}

pref pluk_base_Array__pluk_base_Int__InnerOperatorSetIndex(pref this, pref index, pref value)
{
  pref result;
  result.value = 0;
  result.type = 0;
  ((long*)(this.value[0]))[longFromPref(index)] = longFromPref(value);
  return result;
}

pref pluk_base_Array__pluk_base_Int__InnerOperatorGetIndex(pref this, pref index)
{
  pref result;
  result.type = pluk_base_Int;
  result.value = (size_t*)(((long*)(this.value[0]))[longFromPref(index)]);
  return result;
}
