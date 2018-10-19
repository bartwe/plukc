#include <pluk.h>

#include <stdio.h>
#include <string.h>
#include <unistd.h>
#include <fcntl.h>
#include <errno.h>
#include <sys/types.h>
#include <sys/stat.h>

pref pluk_io_BinaryOStream__InnerBE4B(pref this, pref in, pref array)
{
  char* slot;
  slot = ((char*)(array.value[0]));
  long v = longFromPref(in);
  slot[3] = v & 0xff;
  slot[2] = (v >> 8) & 0xff;
  slot[1] = (v >> 16) & 0xff;
  slot[0] = (v >> 24) & 0xff;
  return nullToPref();
}

