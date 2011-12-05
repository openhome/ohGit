using System;
using System.Collections.Generic;

namespace GitStore
{
    public interface IPerson
    {
        string Name { get; }
        string Email { get; }
    }

    public interface IPersonTime : IPerson
    {
        DateTime Time { get; }
    }

    public interface IBranch
    {
        string Name { get; }
        ICommit Commit { get; }
        IChange CreateChange();
        bool IsEmpty { get; }
    }

    public interface IRef
    {
        string Name { get; }
        IObject Item { get; }
    }

    public interface IRepository
    {
        string Head { get; }
        IDictionary<string, IBranch> Branches { get; }
        IDictionary<string, IRef> Refs { get; }
    }

    public interface IObjectResolver
    {
        void Resolve(IBlob aBlob);
        void Resolve(ITree aTree);
        void Resolve(ICommit aCommit);
        void Resolve(ITag aTag);
    }

    public interface IObjectRef
    {
        string Id { get; }
        IRepository Repository { get; }
    }

    public interface IObject : IObjectRef
    {
        void Resolve(IObjectResolver aResolver);
    }

    public interface ITreeEntry : IObjectRef
    {
        string Name { get; }
        string Mode { get; }
        IObject Item { get; }
    }

    public interface IBlob : IObject
    {
        byte[] Contents { get; }
    }

    public interface ITree : IObject
    {
        ICollection<ITreeEntry> Items { get; }
    }

    public interface ICommit : IObject
    {
        ITree Tree { get; }
        ICollection<ICommit> Parents { get; }
        IPersonTime Author { get; }
        IPersonTime Committer { get; }
        string Description { get; }
    }

    public interface ITag : IObject
    {
        string Type { get; }
        IPersonTime Tagger { get; }
        string Description { get; }
        IObject Item { get; }
    }

    public interface ITreeModifiable
    {
        ITreeModifiable AddTree(string aName, string aMode);
        ITreeModifiable ModifyTree(string aName);
        void AddBlob(byte[] aContents, string aName, string aMode);
        void ModifyBlob(byte[] aContents, string aName);
        void ChangeMode(string aName, string aMode);
        void Delete(string aName);
    }

    public interface IChange
    {
        IBranch Branch { get; }
        ITreeModifiable Root { get; }
        void AddParent(IBranch aBranch);
        ICommit Write(IPerson aAuthor, IPerson aCommitter, string aDescription);
    }

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

        public static IPerson CreatePerson(string aName, string aEmail)
        {
            return (Person.Create(aName, aEmail));
        }
    }
}
