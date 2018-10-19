#include <pluk.h>

#ifdef pwin32

pref lpwstrToPref(LPWSTR str)
{
  pref result;
  result.type = pluk_base_String;
  int len = WideCharToMultiByte(CP_UTF8, 0, str, -1, NULL, 0, NULL, NULL);
  if (len > 0)
    len--; // remove trailing nul char
  
  result.value = pluk_allocateGC(0, len + sizeof(size_t) + 1, 0);
  result.value[0] = len;
  if (len)
    WideCharToMultiByte(CP_UTF8, 0, str, -1, (LPSTR)(&result.value[1]), len + 1, NULL, NULL);
  return result;  
}

#endif

pref cstrnToPref(const char* cstr, size_t len)
{
  pref result;
  result.type = pluk_base_String;
  result.value = pluk_allocateGC(0, len + sizeof(size_t) + 1, 0);
  result.value[0] = len;
  if (len)
    memcpy(&result.value[1], cstr, len);
  return result;  
}

pref cstrToPref(const char* cstr)
{
  if (cstr)
    return cstrnToPref(cstr, strlen(cstr));
  return cstrnToPref(cstr, 0);
}

pref strToPref(size_t* str)
{
  pref result;
  result.type = pluk_base_StaticString;
  result.value = str;
  return result;
}

pref pluk_base_String__HashCode(pref this)
{
  char* st = cstrFromPref(this);
  long len = this.value[0];
  long hash = len;
  if (len > 64)
    len = 64;
  long i = 0;
  while (i < len)
  {
    hash = hash * 13 + (long)st[i];
    i = i + 1;
  }
  return longToPref(hash);      
}

/* extern int GetLength() */
pref pluk_base_String__GetLength(pref this)
{
  pref result;
  result.value = (size_t*)this.value[0];
  result.type = pluk_base_Int;
  return result;
}

/* extern string OperatorAddInner(String other) */
pref pluk_base_String__OperatorAddInner(pref this, pref other)
{
  pref result;
  result.type = pluk_base_String;
  result.value = pluk_allocateGC(0, this.value[0] + other.value[0] + sizeof(size_t) + 1, 0);
  result.value[0] = this.value[0] + other.value[0];
  memcpy(&result.value[1], &this.value[1], this.value[0]);
  memcpy((void*)(((size_t)&result.value[1])+this.value[0]), &other.value[1], other.value[0]);
  return result;
}

/* extern bool OperatorEquals(String other) */
pref pluk_base_String__OperatorEquals(pref this, pref other)
{
  pref result;
  result.type = pluk_base_Bool;
  if ((other.type == 0)
  || (other.value[0] != this.value[0])
  || (0 != strcmp(cstrFromPref(this), cstrFromPref(other))))
    result.value = (size_t*)false;
  else
    result.value = (size_t*)true;
  return result;
}

/* extern int CompareOrdinal(String other) */
pref pluk_base_String__CompareOrdinal(pref this, pref other)
{
  pref result;
  result.type = pluk_base_Int;
  size_t lent = this.value[0];
  size_t leno = other.value[0];
  size_t len = lent;
  if (len > leno)
    len = leno;

  char* st = cstrFromPref(this);
  char* so = cstrFromPref(this);
  
  for (size_t i=0; i<len; ++i)
  {
    char ct = st[i];
    char co = so[i];
    if (ct == co)
      continue;
    if (ct < co)
      result.value = (size_t*)-1;
    else
      result.value = (size_t*)1;
    return result;
  }
    
  if (lent < leno)
    result.value = (size_t*)-1;
  else if (lent > leno)
    result.value = (size_t*)1;
  else
    result.value = (size_t*)0;
  return result;
}

/* extern int HashcodeOrdinal() */
pref pluk_base_String__HashCodeOrdinal(pref this)
{
  char* st = cstrFromPref(this);
  long len = this.value[0];
  long hash = len;
  if (len > 64)
    len = 64;
  long i = 0;
  while (i < len)
  {
    hash = hash * 13 + (long)st[i];
    i = i + 1;
  }
  return longToPref(hash);      
}

