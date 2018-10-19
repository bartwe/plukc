#include <pluk.h>

#include <errno.h>
#include <unistd.h>
#include <sys/time.h>
#include <sys/types.h>
#include <sys/select.h>

pref pluk_net_ReadSocketWaitable__InnerTest(pref this)
{
  int handle = longFromPref(fieldFromPref(this, 0));
  fd_set readSet;
  FD_ZERO(&readSet);
  FD_SET(handle, &readSet);
  int res;
  struct timeval timeout;
  timeout.tv_sec = 0;
  timeout.tv_usec = 0;
  res = select(FD_SETSIZE, &readSet, NULL, NULL, &timeout);
  return boolToPref(res != 0);
}

