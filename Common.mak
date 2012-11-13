all : $(objdir)/ohGit.dll

srcfolder = src$(dirsep)ohGit$(dirsep)OpenHome$(dirsep)Git

source = \
$(srcfolder)$(dirsep)Blob.cs \
$(srcfolder)$(dirsep)Branch.cs \
$(srcfolder)$(dirsep)Change.cs \
$(srcfolder)$(dirsep)Commit.cs \
$(srcfolder)$(dirsep)Fetcher.cs \
$(srcfolder)$(dirsep)Git.cs \
$(srcfolder)$(dirsep)Hash.cs \
$(srcfolder)$(dirsep)Object.cs \
$(srcfolder)$(dirsep)Pack.cs \
$(srcfolder)$(dirsep)Person.cs \
$(srcfolder)$(dirsep)Repository.cs \
$(srcfolder)$(dirsep)Tag.cs \
$(srcfolder)$(dirsep)Tree.cs
  
$(objdir)/ohGit.dll : $(objdir) $(source)
	$(csharp) /unsafe /t:library \
		/out:$(objdir)/ohGit.dll \
		/reference:System.dll \
		/reference:$(objdir)/ICSharpCode.SharpZipLib.dll \
		$(source)

