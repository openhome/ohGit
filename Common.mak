all : $(objdir)/ohGit.net.dll

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
  
$(objdir)/ohGit.net.dll : $(objdir) $(source)
	$(csharp) /unsafe /t:library \
		/out:$(objdir)/ohGit.net.dll \
		/reference:System.dll \
		/reference:SharpZipLib/ICSharpCode.SharpZipLib.dll \
		$(source)

