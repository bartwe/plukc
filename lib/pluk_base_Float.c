#include <pluk.h>
#include <stdlib.h>
#include <stdio.h>
#include <limits.h>
#include <math.h>

pref floatToPref(pluk_float_t value)
{
  pref result;
  result.type = pluk_base_Float;
  long temp;
  pluk_float_t* a = (pluk_float_t*)&temp;
  *a = value;
  result.value = (size_t*)temp;
  return result;
}

pluk_float_t floatFromPref(pref value)
{
  long temp = longFromPref(value);
  pluk_float_t* a = (pluk_float_t*)&temp;
  return *a;
}

/* extern bool OperatorGreaterThan(Float other) */
pref pluk_base_Float__OperatorGreaterThan(pref this, pref data)
{
  pref result;
  if (floatFromPref(this) > floatFromPref(data))
    result.value = (size_t*)true;
  else 
    result.value = (size_t*)false;    
  result.type = pluk_base_Bool;
  return result;
}

/* extern bool OperatorLessThan(Float other) */
pref pluk_base_Float__OperatorLessThan(pref this, pref data)
{
  pref result;
  if (floatFromPref(this) < floatFromPref(data))
    result.value = (size_t*)true;
  else 
    result.value = (size_t*)false;    
  result.type = pluk_base_Bool;
  return result;
}

/* extern bool OperatorGreaterEquals(Float other) */
pref pluk_base_Float__OperatorGreaterEquals(pref this, pref data)
{
  pref result;
  if (floatFromPref(this) >= floatFromPref(data))
    result.value = (size_t*)true;
  else 
    result.value = (size_t*)false;    
  result.type = pluk_base_Bool;
  return result;
}

/* extern bool OperatorLessEquals(Float other) */
pref pluk_base_Float__OperatorLessEquals(pref this, pref data)
{
  pref result;
  if (floatFromPref(this) <= floatFromPref(data))
    result.value = (size_t*)true;
  else 
    result.value = (size_t*)false;    
  result.type = pluk_base_Bool;
  return result;
}

/* extern Float OperatorAdd(Float other) */
pref pluk_base_Float__OperatorAdd(pref this, pref data)
{
  return floatToPref(floatFromPref(this) + floatFromPref(data));
}

/* extern Float OperatorSubtract(Float other) */
pref pluk_base_Float__OperatorSubtract(pref this, pref data)
{
  return floatToPref(floatFromPref(this) - floatFromPref(data));
}

/* extern Float OperatorMultiply(Float other) */
pref pluk_base_Float__OperatorMultiply(pref this, pref data)
{
  return floatToPref(floatFromPref(this) * floatFromPref(data));
}

/* extern Float OperatorDivide(Float other) */
pref pluk_base_Float__OperatorDivide(pref this, pref data)
{
  return floatToPref(floatFromPref(this) / floatFromPref(data));
}

/* extern static bool IsNan(Float other) */
pref pluk_base_Float__InnerIsNan(pref this, pref data)
{
  return boolToPref(isnan(floatFromPref(data)));
}

/* extern String ToString() */
pref pluk_base_Float__ToString(pref this)
{
#ifdef pluk64
//!\ on my 64 bit linux there is a crash in printf("%e") which goes away in valgrind
// so this is a fugly workaround to keep the crashes away
  const size_t bufsize = sizeof(size_t) * 5;
  int len;
  pref result;
  result.value =  pluk_allocateGC(0, bufsize, 0);
  len = sprintf((char*)&result.value[1],"%lx", longFromPref(this));
  result.value[0] = (size_t)len;
  result.type = pluk_base_String;
  return result;
#else
  const size_t bufsize = sizeof(size_t) * 8;
  int len;
  pref result;
  result.value =  pluk_allocateGC(0, bufsize, 0);
  len = sprintf((char*)&result.value[1],"%e", floatFromPref(this));
  result.value[0] = (size_t)len;
  result.type = pluk_base_String;
  return result;
#endif
}

/* extern Float OperatorNegate() */
pref pluk_base_Float__OperatorNegate(pref this)
{
  return floatToPref(-floatFromPref(this));
}

/* extern static float? Parse(string value) */
pref pluk_base_Float__InnerParse(pref this, pref value)
{
  char* v = cstrFromPref(value);
  char* ep;
  double r = strtod(v, &ep);
  if ((v[0] != '\0') && (ep[0] == '\0'))
    return floatToPref((pluk_float_t)r);
  return nullToPref();
}

/* implicit extern float FromInt(int value); */
pref pluk_base_Float__FromInt(pref this, pref value)
{
  return floatToPref((float)longFromPref(value));
}

/* private extern int InnerToInt(); */
pref pluk_base_Float__InnerToInt(pref this)
{
  return longToPref((long)floatFromPref(this));
}
