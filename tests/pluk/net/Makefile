
.PHONY: default rebase clean

default:
	@for d in * ; do if [ -d $$d ] ; then ./runtest $$d; fi; done
	
rebase:
	@for d in * ; do if [ -d $$d ] ; then ./rebasetest $$d; fi; done

clean:
	@for d in * ; do if [ -d $$d ] ; then ./cleantest $$d; fi; done
