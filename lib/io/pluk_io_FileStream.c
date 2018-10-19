#include <pluk.h>

#include <stdio.h>
#include <string.h>
#include <unistd.h>
#include <fcntl.h>
#include <errno.h>
#include <sys/types.h>
#include <sys/stat.h>

// [0] int filehandle
// [1] int errorno

/* extern void Open(string name, bool read, bool write, bool append, bool create, bool createOnly, bool truncate) */
pref pluk_io_InnerFileStream__Open(pref this, pref filename, 
  pref read, pref write, pref append, pref create, pref createOnly, pref truncate)
{
  int flags = 0;
  char* file;
  if (boolFromPref(read) && boolFromPref(read))
    flags |= O_RDWR;
  else
  {
    if (boolFromPref(read))
      flags |= O_RDONLY;
    if (boolFromPref(read))
      flags |= O_WRONLY;
  }
  if (boolFromPref(append))
    flags |= O_APPEND;
  if (boolFromPref(create))
    flags |= O_CREAT;
  if (boolFromPref(createOnly))
    flags |= O_EXCL;
  if (boolFromPref(truncate))
    flags |= O_TRUNC;

#ifndef pwin32
  flags |= O_NOCTTY;
  flags |= O_NONBLOCK;
#endif
  
  file = cstrFromPref(filename);
  
  int handle = open(file, flags, S_IRUSR | S_IWUSR
#ifndef pwin32
 | S_IRGRP | S_IROTH
#endif
);
  int error = 0;
  if (handle == -1)
  {
    handle = 0;
    error = errno;
  }
  
  fieldFromPref(this, 0) = longToPref(handle);
  fieldFromPref(this, 1) = longToPref(error);
  
  return nullToPref();
}

/* extern sting GetErrorMessage(pref this) */
pref pluk_io_InnerFileStream__GetErrorMessage(pref this)
{
#ifndef pwin32
  long error;
  char buf[1024];
  error = longFromPref(fieldFromPref(this, 1));
  if (0 == strerror_r(error, buf, 1024))
      return cstrToPref(buf);
  return emptyString;
#else
  return cstrToPref(strerror(longFromPref(fieldFromPref(this, 1))));
#endif
}

/* extern int Read(Array<Byte> buffer, int offset, int limit) */
pref pluk_io_InnerFileStream__Read(pref this, pref buffer, pref offset, pref limit)
{
  int handle = longFromPref(fieldFromPref(this, 0));
#ifdef pwin32
  int len = sizetFromPref(limit);
  //TODO: Complete crap, mingw doesn't expose nonblocking file reading ?
  if (len > 0)
    len = 1;
  ssize_t res = read(handle, &(bptrFromPref(buffer)[longFromPref(offset)]), len);
  if (res < 0)
  {
    int error = errno;
    fieldFromPref(this, 1) = longToPref(error);
    res = -2;
  }
  else if (res == 0)
    res = -1;
  return longToPref(res);
#else
  ssize_t res = read(handle, &(bptrFromPref(buffer)[longFromPref(offset)]), sizetFromPref(limit));
  if (res < 0)
  {
    int error = errno;
    if (error == EAGAIN)
      res = 0;
    else
    {
      res = -2;
      fieldFromPref(this, 1) = longToPref(error);
    }
  }
  else if (res == 0)
    res = -1;
  return longToPref(res);
#endif
}

/* extern int Write(Array<Byte> buffer, int offset, int limit) */
pref pluk_io_InnerFileStream__Write(pref this, pref buffer, pref offset, pref limit)
{
  int handle = longFromPref(fieldFromPref(this, 0));
#ifdef pwin32
  ssize_t res = write(handle, &(bptrFromPref(buffer)[longFromPref(offset)]), sizetFromPref(limit));
  if (res < 0)
  {
    int error = errno;
    res = -2;
    fieldFromPref(this, 1) = longToPref(error);
  }
  else if (res == 0)
    res = -1;
  return longToPref(res);
#else
  ssize_t res = write(handle, &(bptrFromPref(buffer)[longFromPref(offset)]), sizetFromPref(limit));
  if (res < 0)
  {
    int error = errno;
    if (error == EAGAIN)
      res = 0;
    else
    {
      res = -2;
      fieldFromPref(this, 1) = longToPref(error);
    }
  }
  else if (res == 0)
    res = -1;
  return longToPref(res);
#endif
}

/* extern bool Close() */
pref pluk_io_InnerFileStream__Close(pref this)
{
  pref result;
  int handle = longFromPref(fieldFromPref(this, 0));
  if (close(handle) < 0)
  {
    fieldFromPref(this, 1) = longToPref(errno);
    result = boolToPref(false);
  }
  else
    result = boolToPref(true);
  fieldFromPref(this, 0) = longToPref(0);
  return result;
}

/* extern bool SetPosition(int position) */
pref pluk_io_InnerFileStream__SetPosition(pref this, pref position)
{
  pref result;
  int handle = longFromPref(fieldFromPref(this, 0));
  int pos = longFromPref(position);
  if (lseek(handle, pos, SEEK_SET) < 0)
  {
    fieldFromPref(this, 1) = longToPref(errno);
    result = boolToPref(false);
  }
  else
    result = boolToPref(true);
  return result;
}

/* extern int GetPosition() */
pref pluk_io_InnerFileStream__GetPosition(pref this)
{
  int handle = longFromPref(fieldFromPref(this, 0));
  int pos = lseek(handle, 0, SEEK_CUR);
  if (pos < 0)
  {
    fieldFromPref(this, 1) = longToPref(errno);
  }
  return longToPref(pos);
}

