#include <pluk.h>

/* extern void Write(String data) */
pref pluk_base_Console__Write(pref this, pref data)
{
  pref result;
  result.type = 0;
  result.value = 0;

  fprintf(stdout, "%s", (char*)(&data.value[1]));
  fflush(stdout);
 
  return result;
}

/* extern void WriteError(String data) */
pref pluk_base_Console__WriteError(pref this, pref data)
{
  pref result;
  result.type = 0;
  result.value = 0;

  fprintf(stderr, "%s", (char*)(&data.value[1]));
  fflush(stderr);
 
  return result;
}

char buffer[64*1024+1];

/* extern String InnerReadLine() */
pref pluk_base_Console__InnerReadLine(pref this)
{
  char* result = fgets(buffer, sizeof(buffer), stdin);
  return cstrToPref(result);
}

/* extern bool InnerReadEof() */
pref pluk_base_Console__InnerReadEof(pref this)
{
  return boolToPref(feof(stdin) != 0);
}

