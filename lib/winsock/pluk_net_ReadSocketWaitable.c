#include <pluk.h>

#include <windows.h>
#include <winsock2.h>

pref pluk_net_ReadSocketWaitable__InnerTest(pref this)
{
  SOCKET handle = longFromPref(fieldFromPref(this, 0));
  fd_set readSet;
  FD_ZERO(&readSet);
  FD_SET(handle, &readSet);
  int res;
  struct timeval timeout;
  timeout.tv_sec = 0;
  timeout.tv_usec = 0;
  res = select(0, &readSet, NULL, NULL, &timeout);
  return boolToPref(res != 0);
}
