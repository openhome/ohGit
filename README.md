# ohGit

ohGit is a library for fetching a git using the git protocol and reading its contents.

It is written in C#, and is compatible with both .Net and Mono.

ohGit depends on SharpZipLib from ic#code, which is included as a library.

# Getting Started

The starting point is the Factory class.

    public class Factory
    {
        public static IRepository InitialiseRepository(string aPath, string aBranch)
        {
            return (Repository.Initialise(aPath, aBranch));
        }

        public static IRepository OpenRepository(string aPath)
        {
            return (new Repository(aPath));
        }

        public static bool Fetch(IRepository aRepository, string aUri)
        {
            return (Fetcher.Fetch(aRepository, aUri));
        }
    }

To open an existing repository, use OpenRepositiory(), providing the path to the folder that contains the .git folder. 

To initialise a new repository, use InitialiseRepository(), providing a path and the name of the HEAD branch. You may then use Fetch() to collect its contents using the git protocol.

Either way, you now have an IRepository:

    public interface IRepository
    {
        IDictionary<string, IBranchRef> Branches { get; }
        IDictionary<string, IRef> Refs { get; }
    }

# Reading

It is necessary to have a basic understanding of the low-level structure of a git to understand the folowing collection of interfaces. They expose this structure in a way that facilitates complete traversal of all branches and their parents.

IRepository has an IDictionary for getting an IBranch ...

    public interface IBranch
    {
        string Name { get; }
        ICommit Commit { get; }
        IChange CreateChange();
        bool IsEmpty { get; }
    }

... from which its ICommit can be read ...

    public interface ICommit : IObject
    {
        ITree Tree { get; }
        ICollection<ICommit> Parents { get; }
        IPersonTime Author { get; }
        IPersonTime Committer { get; }
        string Description { get; }
    }

... from which its ITree can be read ...

    public interface ITree : IObject
    {
        ICollection<ITreeEntry> Items { get; }
    }

... from which its ITreeEntry items can be read ...

    public interface ITreeEntry : IObject
    {
        string Name { get; }
        string Mode { get; }
        IObject Item { get; }
    }

Using the Item member an IObject can be retrieved, which may be a
further ITree, or an IBlob.

    public interface IBlob : IObject
    {
        byte[] Contents { get; }
    }

# Usage

This library is currently used by ohMediaToolkit to collect a remotely updated description of its media tree. It is envisaged that the library could be very useful for connecting a software developer to all installations of their app, allowing them to distribute richer content than they could with, for instance, an RSS feed.

i.e. plugins, blacklists/whitelists, examples, seasonal skins, etc