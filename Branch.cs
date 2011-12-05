using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace GitStore
{
    internal interface IBranchRef
    {
        Repository Repository { get; }
        string Name { get; }
        string Id { get; }
    }

    internal class BranchFile : ObjectRefFile, IBranchRef
    {
        internal BranchFile(Repository aRepository, FileInfo aFileInfo)
            : base(aRepository, aFileInfo)
        {
        }
    }

    internal class BranchPacked : IBranchRef
    {
        internal BranchPacked(Repository aRepository, string aName, string aId)
        {
            iRepository = aRepository;
            iName = aName;
            iId = aId;
        }

        public Repository Repository
        {
            get
            {
                return (iRepository);
            }
        }

        public string Name
        {
            get
            {
                return (iName);
            }
        }

        public string Id
        {
            get
            {
                return (iId);
            }
        }

        private Repository iRepository;
        private string iName;
        private string iId;
    }

    internal class BranchEmpty : BranchPacked
    {
        internal BranchEmpty(Repository aRepository, string aName)
            : base(aRepository, aName, null)
        {
        }
    }

    internal class Branch : IBranch
    {
        internal Branch(IBranchRef aReference)
        {
            iReference = aReference;
            iResolved = false;
        }

        public string Name
        {
            get
            {
                return (iReference.Name);
            }
        }

        public ICommit Commit
        {
            get
            {
                Resolve();
                return (iCommit);
            }
        }

        public IChange CreateChange()
        {
            return (new Change(iReference.Repository, this));
        }

        public bool IsEmpty
        {
            get
            {
                Resolve();
                return (iCommit == null);
            }
        }

        internal void Update(IBranchRef aReference)
        {
            iReference = aReference;
            iResolved = false;
        }

        private void Resolve()
        {
            if (!iResolved)
            {
                string id = iReference.Id;

                if (id != null)
                {
                    iCommit = new Commit(new CommitRef(iReference.Repository, id));
                }

                iResolved = true;
            }
        }

        private IBranchRef iReference;
        private bool iResolved;
        private ICommit iCommit;
    }
}
