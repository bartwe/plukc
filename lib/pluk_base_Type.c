#include <pluk.h>

/* extern int InnerFullName() */
pref pluk_base_Type__InnerFullName(pref this)
{
  pref result;
  if (!this.value)
    return emptyString;
  result.type = pluk_base_StaticString;
  result.value = (size_t*)this.value[1];
  return result;
}
