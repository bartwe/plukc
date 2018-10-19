.PHONY: default build test install clean

default: build

build:
	@cd lib && make build
	@cd dotnet && make build
	
install:
	@cd lib && make install
	@cd dotnet && make install
	@cd src && make install
	install scripts/pluki /usr/local/bin/pluki
	install scripts/pluk /usr/local/bin/pluk

clean:
	@cd lib && make clean
	@cd dotnet && make clean
	@cd tests && make clean
	
test:
	@cd tests && make default
