#include <pluk.h>

/* extern private string InnerStringFromByteArray(Array<byte> buffer, int offset, int length) */
pref pluk_base_Utf8Encoding__InnerStringFromByteArray(pref this, pref buffer, pref offset, pref length)
{
  return cstrnToPref((char*)&(bptrFromPref(buffer)[longFromPref(offset)]), sizetFromPref(length));
}

/* extern private int InnerStringToByteArray(string text, Array<byte> buffer, int offset, int length); */
pref pluk_base_Utf8Encoding__InnerStringToByteArray(pref this, pref text, pref buffer, pref offset, pref length)
{
  size_t len = strlenFromPref(text);
  if (len <= sizetFromPref(length))
    memcpy(&bptrFromPref(buffer)[sizetFromPref(offset)], cstrFromPref(text), len);
  return longToPref(len);
}

