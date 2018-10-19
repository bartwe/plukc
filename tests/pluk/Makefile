.PHONY: default clean

default:
	@for d in * ; do if [ -d $$d ] ; then cd $$d && make default && cd ..; fi; done
