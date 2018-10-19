#include <pluk.h>
#include <time.h>
#include <stdlib.h>

#ifdef pwin32
pref pluk_base_Random__Randomize(pref this)
{
  int r1 = rand();
  int r2 = (int)time(NULL);
  srand(r1 * 13 + r2);
  int r3 = rand();
  srand(r2 * 13 + r3);
  return nullToPref();
}

pref pluk_base_Random__NextInt(pref this)
{
  return longToPref(rand());
}
#else
pref pluk_base_Random__Randomize(pref this)
{
  int r1 = random();
  int r2 = (int)time(NULL);
  srandom(r1 * 13 + r2);
  int r3 = random();
  srandom(r2 * 13 + r3);
  return nullToPref();
}

pref pluk_base_Random__NextInt(pref this)
{
  return longToPref(random());
}

#endif
