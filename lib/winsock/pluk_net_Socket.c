#include <pluk.h>

#if !defined(_WIN32_WINNT) || (_WIN32_WINNT < 0x0501)
#undef _WIN32_WINNT
#define _WIN32_WINNT 0x0501
#endif

#include <windows.h>
#include <winsock2.h>
#include <ws2tcpip.h>
#include <stdio.h>


int pluk_net_Socket_Startup();
void pluk_net_Socket_Shutdown();

// [0] int handle
// [1] int wsaerror
// [2] int reserved

// bool InnerOpenSocket(string host, string port)
pref pluk_net_Socket__InnerOpenSocket(pref this, pref host, pref port)
{
  int error = pluk_net_Socket_Startup();
  if (error)
  {
    fieldFromPref(this, 0) = longToPref(0);
    fieldFromPref(this, 1) = longToPref(error);
      return boolToPref(false);
  }

  int sockfd;
  struct addrinfo hints, *servinfo, *p;
  int rv;

  memset(&hints, 0, sizeof(hints));
  hints.ai_family = AF_UNSPEC;
  hints.ai_socktype = SOCK_STREAM;
  hints.ai_protocol = IPPROTO_TCP;

  //BUG use W variant

  if ((rv = getaddrinfo(cstrFromPref(host), cstrFromPref(port), &hints, &servinfo)) != 0)
  {
    fieldFromPref(this, 0) = longToPref(0);
    fieldFromPref(this, 1) = longToPref(WSAGetLastError());
    return boolToPref(false);
  }
  error = 0;
  // loop through all the results and connect to the first we can
  for(p = servinfo; p != NULL; p = p->ai_next)
  {
    if ((sockfd = WSASocket(p->ai_family, p->ai_socktype, p->ai_protocol, NULL, 0, WSA_FLAG_OVERLAPPED)) == -1)
    {
      error = WSAGetLastError();
      continue;
    }
    int ipv6only = 0;
    if (setsockopt(sockfd, IPPROTO_IPV6,  27//IPV6_V6ONLY
    , (char*)&ipv6only, sizeof(ipv6only)) == -1)
    {
      error = WSAGetLastError();
      closesocket(sockfd);
      continue;
    }
    if (connect(sockfd, p->ai_addr, p->ai_addrlen) == -1)
    {
      error = WSAGetLastError();
      closesocket(sockfd);
      continue;
    }
    break; // if we get here, we must have connected successfully
  }
  if (p == NULL)
  {
    // looped off the end of the list with no connection
    // retry or raise first error
    fieldFromPref(this, 0) = longToPref(0);
    fieldFromPref(this, 1) = longToPref(error);
    return boolToPref(false);
  }
  freeaddrinfo(servinfo); // all done with this structure
  
  u_long status = 1;
  if (SOCKET_ERROR == ioctlsocket(sockfd, FIONBIO, &status))  //set the socket in non-blocking mode
  {
    fieldFromPref(this, 0) = longToPref(0);
    fieldFromPref(this, 1) = longToPref(error);
    closesocket(sockfd);
    return boolToPref(false);
  }
  
  fieldFromPref(this, 0) = longToPref(sockfd);
  return boolToPref(true);
}

// private bool Open(int port)
pref pluk_net_Socket__InnerOpen(pref this, pref handle)
{
  int error = pluk_net_Socket_Startup();
  if (error)
  {
    fieldFromPref(this, 0) = longToPref(0);
    fieldFromPref(this, 1) = longToPref(error);
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
  if (closesocket(fd) == SOCKET_ERROR)
  {
    fieldFromPref(this, 0) = longToPref(0);
    fieldFromPref(this, 1) = longToPref(WSAGetLastError());
    return boolToPref(false);
  }
  pluk_net_Socket_Shutdown();
  return boolToPref(true);
}

pref pluk_net_Socket__InnerCloseForWriting(pref this)
{
  int fd;
  fd = longFromPref(fieldFromPref(this, 0));
  if (shutdown(fd, SD_SEND) == SOCKET_ERROR)
  {
    int err = WSAGetLastError();
    if ((err != WSAENOTCONN) && (err != WSAECONNRESET))
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
  long error;
  error = longFromPref(fieldFromPref(this, 1));
  char Message[1024];
  FormatMessage(FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS |
    FORMAT_MESSAGE_MAX_WIDTH_MASK, NULL, error,
    MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
    (LPSTR) Message, 1024, NULL);
  return cstrToPref(Message);
}

// int Write(Array<byte> buffer, int offset, int limit)
pref pluk_net_Socket__InnerWrite(pref this, pref buffer, pref offset, pref limit)
{
  SOCKET handle = longFromPref(fieldFromPref(this, 0));
  int res = send(handle, (void*)&(bptrFromPref(buffer)[longFromPref(offset)]), sizetFromPref(limit), 0);
  if (res == SOCKET_ERROR)
  {
    int error = WSAGetLastError();
    if (error == WSAEWOULDBLOCK)
    {
      res = 0;
    }
    else
    {
      res = -2;
      fieldFromPref(this, 1) = longToPref(error);
    }
  }
  else if (res == 0)
  {
    res = -2;
    fieldFromPref(this, 1) = longToPref(0);
  }
  return longToPref(res);
}

pref pluk_net_Socket__InnerRead(pref this, pref buffer, pref offset, pref limit)
{
  SOCKET handle = longFromPref(fieldFromPref(this, 0));
  int res = recv(handle, (void*)&(bptrFromPref(buffer)[longFromPref(offset)]), sizetFromPref(limit), 0);
  if (res == SOCKET_ERROR)
  {
    int error = WSAGetLastError();
    if (error == WSAEWOULDBLOCK)
    {
      res = 0;
    }
    else if (error == WSAECONNRESET)
    {
      res = -1;
    }
    else
    {
      res = -2;
      fieldFromPref(this, 1) = longToPref(error);
    }
  }
  else if (res == 0)
    res = -1;
  return longToPref(res);
}
