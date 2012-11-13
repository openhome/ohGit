using System;
using System.Collections.Generic;

namespace OpenHome.Git
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
        //IChange CreateChange();
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
        void Delete();
        bool Fetch();
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

    public static class GitFactory
    {
        public static IRepository Open(string aPath, string aUri, string aBranch)
        {
            return (new Repository(aPath, aUri, aBranch));
        }

        public static IRepository Open(string aPath, string aUri)
        {
            return (Open(aPath, aUri, "master"));
        }

        /*
        public static IPerson CreatePerson(string aName, string aEmail)
        {
            return (Person.Create(aName, aEmail));
        }
        */
    }
}
