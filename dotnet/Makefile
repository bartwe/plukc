# on XP
# RCC ?= /cygdrive/c/Program\ Files/Microsoft\ SDKs/Windows/v6.0A/bin/resgen
# on win7 64bit
RCC ?= /cygdrive/c/Program\ Files\ \(x86\)/Microsoft\ SDKs/Windows/v7.0A/Bin/ResGen


MCC ?= /cygdrive/c/Windows/Microsoft.NET/Framework/v3.5/csc

.PHONY: default build install clean

default: build

build:
	$(RCC) Resource.resx Compiler.Resource.resources
	-rm bin/Debug/*.cs
	$(MCC) /debug+ /debug:pdbonly /checked+ /resource:Compiler.Resource.resources /out:pluk.exe /recurse:'*.cs'
	
install: default
	install pluk.exe /usr/local/lib/pluk/pluk.exe
	install pluk.pdb /usr/local/lib/pluk/pluk.pdb
	
clean:
	rm -f pluk.exe* Compiler.Resources.resources
