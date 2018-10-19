#include <pluk.h>

#if !defined(_WIN32_WINNT) || (_WIN32_WINNT < 0x0501)
#undef _WIN32_WINNT
#define _WIN32_WINNT 0x0501
#endif

#include <windows.h>
#include <winsock2.h>
#include <ws2tcpip.h>
#include <stdio.h>


//TODO: use this also for normal sockets
int count = 0;

int pluk_net_Socket_Startup()
{
  count++;
  if (count > 1)
    return 0;
  WORD wVersionRequested;
  WSADATA wsaData;
  wVersionRequested = MAKEWORD(2, 2);
  return WSAStartup(wVersionRequested, &wsaData);
}

void pluk_net_Socket_Shutdown()
{
  count--;
  if (count > 0)
    return;
  WSACleanup();
}

// [0] int handle
// [1] int errorno

// private bool Open(string port)
pref pluk_net_ServerSocket__InnerOpen(pref this, pref port)
{
  int error = pluk_net_Socket_Startup();
  if (error)
  {
    fieldFromPref(this, 0) = longToPref(0);
    fieldFromPref(this, 1) = longToPref(error);
    fieldFromPref(this, 2) = longToPref(0);
    return boolToPref(false);
  }
  
  int sockfd;
  struct addrinfo hints, *servinfo, *p;
  int rv;
  BOOL ra;

  memset(&hints, 0, sizeof hints);
  hints.ai_family = AF_UNSPEC;
  hints.ai_socktype = SOCK_STREAM;
  hints.ai_flags = AI_PASSIVE; // use my IP address
  hints.ai_protocol = IPPROTO_TCP;


  if ((rv = getaddrinfo(NULL, cstrFromPref(port), &hints, &servinfo)) != 0)
  {
    fieldFromPref(this, 0) = longToPref(0);
    fieldFromPref(this, 1) = longToPref(WSAGetLastError());
    fieldFromPref(this, 2) = longToPref(0);
    return boolToPref(false);
  }
  error = 0;
  // loop through all the results and bind to the first we can
  for(p = servinfo; p != NULL; p = p->ai_next)
  {
    if ((sockfd = WSASocket(p->ai_family, p->ai_socktype, p->ai_protocol,  NULL, 0, WSA_FLAG_OVERLAPPED)) == -1)
    {
      error = WSAGetLastError();
      continue;
    }
    ra = 1;
    if (setsockopt(sockfd, SOL_SOCKET, SO_REUSEADDR, (char*)&ra, sizeof(ra)) == -1)
    {
      error = WSAGetLastError();
      closesocket(sockfd);
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
    if (bind(sockfd, p->ai_addr, p->ai_addrlen) == -1)
    {
      error = WSAGetLastError();
      closesocket(sockfd);
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
    error = WSAGetLastError();
    closesocket(sockfd);
  }
  if (!error)
  {
    u_long status = 1;
    if (SOCKET_ERROR == ioctlsocket(sockfd, FIONBIO, &status))  //set the socket in non-blocking mode
    {
      error = WSAGetLastError();
      closesocket(sockfd);
    }
  }
  if (error)
  {
    fieldFromPref(this, 0) = longToPref(0);
    fieldFromPref(this, 1) = longToPref(error);
    fieldFromPref(this, 2) = longToPref(0);
    return boolToPref(false);
  }
  fieldFromPref(this, 0) = longToPref(sockfd);
  return boolToPref(true);
}

// private int Accept()
pref pluk_net_ServerSocket__InnerAccept(pref this)
{
  SOCKET g;
  int fd;
  
  fd = longFromPref(fieldFromPref(this, 0));
  g = accept(fd, NULL, NULL);
  if (g == INVALID_SOCKET)
  {
    int error;
    error = WSAGetLastError();
    if (error == WSAEWOULDBLOCK)
      return longToPref(0);
    fieldFromPref(this, 1) = longToPref(error);
    return longToPref(-1);
  }
  return longToPref(g);
}

pref pluk_net_ServerSocket__InnerClose(pref this)
{
  SOCKET fd;
  fd = longFromPref(fieldFromPref(this, 0));
  fieldFromPref(this, 0) = longToPref(0);
  if (SOCKET_ERROR == closesocket(fd))
  {
    fieldFromPref(this, 1) = longToPref(WSAGetLastError());
    pluk_net_Socket_Shutdown();  
    return boolToPref(false);
  }
  pluk_net_Socket_Shutdown();  
  return boolToPref(true);
}

pref pluk_net_ServerSocket__InnerGetErrorMessage(pref this)
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
