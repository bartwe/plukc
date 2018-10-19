#include <pluk.h>

size_t* pluk_allocateGC(size_t fieldCount, size_t additionalBytes, size_t* stackTrace);
/* pointer to value/type pair */
void pluk_touchGC(pref* reference);
void pluk_disposeGC(size_t* value, size_t* type);
void pluk_fullSweepGC(size_t* stackTrace);

enum mode { marking, freeing };

size_t* white;
size_t* grey;
size_t* black;
bool direction;
size_t allocationCount, lifeCount, threshold;
enum mode mode;
size_t* fiberStacks;

//set through extern
size_t* gc_globalsBegin;
size_t* gc_globalsEnd;

bool disabled = false;
bool forceFullGcOnAlloc = false;
bool leakFreedMemory = false;

void gc_registerFiber(size_t* fiber)
{
  fiber[2] = (size_t)fiberStacks;
  fiber[3] = 0;
  if (fiberStacks)
    fiberStacks[3] = (size_t)fiber;
  fiberStacks = fiber;
}

void gc_unregisterFiber(size_t* fiber)
{
  if (fiber[2]) // not the last
    ((size_t*)fiber[2])[3] = fiber[3]; // my next.prev = prev
  if (fiber[3]) // not the first
    ((size_t*)fiber[3])[2] = fiber[2]; // my prev.next = next
  else
    fiberStacks = (size_t*)fiber[2];
}

/* objects are 3 words larger -3[FieldCount << 1 | Color] -2 Next -1 Prev */

void touchGC(size_t* data)
{
  if ((data[-3] & 1) == (direction?0:1))
  {
    data[-3] ^= 1;
    if (data[-2])
      ((size_t*)data[-2])[-1] = data[-1];
    if (data[-1])
      ((size_t*)data[-1])[-2] = data[-2];
    else
      if (direction)
        white = (size_t*)data[-2];
      else
        black = (size_t*)data[-2];
    if (grey) 
      grey[-1] = (size_t)data;
    data[-2] = (size_t)grey;
    data[-1] = 0;
    grey = data;
  }
}

/* pointer to value/type pair */
void pluk_touchGC(pref* reference)
{
  if (reference == 0)
    return;
  size_t* t = reference->type;
  if ((t == 0) || (t[0] == 0))
    return;
  size_t* data = reference->value;
  if (data == 0)
    return;
  //inline of touchGC
  if ((data[-3] & 1) == (direction?0:1))
  {
    data[-3] ^= 1;
    if (data[-2])
      ((size_t*)data[-2])[-1] = data[-1];
    if (data[-1])
      ((size_t*)data[-1])[-2] = data[-2];
    else
      if (direction)
        white = (size_t*)data[-2];
      else
        black = (size_t*)data[-2];
    if (grey) 
      grey[-1] = (size_t)data;
    data[-2] = (size_t)grey;
    data[-1] = 0;
    grey = data;
  }
}

void removeAndFree(size_t* data)
{
  if (black == data)
    black = (size_t*)data[-2];
  if (white == data)
    white = (size_t*)data[-2];
  if (grey == data)
    grey = (size_t*)data[-2];
  if (data[-2])
    ((size_t*)data[-2])[-1] = data[-1];
  if (data[-1])
    ((size_t*)data[-1])[-2] = data[-2];
  if (!leakFreedMemory)
    free(&data[-3]);
  lifeCount--;
}

void pluk_disposeGC(size_t* value, size_t* valueType)
{
  if (value == 0)
    return;
  if (valueType == 0)
    return;
  if (valueType[0] == 0)
    return;
  removeAndFree(value);
}

void makeAlive(size_t* data)
{
  size_t fieldCount = data[-3] >> 1;
  if (direction)
  {
    data[-3] = (fieldCount << 1) | 1;
    if (black)
      black[-1] = (size_t)data;
    data[-2] = (size_t)black;
    data[-1] = 0;
    black = data;
    if (white == data)
      white = 0;
  }  
  else
  {
    data[-3] = fieldCount << 1;
    if (white)
      white[-1] = (size_t)data;
    data[-2] = (size_t)white;
    data[-1] = 0;
    white = data;
    if (black == data)
      black = 0;    
  }
  while (fieldCount--)
  {
    pluk_touchGC((pref*)&data[fieldCount << 1]);
  }
}

void markLocalStack(size_t* stackTrace)
{
  while (stackTrace)
  {
    pref* cursor;
    cursor = (pref*)&stackTrace[2];
    stackTrace = (size_t*)stackTrace[0];
    while (true)
    {
      if (stackTrace == 0)
        return;
      if (stackTrace[1])
        break;
      stackTrace = (size_t*)stackTrace[0];
    }
    while (((size_t)cursor) < ((size_t)stackTrace))
    {
      pluk_touchGC(cursor);
      cursor = &cursor[1];
    }
  }
}

