using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenHome.Git
{
    internal interface ICommitRef
    {
        Repository Repository { get; }
        string Id { get; }
        byte[] Contents { get; }
    }


    internal class CommitRef : ICommitRef
    {
        internal CommitRef(Repository aRepository, string aId, byte[] aContents)
        {
            iRepository = aRepository;
            iId = aId;
        }

        internal CommitRef(Repository aRepository, string aId)
            : this(aRepository, aId, null)
        {
        }

        public string Id
        {
            get
            {
                return (iId);
            }
        }

        public Repository Repository
        {
            get
            {
                return (iRepository);
            }
        }

        public byte[] Contents
        {
            get
            {
                if (iContents == null)
                {
                    Object obj = iRepository.GetObject(iId);
                    CorruptIf(obj.Type != EObjectType.Commit);
                    iContents = obj.Contents;
                }

                return (iContents);
            }
        }

        internal void CorruptIf(bool aValue)
        {
            if (aValue)
            {
                throw (new GitCorruptCommitException(iId));
            }
        }

        private Repository iRepository;
        private string iId;
        private byte[] iContents;
    }

    internal class Commit : ICommit
    {
        internal Commit(ICommitRef aReference)
        {
            iReference = aReference;
        }

        internal void Resolve()
        {
            if (iTree == null)
            {
                iParents = new List<ICommit>();

                string commit = ASCIIEncoding.ASCII.GetString(iReference.Contents);

                string[] lines = commit.Split(new char[] { '\n' });

                int offset = 1;

                foreach (string line in lines)
                {
                    if (line.Length == 0)
                    {
                        break;
                    }

                    offset += line.Length + 1;

                    string[] parts = line.Split(new char[] { ' ' }, 2);

                    CorruptIf(parts.Length != 2);

                    string key = parts[0];
                    string value = parts[1];

                    switch (key)
                    {
                        case "tree":
                            CorruptIf(iTree != null);
                            iTree = new Tree(new TreeRef(iReference.Repository, value));
                            break;

                        case "parent":
                            iParents.Add(new Commit(new CommitRef(iReference.Repository, value)));
                            break;

                        case "author":
                            CorruptIf(iAuthor != null);
                            iAuthor = PersonTime.Create(value);
                            break;

                        case "committer":
                            CorruptIf(iCommitter != null);
                            iCommitter = PersonTime.Create(value);
                            break;

                        default:
                            CorruptIf(true);
                            break;
                    }
                }

                CorruptIf(iTree == null);
                CorruptIf(iAuthor == null);
                CorruptIf(iCommitter == null);

                iDescription = commit.Substring(offset);
            }
        }

        internal static ICommit Write(Repository aRepository, ITree aTree, IEnumerable<ICommit> aParents, IPerson aAuthor, IPerson aCommitter, string aDescription)
        {
            StringBuilder builder = new StringBuilder();

            builder.Append("tree " + aTree.Id + "\n");

            foreach (ICommit parent in aParents)
            {
                builder.Append("parent " + parent.Id + "\n");
            }

            builder.Append("author " + PersonTime.String(aAuthor) + "\n");
            builder.Append("committer " + PersonTime.String(aCommitter) + "\n");
            builder.Append("\n");
            builder.Append(aDescription);

            byte[] bytes = ASCIIEncoding.ASCII.GetBytes(builder.ToString());

            string id = aRepository.WriteObject(bytes, EObjectType.Commit);

            // TODO

            return (new Commit(new CommitRef(aRepository, id, bytes)));
        }

        private void CorruptIf(bool aValue)
        {
            if (aValue)
            {
                throw (new GitCorruptCommitException(iReference.Id));
            }
        }

        public IRepository Repository
        {
            get
            {
                return (iReference.Repository);
            }
        }

        public string Id
        {
            get
            {
                return (iReference.Id);
            }
        }

        public ITree Tree
        {
            get
            {
                Resolve();
                return (iTree);
            }
        }

        public ICollection<ICommit> Parents
        {
            get
            {
                Resolve();
                return (iParents);
            }
        }

        public IPersonTime Author
        {
            get
            {
                Resolve();
                return (iAuthor);
            }
        }

        public IPersonTime Committer
        {
            get
            {
                Resolve();
                return (iCommitter);
            }
        }

        public string Description
        {
            get
            {
                Resolve();
                return (iDescription);
            }
        }

        public void Resolve(IObjectResolver aHandler)
        {
            aHandler.Resolve(this);
        }

        private ICommitRef iReference;

        private ITree iTree;
        private List<ICommit> iParents;
        private IPersonTime iAuthor;
        private IPersonTime iCommitter;
        private string iDescription;
    }
}
