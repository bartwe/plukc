#define _XOPEN_SOURCE 600

#include <stdlib.h>
#include <stdbool.h>
#include <stdio.h>
#include <string.h>
#ifdef pwin32
#include <windows.h>
#endif

#ifndef g_pluk_h
#define g_pluk_h

typedef struct
{
  size_t* value;
  size_t* type;
} pref;

extern size_t* pluk_base_Bool;
extern size_t* pluk_base_Byte;
extern size_t* pluk_base_Int;
extern size_t* pluk_base_Float;
extern size_t* pluk_base_String;
extern size_t* pluk_base_StaticString;
extern size_t* pluk_base_Type;
extern size_t  pluk_base_argc;
extern char**  pluk_base_argv;
extern char**  pluk_base_environ;

extern pref emptyString;

#ifdef pluk64
#define pluk_float_t double
#else
#define pluk_float_t float
#endif 

#define longFromPref(number) ((long)(number).value)
pluk_float_t floatFromPref(pref number);
#define sizetFromPref(number) ((size_t)(number).value)
#define ssizetFromPref(number) ((ssize_t)(number).value)
#define boolFromPref(number) (longFromPref(number)?true:false)
#define bptrFromPref(array) ((unsigned char*)((array).value[0]))

#define fieldFromPref(self, offset) (((pref*)(self).value)[(offset)])

pref strToPref(size_t* str);
pref cstrToPref(const char* cstr);
#ifdef pwin32
pref lpwstrToPref(LPWSTR cstr);
#endif
pref cstrnToPref(const char* cstr, size_t len);
#define cstrFromPref(pref) ((char*)(&(pref).value[1]))
#define strlenFromPref(pref) ((size_t)((pref).value[0]))
pref longToPref(long number);
pref floatToPref(pluk_float_t number);
pref boolToPref(bool truth);
pref nullToPref();

size_t* pluk_allocateGC(size_t fieldCount, size_t additionalBytes, size_t* stackTrace);
/* pointer to value/type pair */
void pluk_touchGC(pref* reference);
void pluk_disposeGC(size_t* value, size_t* type);
void pluk_fullSweepGC(size_t* stackTrace);

#endif /* g_pluk_h */
