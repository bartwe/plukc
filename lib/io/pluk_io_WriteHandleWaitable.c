#include <pluk.h>

#include <errno.h>
#include <unistd.h>
#include <sys/time.h>
#ifdef pwin32
#else
#include <sys/select.h>
#endif
#include <sys/types.h>

pref pluk_io_WriteHandleWaitable__InnerTest(pref this)
{
#ifdef pwin32
  // only used for file handles, filehandles are currently blocking only on windows
  return boolToPref(true);
#else
  int handle = longFromPref(fieldFromPref(this, 0));
  fd_set writeSet;
  FD_ZERO(&writeSet);
  FD_SET(handle, &writeSet);
  int res;
  struct timeval timeout;
  timeout.tv_sec = 0;
  timeout.tv_usec = 0;
  res = select(FD_SETSIZE, NULL, &writeSet, NULL, &timeout);
  if (res == -1)
    fieldFromPref(this, 1) = longToPref(errno);
  return boolToPref(res > 0);
#endif
}
