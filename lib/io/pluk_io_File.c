#include <pluk.h>

#include <stdio.h>
#include <string.h>
#include <unistd.h>
#include <fcntl.h>
#include <errno.h>
#include <sys/types.h>
#include <sys/stat.h>
#include <time.h>


pref pluk_io_File__InnerRename(pref this, pref sourceFilename, pref targetFilename)
{
  if (-1 == rename(cstrFromPref(sourceFilename), cstrFromPref(targetFilename)))
  {
    return longToPref(errno);
  }
  return longToPref(0);
}

pref pluk_io_File__InnerDelete(pref this, pref filename)
{
  if (-1 == unlink(cstrFromPref(filename)))
  {
    return longToPref(errno);
  }
  return longToPref(0);
}

pref pluk_io_File__InnerGetErrorMessage(pref this, pref error)
{
#ifndef pwin32
  long err = longFromPref(error);
  char buf[1024];
  if (0 == strerror_r(err, buf, 1024))
      return cstrToPref(buf);
  return emptyString;
#else
  return cstrToPref(strerror(longFromPref(error)));
#endif
}


// [0] int errno
// [1] int size
// [2] string filename
// [3] int tag
// [4] string lastModified rfc2822

pref pluk_io_FileMetadataImpl__Retrieve(pref this)
{
  pref result;
  result.type = 0;
  result.value = 0;
      
  struct stat stbuf;
  if (0 == stat(cstrFromPref(fieldFromPref(this, 2)), &stbuf))
  {
    fieldFromPref(this, 1) = longToPref(stbuf.st_size);
    fieldFromPref(this, 3) = longToPref(stbuf.st_size ^ ((off_t)stbuf.st_mtime));

    char buf[256];
    buf[255] = '\0';
    buf[254] = '\0';
    struct tm *ts;
    // not thread safe :(
    ts = localtime(&stbuf.st_mtime);
    strftime(&buf[0], 255, "%a, %d %b %Y %H:%M:%S %z", ts);
    fieldFromPref(this, 4) = cstrToPref(buf);
    
    return result;
  }
  fieldFromPref(this, 0) = longToPref(errno);
  return result;
}                            

/* extern sting GetErrorMessage(pref this) */
pref pluk_io_FileMetadataImpl__GetErrorMessage(pref this)
{
#ifndef pwin32
  long error;
  char buf[1024];
  error = longFromPref(fieldFromPref(this, 0));
  if (0 == strerror_r(error, buf, 1024))
      return cstrToPref(buf);
  return emptyString;
#else
  return cstrToPref(strerror(longFromPref(fieldFromPref(this, 0))));
#endif
}

pref pluk_io_FileMetadataImpl__GetFileExists(pref this)
{
  int err = longFromPref(fieldFromPref(this, 0));
  if (err == 0)
    return boolToPref(true);
  if (err == ENOENT)
    return boolToPref(false);
  return nullToPref();
}
