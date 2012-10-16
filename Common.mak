all : $(objdir)/ohGit.dll

source = \
Blob.cs \
Branch.cs \
Change.cs \
Commit.cs \
Factory.cs \
Fetcher.cs \
Hash.cs \
Object.cs \
Pack.cs \
Person.cs \
Repository.cs \
Tag.cs \
Tree.cs
  
$(objdir)/ohGit.dll : $(objdir) $(source)
	$(csharp) /unsafe /t:library \
		/out:$(objdir)/ohGit.dll \
		/reference:System.dll \
		/reference:$(objdir)/ICSharpCode.SharpZipLib.dll \
		$(source)

