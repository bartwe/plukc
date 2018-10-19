#include <pluk.h>

#include <errno.h>
#include <sys/types.h>
#include <unistd.h>
#include <fcntl.h>
#include <string.h>
#include <netdb.h>
#include <sys/socket.h>
#include <netinet/in.h>
#include <arpa/inet.h>
#include <netinet/tcp.h>

// [0] int handle
// [1] int errorno
// [2] int errorkind

// bool InnerOpenSocket(string host, string port)
pref pluk_net_Socket__InnerOpenSocket(pref this, pref host, pref port)
{
  int sockfd;  
  struct addrinfo hints, *servinfo, *p;
  int rv;

  memset(&hints, 0, sizeof(hints));
  hints.ai_family = AF_UNSPEC;
  hints.ai_socktype = SOCK_STREAM;
  hints.ai_protocol = IPPROTO_TCP;

  if ((rv = getaddrinfo(cstrFromPref(host), cstrFromPref(port), &hints, &servinfo)) != 0)
  {
    fieldFromPref(this, 0) = longToPref(0);
    fieldFromPref(this, 1) = longToPref(rv);
    fieldFromPref(this, 2) = longToPref(1);
    return boolToPref(false);
  }
  int error = 0;
  // loop through all the results and connect to the first we can
  for(p = servinfo; p != NULL; p = p->ai_next)
  {
    if ((sockfd = socket(p->ai_family, p->ai_socktype, p->ai_protocol)) == -1)
    {
      error = errno;
      continue;
    }
    if (connect(sockfd, p->ai_addr, p->ai_addrlen) == -1)
    {
      error = errno;
      close(sockfd);
      continue;
    }
    break; // if we get here, we must have connected successfully
  }
  freeaddrinfo(servinfo); // all done with this structure
  if (p == NULL)
  {
    // looped off the end of the list with no connection
    // retry or raise first error
    fieldFromPref(this, 0) = longToPref(0);
    fieldFromPref(this, 1) = longToPref(error);
    fieldFromPref(this, 2) = longToPref(0);
    return boolToPref(false);
  }
  
  int flag;
  flag = 1;
  rv = setsockopt(sockfd, IPPROTO_TCP, TCP_NODELAY, (char *)&flag, sizeof(flag));
  
  if (rv == -1)
  {
    close(sockfd);
    fieldFromPref(this, 0) = longToPref(0);
    fieldFromPref(this, 1) = longToPref(rv);
    fieldFromPref(this, 2) = longToPref(0);
    return boolToPref(false);
  }  
  
  fieldFromPref(this, 0) = longToPref(sockfd);
  return boolToPref(true);                  
}

// internal bool Open(int port)
pref pluk_net_Socket__InnerOpen(pref this, pref handle)
{
  int sockfd = longFromPref(handle);
  int flag;
  flag = 1;
  int rv = setsockopt(sockfd, IPPROTO_TCP, TCP_NODELAY, (char *)&flag, sizeof(flag));
  if (rv == -1)
  {
    close(sockfd);
    fieldFromPref(this, 0) = longToPref(0);
    fieldFromPref(this, 1) = longToPref(rv);
    fieldFromPref(this, 2) = longToPref(0);
    return boolToPref(false);
  }  
  fieldFromPref(this, 0) = handle;
  return boolToPref(true);
}

pref pluk_net_Socket__InnerClose(pref this)
{
  int fd;
  fd = longFromPref(fieldFromPref(this, 0));
  fieldFromPref(this, 0) = longToPref(0);
  if (close(fd) == -1)
  {
    fieldFromPref(this, 0) = longToPref(0);
    fieldFromPref(this, 1) = longToPref(errno);
    return boolToPref(false);
  }
  return boolToPref(true);
}

pref pluk_net_Socket__InnerCloseForWriting(pref this)
{
  int fd;
  fd = longFromPref(fieldFromPref(this, 0));
  if (shutdown(fd, SHUT_WR) == -1)
  {
    int err = errno;
    if ((err != ENOTCONN) && (err != ECONNRESET))
    {
      fieldFromPref(this, 0) = longToPref(0);
      fieldFromPref(this, 1) = longToPref(err);
      return boolToPref(false);
    }
  }
  return boolToPref(true);
}

pref pluk_net_Socket__InnerGetErrorMessage(pref this)
{
  long error, kind;
  char buf[1024];
  error = longFromPref(fieldFromPref(this, 1));
  kind = longFromPref(fieldFromPref(this, 2));
  if (0 == kind) // errno
  {
    if (0 == strerror_r(error, buf, 1024))
      return cstrToPref(buf);
  }
  if (1 == kind) // getaddrinfo
  {
    return cstrToPref(gai_strerror(error));
  }
  return emptyString;
}

// int Write(Array<byte> buffer, int offset, int limit)
pref pluk_net_Socket__InnerWrite(pref this, pref buffer, pref offset, pref limit)
{
  int handle = longFromPref(fieldFromPref(this, 0));
  ssize_t res = send(handle, &(bptrFromPref(buffer)[longFromPref(offset)]), sizetFromPref(limit), MSG_NOSIGNAL | MSG_DONTWAIT);
  if (res <= 0)
  {
    int error ;
    if (res == 0)
      error = 0;
    else
      error = errno;
    if (error == EAGAIN)
      res = 0;
    else
    {
      res = -2;
      fieldFromPref(this, 1) = longToPref(error);
    }
  }
  return longToPref(res);
}

pref pluk_net_Socket__InnerRead(pref this, pref buffer, pref offset, pref limit)
{
  int handle = longFromPref(fieldFromPref(this, 0));
  ssize_t res = recv(handle, &(bptrFromPref(buffer)[longFromPref(offset)]), sizetFromPref(limit), MSG_NOSIGNAL | MSG_DONTWAIT);
  if (res < 0)
  {
    int error = errno;
    res = -2;
    if (error == EAGAIN)
      res = 0;
    else if (error == ECONNRESET)
      res = -1;
    else
      fieldFromPref(this, 1) = longToPref(error);
  }
  else if (res == 0)
    res = -1;
  return longToPref(res);
}
