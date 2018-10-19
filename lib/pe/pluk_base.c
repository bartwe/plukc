#include <pluk.h>

size_t* pluk_base_Bool;
size_t* pluk_base_Byte;
size_t* pluk_base_Int;
size_t* pluk_base_Float;
size_t* pluk_base_String;
size_t* pluk_base_StaticString;
size_t* pluk_base_Type;
size_t* pluk_base_OverflowException;
size_t* pluk_base_BoundsException;

size_t* pluk_base_stackTraceData;
pref emptyString;

extern size_t* gc_globalsBegin;
extern size_t* gc_globalsEnd;

typedef struct
{
  size_t length;
  char null;
} emptyStringBufferType;

emptyStringBufferType emptyStringBuffer;
    
void pluk_base_exit(int status)
{
  pluk_fullSweepGC(0);
  pluk_fullSweepGC(0);
  exit(status);
}

void pluk_base_setup(size_t* pbbool, size_t* pbbyte, size_t* pbint, size_t* pbfloat, size_t* pbstring, size_t* pbstaticstring, size_t* pbtype, size_t* stackTraceData, size_t* staticDataSlotsBegin, size_t* staticDataSlotsEnd, size_t* overflowType, size_t* boundsType)
{
  pluk_base_Bool = pbbool;
  pluk_base_Byte = pbbyte;
  pluk_base_Int = pbint;
  pluk_base_Float = pbfloat;
  pluk_base_String = pbstring;
  pluk_base_StaticString = pbstaticstring;
  pluk_base_Type = pbtype;
  pluk_base_OverflowException = overflowType;
  pluk_base_BoundsException = boundsType;
  
  pluk_base_stackTraceData = stackTraceData;
  
  emptyStringBuffer.length = 0;
  emptyStringBuffer.null = 0;
  emptyString.type = pluk_base_StaticString;
  emptyString.value = (size_t*)&emptyStringBuffer;
  
  gc_globalsBegin = staticDataSlotsBegin;
  gc_globalsEnd = staticDataSlotsEnd;
}

void pluk_base_saveStackRoot(size_t* stackRoot)
{
}
