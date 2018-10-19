#include <pluk.h>

#include <errno.h>
#include <unistd.h>
#include <sys/time.h>
#include <sys/types.h>
#ifdef pwin32
#include <windows.h>
#endif

pref pluk_io_Waitable__InnerYieldFast(pref this)
{
#ifdef pwin32
  SleepEx(1, FALSE);
#else
  usleep(1000);
#endif
  return nullToPref();
}

pref pluk_io_Waitable__InnerYield(pref this)
{
#ifdef pwin32
  SleepEx(10, FALSE);
#else
  usleep(10000);
#endif
  return nullToPref();
}