/* extern int CompareOrdinalIgnoreCase(String other) */
pref pluk_base_String__CompareOrdinalIgnoreCase(pref this, pref other)
{
  pref result;
  result.type = pluk_base_Int;
  size_t lent = this.value[0];
  size_t leno = other.value[0];
  size_t len = lent;
  if (len > leno)
    len = leno;

  char* st = cstrFromPref(this);
  char* so = cstrFromPref(other);
  
  for (size_t i=0; i<len; ++i)
  {
    char ct = st[i];
    char co = so[i];
    if ((ct>=65)&&(ct<=90))
      ct += 32;
    if ((co>=65)&&(co<=90))
      co += 32;
    if (ct == co)
      continue;
    if (ct < co)
      result.value = (size_t*)-1;
    else
      result.value = (size_t*)1;
    return result;
  }
    
  if (lent < leno)
    result.value = (size_t*)-1;
  else if (lent > leno)
    result.value = (size_t*)1;
  else
    result.value = (size_t*)0;
  return result;
}

/* extern int HashcodeOrdinalIgnoreCase() */
pref pluk_base_String__HashCodeOrdinalIgnoreCase(pref this)
{
  char* st = cstrFromPref(this);
  long len = this.value[0];
  long hash = len;
  if (len > 64)
    len = 64;
  long i = 0;
  while (i < len)
  {
    char ct = st[i];
    if ((ct>=65)&&(ct<=90))
      ct += 32;
    hash = hash * 13 + (long)ct;
    i = i + 1;
  }
  return longToPref(hash);      
}

pref pluk_base_String__ToUpperOrdinal(pref this)
{
  pref result = cstrnToPref(cstrFromPref(this), this.value[0]);
  // we change the string inplace, this is only allowed here because there is no sharing
  char* buf = cstrFromPref(result);
  size_t len = this.value[0];
  for (size_t i = 0; i < len; ++i)
  {
    char c = buf[i];
    if ((c >= 97) && (c <= 122))
      buf[i] = c - 32;
  }
  return result;
}

pref pluk_base_String__ToLowerOrdinal(pref this)
{
  pref result = cstrnToPref(cstrFromPref(this), this.value[0]);
  char* buf = cstrFromPref(result);
  size_t len = this.value[0];
  for (size_t i = 0; i < len; ++i)
  {
    char c = buf[i];
    if ((c >= 65) && (c <= 90))
      buf[i] = c + 32;
  }
  return result;
}

pref pluk_base_String__Trim(pref this)
{
  pref result = cstrnToPref(cstrFromPref(this), this.value[0]);
  char* buf = cstrFromPref(result);
  size_t len = this.value[0];
  
  size_t start = 0;
  size_t end = 0;
  
  size_t c = len;
  
  while (c > 0)
  {
    char c = buf[start];
    if (!((c == 9) || (c == 32) || (c == 10) || (c == 13)))
      break;
    start++;
    c--;
  }
  
  while (c > 0)
  {
    char c = buf[len - (1 + end)];
    if (!((c == 9) || (c == 32) || (c == 10) || (c == 13)))
      break;
    end++;
    c--;
  }
  
  if ((start == 0) && (end == 0))
    return this;
  return cstrnToPref(&buf[start], len - (start + end));
}

/* extern string SubString(int offset, int length) */
pref pluk_base_String__InnerSubString(pref this, pref offset, pref length)
{
  pref result;
  result.type = pluk_base_String;
  result.value = pluk_allocateGC(0, sizetFromPref(length) + sizeof(size_t) + 1, 0);
  result.value[0] = sizetFromPref(length);
  memcpy(cstrFromPref(result), &cstrFromPref(this)[sizetFromPref(offset)], sizetFromPref(length));
  return result;  
}

/* extern Int Pos(string substring, int offset) */
pref pluk_base_String__InnerPos(pref this, pref substring, pref offset)
{
  char* r;
  pref result;
  result.type = pluk_base_Int;
  r = strstr(&cstrFromPref(this)[sizetFromPref(offset)], cstrFromPref(substring));
  if (r == 0)
    result.value = (size_t*)-1;
  else
    result.value = (size_t*)((long)r - (long)cstrFromPref(this));
  return result;  
}

