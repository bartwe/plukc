#include <pluk.h>

/* extern static int ArgumentCount() */
pref pluk_base_ApplicationHelper__ArgumentCount(pref this)
{
  pref result;
  result.value = (size_t*)pluk_base_argc;
  result.type = pluk_base_Int;
  return result;
}

/* extern static string ArgumentValue(int index) */
pref pluk_base_ApplicationHelper__ArgumentValue(pref this, pref index)
{
  size_t i = sizetFromPref(index);
  if (i >= pluk_base_argc)
    return cstrToPref("");
  else
    return cstrToPref(pluk_base_argv[i]);
}

/* extern static int EnvironmentCount() */
pref pluk_base_ApplicationHelper__EnvironmentCount(pref this)
{
  pref result;
  long count = 0;
  result.type = pluk_base_Int;
  while (pluk_base_environ[count])
    count++;
  result.value = (size_t*)count;
  return result;  
}

/* extern static int EnvironmentValue(int index) */
pref pluk_base_ApplicationHelper__EnvironmentValue(pref this, pref index)
{
  return cstrToPref(pluk_base_environ[sizetFromPref(index)]);
}
