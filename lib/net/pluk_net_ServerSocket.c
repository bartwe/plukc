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

// [0] int handle
// [1] int errorno
// [2] int errorkind

// private bool Open(string port)
pref pluk_net_ServerSocket__InnerOpen(pref this, pref port)
{
  int sockfd;
  struct addrinfo hints, *servinfo, *p;
  int rv;
  int ra;

  memset(&hints, 0, sizeof hints);
  hints.ai_family = AF_UNSPEC;
  hints.ai_socktype = SOCK_STREAM;
  hints.ai_flags = AI_PASSIVE; // use my IP address
  hints.ai_protocol = IPPROTO_TCP;

  if ((rv = getaddrinfo(NULL, cstrFromPref(port), &hints, &servinfo)) != 0)
  {
    fieldFromPref(this, 0) = longToPref(0);
    fieldFromPref(this, 1) = longToPref(rv);
    fieldFromPref(this, 2) = longToPref(1);
    return boolToPref(false);
  }
  int error = 0;
  // loop through all the results and bind to the first we can
  for(p = servinfo; p != NULL; p = p->ai_next)
  {
    if ((sockfd = socket(p->ai_family, p->ai_socktype, p->ai_protocol)) == -1)
    {
      error = errno;
      continue;
    }
    ra = 1;
    if (setsockopt(sockfd, SOL_SOCKET, SO_REUSEADDR, &ra, sizeof(ra)) == -1)
    {
      error = errno;
      close(sockfd);
      continue;
    }
    if (bind(sockfd, p->ai_addr, p->ai_addrlen) == -1)
    {
      error = errno;
      close(sockfd);
      continue;
    }
    break; // if we get here, we must have connected successfully
  }
  if (p == NULL)
  {
    fieldFromPref(this, 0) = longToPref(0);
    fieldFromPref(this, 1) = longToPref(error);
    fieldFromPref(this, 2) = longToPref(0);
    return boolToPref(false);  
  }
  error = 0;
  if (listen(sockfd, 2) == -1)
  {
    error = errno;
    close(sockfd);    
  }
  if (!error)
  {
    int flags;
    flags = fcntl(sockfd, F_GETFL, 0);
    if (flags != -1)
    {
      flags |= O_NONBLOCK;
      fcntl(sockfd, F_SETFL, flags);
    }
    else
    {
      error = errno;
      close(sockfd);
    }
  }
  if (error)
  {
    fieldFromPref(this, 0) = longToPref(0);
    fieldFromPref(this, 1) = longToPref(errno);
    fieldFromPref(this, 2) = longToPref(0);
    return boolToPref(false);  
  }
  fieldFromPref(this, 0) = longToPref(sockfd);
  return boolToPref(true);
}

// private int Accept()
pref pluk_net_ServerSocket__InnerAccept(pref this)
{
  struct sockaddr_in from;
  int g;
  socklen_t len;
  int fd;
  
  fd = longFromPref(fieldFromPref(this, 0));
  len = sizeof(from);
  g = accept(fd, (struct sockaddr*) &from, &len);
  if (g == -1)
  {
    int error;
    error = errno;
    if (error == EWOULDBLOCK) 
      return longToPref(0);
    fieldFromPref(this, 1) = longToPref(error);
    return longToPref(-1);
  }
  return longToPref(g);
}

pref pluk_net_ServerSocket__InnerClose(pref this)
{
  int fd;
  fd = longFromPref(fieldFromPref(this, 0));
  fieldFromPref(this, 0) = longToPref(0);
  if (-1 == close(fd))
  {
    fieldFromPref(this, 1) = longToPref(errno);
    return boolToPref(false);
  }
  return boolToPref(true);
}

pref pluk_net_ServerSocket__InnerGetErrorMessage(pref this)
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
