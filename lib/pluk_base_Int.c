#include <pluk.h>
#include <limits.h>

pref longToPref(long value)
{
  pref result;
  result.type = pluk_base_Int;
  result.value = (size_t*)value;
  return result;
}

/* extern bool OperatorGreaterThan(Int other) */
pref pluk_base_Int__OperatorGreaterThan(pref this, pref data)
{
  pref result;
  if (longFromPref(this) > longFromPref(data))
    result.value = (size_t*)true;
  else 
    result.value = (size_t*)false;    
  result.type = pluk_base_Bool;
  return result;
}

/* extern bool OperatorLessThan(Int other) */
pref pluk_base_Int__OperatorLessThan(pref this, pref data)
{
  pref result;
  if (longFromPref(this) < longFromPref(data))
    result.value = (size_t*)true;
  else 
    result.value = (size_t*)false;    
  result.type = pluk_base_Bool;
  return result;
}

/* extern bool OperatorGreaterEquals(Int other) */
pref pluk_base_Int__OperatorGreaterEquals(pref this, pref data)
{
  pref result;
  if (longFromPref(this) >= longFromPref(data))
    result.value = (size_t*)true;
  else 
    result.value = (size_t*)false;    
  result.type = pluk_base_Bool;
  return result;
}

/* extern bool OperatorLessEquals(Int other) */
pref pluk_base_Int__OperatorLessEquals(pref this, pref data)
{
  pref result;
  if (longFromPref(this) <= longFromPref(data))
    result.value = (size_t*)true;
  else 
    result.value = (size_t*)false;    
  result.type = pluk_base_Bool;
  return result;
}

/* extern bool OperatorEquals(Int other) */
pref pluk_base_Int__OperatorEquals(pref this, pref data)
{
  pref result;
  if (longFromPref(this) == longFromPref(data))
    result.value = (size_t*)true;
  else 
    result.value = (size_t*)false;    
  result.type = pluk_base_Bool;
  return result;
}

/* extern bool OperatorNotEquals(Int other) */
pref pluk_base_Int__OperatorNotEquals(pref this, pref data)
{
  pref result;
  if (longFromPref(this) != longFromPref(data))
    result.value = (size_t*)true;
  else 
    result.value = (size_t*)false;    
  result.type = pluk_base_Bool;
  return result;
}

/* extern Int OperatorAdd(Int other) */
pref pluk_base_Int__OperatorAdd(pref this, pref data)
{
  pref result;
  result.value = (size_t*)(longFromPref(this) + longFromPref(data));
  result.type = pluk_base_Int;
  return result;
}

/* extern Int OperatorSubtract(Int other) */
pref pluk_base_Int__OperatorSubtract(pref this, pref data)
{
  pref result;
  result.value = (size_t*)(longFromPref(this) - longFromPref(data));
  result.type = pluk_base_Int;
  return result;
}

/* extern Int OperatorMultiply(Int other) */
pref pluk_base_Int__OperatorMultiply(pref this, pref data)
{
  pref result;
  result.value = (size_t*)(longFromPref(this) * longFromPref(data));
  result.type = pluk_base_Int;
  return result;
}

/* extern Int OperatorDivide(Int other) */
pref pluk_base_Int__OperatorDivide(pref this, pref data)
{
  pref result;
  result.value = (size_t*)(longFromPref(this) / longFromPref(data));
  result.type = pluk_base_Int;
  return result;
}

/* extern Int OperatorModulo(Int other) */
pref pluk_base_Int__OperatorModulo(pref this, pref data)
{
  long a = longFromPref(this);
  long b = longFromPref(data); 
  long c = a % b;
  
//  fprintf(stdout, "%ld %% %ld = %ld\n", a, b, c);
//  fflush(stdout);
      
  return longToPref(c);
}

/* extern Int OperatorLeft(Int other) */
pref pluk_base_Int__OperatorLeft(pref this, pref data)
{
  pref result;
  result.value = (size_t*)(longFromPref(this) << longFromPref(data));
  result.type = pluk_base_Int;
  return result;
}

/* extern Int OperatorRight(Int other) */
pref pluk_base_Int__OperatorRight(pref this, pref data)
{
  pref result;
  result.value = (size_t*)(longFromPref(this) >> longFromPref(data));
  result.type = pluk_base_Int;
  return result;
}

/* extern Int Pow(Int power) */
pref pluk_base_Int__Pow(pref this, pref data)
{
  long a = longFromPref(this);
  long b = longFromPref(data);
  long c = 1;
  while (b > 0)
  {
    c *= a;
    --b;
  }
  return longToPref(c);
}

/* extern String ToString() */
pref pluk_base_Int__ToString(pref this)
{
  const size_t bufsize = sizeof(size_t) * 5;
  int len;
  pref result;
  result.value =  pluk_allocateGC(0, bufsize, 0);
  len = sprintf((char*)&result.value[1],"%ld", longFromPref(this));
  result.value[0] = (size_t)len;
  result.type = pluk_base_String;
  return result;
}

/* extern String ToHexString() */
pref pluk_base_Int__ToHexString(pref this)
{
  const size_t bufsize = sizeof(size_t) * 5;
  int len;
  pref result;
  result.value =  pluk_allocateGC(0, bufsize, 0);
  len = sprintf((char*)&result.value[1],"%lx", longFromPref(this));
  result.value[0] = (size_t)len;
  result.type = pluk_base_String;
  return result;
}

pref pluk_base_Int__GetMaxValue(pref this)
{
  return longToPref(LONG_MAX);
}

pref pluk_base_Int__GetMinValue(pref this)
{
  return longToPref(LONG_MIN);
}

/* extern Int OperatorNegate() */
pref pluk_base_Int__OperatorNegate(pref this)
{
  pref result;
  result.value = (size_t*)(-longFromPref(this));
  result.type = pluk_base_Int;
  return result;
}

/* extern static int? Parse(string value, int base) */
pref pluk_base_Int__InnerParse(pref this, pref value, pref base)
{
  char* v = cstrFromPref(value);
  char* ep;
  long r = strtol(v, &ep, longFromPref(base));
  if ((v[0] != '\0') && (ep[0] == '\0'))
    return longToPref(r);
  return nullToPref();
}

