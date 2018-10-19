#include <pluk.h>

#include <stdio.h>

/* extern bool OperatorEquals(Byte other) */
pref pluk_base_Byte__OperatorEquals(pref this, pref data)
{
  pref result;
  if (longFromPref(this) == longFromPref(data))
    result.value = (size_t*)true;
  else 
    result.value = (size_t*)false;    
  result.type = pluk_base_Bool;
  return result;
}

/* extern bool OperatorNotEquals(Byte other) */
pref pluk_base_Byte__OperatorNotEquals(pref this, pref data)
{
  pref result;
  if (longFromPref(this) != longFromPref(data))
    result.value = (size_t*)true;
  else 
    result.value = (size_t*)false;    
  result.type = pluk_base_Bool;
  return result;
}

/* extern int InnerToInt(byte value) */
pref pluk_base_Byte__InnerToInt(pref this)
{
  return longToPref(longFromPref(this) & 0xff);
}

/* extern String ToString() */
pref pluk_base_Byte__ToString(pref this)
{
  const size_t bufsize = sizeof(size_t) * 5;
  int len;
  pref result;
  result.value =  pluk_allocateGC(0, bufsize, 0);
  len = sprintf((char*)&result.value[1],"%ld", longFromPref(this) & 0xff);
  result.value[0] = (size_t)len;
  result.type = pluk_base_String;
  return result;
}

