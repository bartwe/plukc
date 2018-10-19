#include <pluk.h>

#include <windows.h>
#include <winsock2.h>

pref pluk_net_WriteSocketWaitable__InnerTest(pref this)
{
  SOCKET handle = longFromPref(fieldFromPref(this, 0));
  fd_set writeSet;
  FD_ZERO(&writeSet);
  FD_SET(handle, &writeSet);
  int res;
  struct timeval timeout;
  timeout.tv_sec = 0;
  timeout.tv_usec = 0;
  res = select(FD_SETSIZE, NULL, &writeSet, NULL, &timeout);
  return boolToPref(res != 0);
}
