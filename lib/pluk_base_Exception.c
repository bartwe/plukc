#include <pluk.h>

typedef struct
{
  void* retSite;
  size_t line;
  size_t* file;
  size_t* class;
  size_t* field;
} traceinfo;

extern traceinfo* pluk_base_stackTraceData;
bool stackTraceHashBuild = false;
int stackTraceEntryCount = 0;
size_t* stackTraceIndex;
size_t* stackTraceChain;

struct framestruct
{
  struct framestruct* next;
  void* value;
};

typedef struct framestruct frame;

void buildStackTraceHash()
{
  int i = 0;
  traceinfo* ti = pluk_base_stackTraceData;
  while (ti[i].retSite)
    i++;
  stackTraceEntryCount = i;
  stackTraceIndex = (size_t*)calloc(stackTraceEntryCount, sizeof(size_t));
  stackTraceChain = (size_t*)calloc(stackTraceEntryCount, sizeof(size_t));
  i = 0;
  ti = pluk_base_stackTraceData;
  while (ti[i].retSite)
  {
    size_t hash = (size_t)ti[i].retSite;
    hash = hash ^ (hash >> 7); // the low bits are not very informative so lets shift to left and xor, that way the really low bits stay in range, and we get the more intresting higher bits too.
    hash = hash % stackTraceEntryCount;
    stackTraceChain[i] = stackTraceIndex[hash];
    stackTraceIndex[hash] = i;
    i++;
  }
  stackTraceHashBuild = true;
}

traceinfo* findTraceInfo(frame* frame)
{
  if (!stackTraceHashBuild)
    buildStackTraceHash();

  size_t* value = frame->value;    
  size_t hash = (size_t)value;
  hash = hash ^ (hash >> 7);
  hash = hash % stackTraceEntryCount;

  traceinfo* ti = pluk_base_stackTraceData;
  
  int i = stackTraceIndex[hash];
  while (i)
  {
    if (ti[i].retSite == value)
      return &ti[i];
    i = stackTraceChain[i];
  }
  return 0;
}

/* extern void InnerThrow() */
pref pluk_base_Exception__InnerThrow(pref this, frame* stackFrame)
{
  pref result;
  traceinfo* ti;
  result.type = 0;
  result.value = 0;

//  printf("Stack frame\n");
  while (true)
  {
    if (!stackFrame->next)
      break;
    ti = findTraceInfo(stackFrame);
//    if (ti)
//      printf("%p %s.%s %s(%ld)\n", (void*)stackFrame, (char*)ti->class, (char*)ti->field, (char*)ti->file, (long)ti->line);
//    else
//      printf("unknown (%p)\n", stackFrame->value);
    stackFrame = stackFrame->next;
  }
  
  return result;
}

// int()
pref pluk_base_Exception__GetFramePointer(pref this, frame* stackFrame)
{
  pref result;
  result.type = pluk_base_Int;
  result.value = 0;
  if (stackFrame)
    stackFrame = stackFrame->next;
  result.value = (size_t*)stackFrame;
  return result;
}

pref pluk_base_Exception__NextFramePointer(pref this, pref stackFrame)
{
  pref result;
  result.type = pluk_base_Int;
  result.value = (size_t*)((frame*)stackFrame.value)->next;
  return result;
}

pref pluk_base_Exception__ValidFramePointer(pref this, pref stackFrame)
{
  pref result;
  result.type = pluk_base_Bool;
  if ((!stackFrame.value) || (!((frame*)stackFrame.value)->next))
    result.value = (size_t*)false;
  else
    result.value = (size_t*)true;
  return result;
}

// string(int)
pref pluk_base_CallFrame__GetSource(pref this, pref framePointer)
{
  frame* fp = (frame*)longFromPref(framePointer);
  traceinfo* ti;
  ti = findTraceInfo(fp);
  if (!ti)
    return emptyString;
  return strToPref(ti->file);
}

// string(int)
pref pluk_base_CallFrame__GetDefinition(pref this, pref framePointer)
{
  frame* fp = (frame*)longFromPref(framePointer);
  traceinfo* ti;
  ti = findTraceInfo(fp);
  if (!ti)
    return emptyString;
  return strToPref(ti->class);
}

// string(int)
pref pluk_base_CallFrame__GetField(pref this, pref framePointer)
{
  frame* fp = (frame*)longFromPref(framePointer);
  traceinfo* ti;
  ti = findTraceInfo(fp);
  if (!ti)
    return emptyString;
  return strToPref(ti->field);
}

// int(int)
pref pluk_base_CallFrame__GetLine(pref this, pref framePointer)
{
  frame* fp = (frame*)longFromPref(framePointer);
  traceinfo* ti;
  ti = findTraceInfo(fp);
  if (!ti)
    return longToPref(-2);
  return longToPref((long)ti->line);
}
