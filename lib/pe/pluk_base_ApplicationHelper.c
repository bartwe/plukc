#include <pluk.h>
#include <windows.h>
#include <stdio.h>
#include <shellapi.h>

/* extern static int ArgumentCount() */
pref pluk_base_ApplicationHelper__ArgumentCount(pref this)
{
  LPWSTR *szArglist;
  int nArgs;
  szArglist = CommandLineToArgvW(GetCommandLineW(), &nArgs);
  LocalFree(szArglist);
  return longToPref(nArgs);
}

/* extern static string ArgumentValue(int index) */
pref pluk_base_ApplicationHelper__ArgumentValue(pref this, pref index)
{
  //BUG
  //should use the W functions and convert to utf8
  pref result;
  LPWSTR *szArglist;
  int nArgs;
  int i;
  szArglist = CommandLineToArgvW(GetCommandLineW(), &nArgs);
  i = longFromPref(index);
  if (i < nArgs)
    result = lpwstrToPref(szArglist[i]);
  else
    result = emptyString;
  LocalFree(szArglist);
  return result;
}

/* extern static int EnvironmentCount() */
pref pluk_base_ApplicationHelper__EnvironmentCount(pref this)
{
  pref result;
  long count = 0;
  result.type = pluk_base_Int;
//  while (pluk_base_environ[count])
//    count++;
  result.value = (size_t*)count;
  return result;  
}

/* extern static int EnvironmentValue(int index) */
pref pluk_base_ApplicationHelper__EnvironmentValue(pref this, pref index)
{
//  return cstrToPref(pluk_base_environ[sizetFromPref(index)]);
  return cstrToPref("");
}
