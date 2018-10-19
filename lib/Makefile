CFLAGS += -O0 -g -mno-cygwin -mwindows -mwin32 -shared -Dpwin32

.PHONY: default clean build install

CWARNFLAGS = -Wall -pedantic -ansi -std=c99 -Wextra -Wunused -Wfloat-equal -Wpointer-arith -Wcast-align -Wunreachable-code -Werror -Wno-unused-parameter
.SUFFIXES:
.SUFFIXES: .c .o

%.o : %.c
	$(CC) -c $(CFLAGS) $(CWARNFLAGS) -I./pe -I. $^ -o $@

%.o : %.s
	$(CC) -c $(CFLAGS) $(CWARNFLAGS) -I./pe -I. $^ -o $@

%.o : %.S
	$(CC) -c $(CFLAGS) $(CWARNFLAGS) -I./pe -I. $^ -o $@

default: build

clean:
	rm -f *.o *.dll *.lo *.la *~ pe/*.o net/*.o winsock/*.o

build: pluk_base.dll pluk_io.dll pluk_net.dll

pluk_base.dll: $(patsubst %.c,%.o,$(wildcard *.c)) $(patsubst %.c,%.o,$(wildcard base/*.c)) $(patsubst %.c,%.o,$(wildcard pe/*.c)) $(patsubst %.s,%.o,$(wildcard i686/*.s)) $(patsubst %.S,%.o,$(wildcard i686/*.S))
	$(CC) $(CFLAGS) -Wl,-soname,pluk_base.dll -o $@ $^ 

pluk_io.dll: pluk_base.dll $(patsubst %.c,%.o,$(wildcard io/*.c))
	$(CC) $(CFLAGS) -Wl,-soname,pluk_io.dll -o $@ $^ 

pluk_net.dll: pluk_base.dll $(patsubst %.c,%.o,$(wildcard winsock/*.c))
	$(CC) $(CFLAGS) -Wl,-soname,pluk_net.dll -o $@ $^ -lwsock32 -lws2_32

install: default
	mkdir -p /usr/local/lib/pluk/lib
	install -t /usr/local/lib/pluk/lib pluk_base.dll
	install -t /usr/local/lib/pluk/lib pluk_io.dll
	install -t /usr/local/lib/pluk/lib pluk_net.dll
	echo "Don't forget to put $(shell cygpath -wa /usr/local/lib/pluk/lib) into the windows path."