// if scanall is false only the current stack is scanned unless that results in no new elements on the gray list
// otherwise the fiberstacks are also scanned
void markStack(size_t* stackTrace, bool scanAll)
{
  markLocalStack(stackTrace);
  if (scanAll || (grey == 0))
  {
    size_t* fs = fiberStacks;
    while (fs)
    {
      size_t* ebp = (size_t*)fs[1];
      markLocalStack(ebp);
      fs = (size_t*)fs[2];
    }
    
    pref* cursor = (pref*)gc_globalsBegin;
    while (cursor < (pref*)gc_globalsEnd)
    {
      pluk_touchGC(cursor);
      cursor = &cursor[1];
    }
  }
}

void pluk_fullSweepGC(size_t* stackTrace)
{
  if (disabled)
    return;
  mode = marking;
  markStack(stackTrace, true);
  while (grey)
  {
    size_t* cursor = grey;
    grey = (size_t*)grey[-2];
    if (grey)
      grey[-1] = 0;
    makeAlive(cursor);
  }
  if (direction)
  {
    while (white)
    {
      size_t* cursor = white;
      white = (size_t*)white[-2];
      if (white)
        white[-1] = 0;
      if (!leakFreedMemory)
        free(&(cursor[-3]));
      lifeCount--;
    }
  }
  else
  {
    while (black)
    {
      size_t* cursor = black;
      black = (size_t*)black[-2];
      if (black)
        black[-1] = 0;
      if (!leakFreedMemory)
        free(&(cursor[-3]));
      lifeCount--;
    }
  }
  direction = !direction;
}

void process(size_t* stackTrace)
{
  if (disabled)
    return;
  if (mode == marking)
  {
    if (grey)
    {
      size_t* cursor = grey;
      grey = (size_t*)grey[-2];
      if (grey)
        grey[-1] = 0;
      makeAlive(cursor);
    }
    else
    {
      if (stackTrace)
      {
        markStack(stackTrace, false);
        if (grey == 0)
          mode = freeing;
      }
    }
  }
  else
  {
    if (direction)
    {
      if (white)
      {
        size_t* cursor = white;
        white = (size_t*)white[-2];
        if (white)
          white[-1] = 0;
        if (!leakFreedMemory)
          free(&(cursor[-3]));
        lifeCount--;
      }
      else
      {
        direction = ! direction;
        mode = marking;
      }
    }
    else
    {
      if (black)
      {
        size_t* cursor = black;
        black = (size_t*)black[-2];
        if (black)
          black[-1] = 0;
        if (!leakFreedMemory)
          free(&(cursor[-3]));
        lifeCount--;
      }
      else
      {
        direction = ! direction;
        mode = marking;
      }
    }
  }
}

void fullProcess()
{
  if (disabled)
    return;
  while(true)
  {
    if (mode == marking)
    {
      if (grey)
      {
        size_t* cursor = grey;
        grey = (size_t*)grey[-2];
        if (grey)
          grey[-1] = 0;
        makeAlive(cursor);
      }
      else
      {
        return;
      }
    }
    else
    {
      if (direction)
      {
        if (white)
        {
          size_t* cursor = white;
          white = (size_t*)white[-2];
          if (white)
            white[-1] = 0;
          if (!leakFreedMemory)
            free(&(cursor[-3]));
          lifeCount--;
        }
        else
        {
          direction = ! direction;
          mode = marking;
        }
      }
      else
      {
        if (black)
        {
          size_t* cursor = black;
          black = (size_t*)black[-2];
          if (black)
            black[-1] = 0;
          if (!leakFreedMemory)
            free(&(cursor[-3]));
          lifeCount--;
        }
        else
        {
          direction = ! direction;
          mode = marking;
        }
      }
    }
  }
}

size_t* pluk_allocateGC(size_t fieldCount, size_t additionalBytes, size_t* stackTrace)
{
  size_t c;
  size_t* result;
  if (!disabled)
  {
    if (stackTrace && ((lifeCount > threshold) || forceFullGcOnAlloc))
    {
      pluk_fullSweepGC(stackTrace);
      threshold = lifeCount * 2 + 1024;
    }
    else
      process(stackTrace);
  }
  c = (fieldCount * 2 + 3) * (size_t)sizeof(size_t) + additionalBytes;
  c = ((c+(size_t)sizeof(size_t)-1) >> ((sizeof(size_t)==8)?3:2)) << ((sizeof(size_t)==8)?3:2);
  result = calloc(c, 1);
  if (!result)
  {
    if (stackTrace)
    {
      pluk_fullSweepGC(stackTrace);
      threshold = lifeCount * 2 + 1024;
    }
    else
      fullProcess();
    result = calloc(c, 1);
    if (!result)
    {
      if (stackTrace)
      {
        pluk_fullSweepGC(stackTrace);
        threshold = lifeCount * 2 + 1024;
      }
      else
        fullProcess();
      result = calloc(c, 1);
      if (!result)
        abort();
    }
  }
  lifeCount++;
  allocationCount++;  
  result = &(result[3]);
  if (direction)
  {
    result[-3] = (fieldCount << 1) + 1;
    if (black)
      black[-1] = (size_t)result;
    result[-2] = (size_t)black;
    result[-1] = 0;
    black = result;
  }
  else
  {
    result[-3] = fieldCount << 1;
    if (white)
      white[-1] = (size_t)result;
    result[-2] = (size_t)white;
    result[-1] = 0;
    white = result;
  }
  return result;
}
