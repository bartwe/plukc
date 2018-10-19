#include <pluk.h>

/* extern int HashCode() */
pref pluk_base_Object__HashCode(pref this)
{
  pref result;
  result.type = pluk_base_Int;
  result.value = (size_t*)((long)this.type ^ (long)this.value);
  return result;
}

/* extern Type GetType() */
pref pluk_base_Object__GetType(pref this)
{
  pref result;
  result.type = pluk_base_Type;
  result.value = (size_t*)this.type[2];
  return result;
}

/* extern bool OperatorEquals(Object)*/
pref pluk_base_Object__OperatorEquals(pref this, pref other)
{
  return boolToPref((this.type == other.type) && (this.value == other.value));
}

/* extern bool OperatorNotEquals(Object)*/
pref pluk_base_Object__OperatorNotEquals(pref this, pref other)
{
  return boolToPref((this.type != other.type) || (this.value != other.value));
}
