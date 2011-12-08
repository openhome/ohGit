all : $(objdir)$(dllprefix)ohGit.$(dllext)

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
  
$(objdir)$(dllprefix)ohGit.$(dllext) : $(objdir) $(source)
	$(csharp) /unsafe /t:library \
		/out:$(objdir)/$(dllprefix)ohGit.$(dllext) \
		/reference:System.dll \
		/reference:SharpZipLib/ICSharpCode.SharpZipLib.dll \
		$(source)

