#include <pluk.h>

pref boolToPref(bool truth)
{
  pref result;
  result.type = pluk_base_Bool;
  result.value = (size_t*)(size_t)(truth?1:0);
  return result;
}

/* extern bool OperatorEquals(Bool other) */
pref pluk_base_Bool__OperatorEquals(pref this, pref data)
{
  pref result;
  if (boolFromPref(this) == boolFromPref(data))
    result.value = (size_t*)true;
  else 
    result.value = (size_t*)false;    
  result.type = pluk_base_Bool;
  return result;
}

/* extern bool OperatorNotEquals(Bool other) */
pref pluk_base_Bool__OperatorNotEquals(pref this, pref data)
{
  pref result;
  if (boolFromPref(this) != boolFromPref(data))
    result.value = (size_t*)true;
  else 
    result.value = (size_t*)false;    
  result.type = pluk_base_Bool;
  return result;
}

/* extern bool OperatorAnd(Bool other) */
pref pluk_base_Bool__OperatorAnd(pref this, pref data)
{
  pref result;
  if (boolFromPref(this) && boolFromPref(data))
    result.value = (size_t*)true;
  else 
    result.value = (size_t*)false;    
  result.type = pluk_base_Bool;
  return result;
}

/* extern bool OperatorOr(Bool other) */
pref pluk_base_Bool__OperatorOr(pref this, pref data)
{
  pref result;
  if (boolFromPref(this) || boolFromPref(data))
    result.value = (size_t*)true;
  else 
    result.value = (size_t*)false;    
  result.type = pluk_base_Bool;
  return result;
}

/* extern bool OperatorNot() */
pref pluk_base_Bool__OperatorNot(pref this)
{
  pref result;
  if (boolFromPref(this))
    result.value = (size_t*)false;
  else
    result.value = (size_t*)true;
  result.type = pluk_base_Bool;
  return result;
}

