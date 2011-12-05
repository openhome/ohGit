using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenHome.Git
{
    class BlobModifiable
    {
        internal BlobModifiable(Repository aRepository, byte[] aContents)
        {
            iRepository = aRepository;
            iContents = aContents;
        }

        internal IBlob Write()
        {
            return (Blob.Write(iRepository, iContents));
        }

        private Repository iRepository;
        private byte[] iContents;
    }

    class BlobModifiableNew : BlobModifiable
    {
        internal BlobModifiableNew(Repository aRepository, byte[] aContents, string aName, string aMode)
            : base(aRepository, aContents)
        {
            iName = aName;
            iMode = aMode;
        }

        internal string Name
        {
            get
            {
                return (iName);
            }
        }

        internal string Mode
        {
            get
            {
                return (iMode);
            }
        }

        private string iName;
        private string iMode;
    }

    internal class TreeModifiable : ITreeModifiable
    {
        internal TreeModifiable(Repository aRepository, ITree aTree)
        {
            iRepository = aRepository;
            iTree = aTree;
            iModified = false;
            iTreeAddList = new Dictionary<string, TreeModifiableNew>();
            iTreeModifyList = new Dictionary<string, TreeModifiable>();
            iBlobAddList = new Dictionary<string, BlobModifiableNew>();
            iBlobModifyList = new Dictionary<string, BlobModifiable>();
            iModeChangeList = new Dictionary<string, string>();
            iDeleteList = new List<string>();
            iWritten = false;
        }

        public ITreeModifiable AddTree(string aName, string aMode)
        {
            CheckWritten();

            CheckAdd(aName);

            iModified = true;

            TreeModifiableNew addition = new TreeModifiableNew(iRepository, aName, aMode);

            iTreeAddList.Add(aName, addition);

            return (addition);
        }

        public ITreeModifiable ModifyTree(string aName)
        {
            CheckWritten();

            CheckDelete(aName);

            if (iTreeModifyList.ContainsKey(aName))
            {
                throw (new GitStoreError(aName + " already staged for modification"));
            }

            ITreeEntry item = Find(aName);

            if (item != null)
            {
                ITree tree = item.Item as ITree;

                if (tree != null)
                {
                    TreeModifiable modifiable = new TreeModifiable(iRepository, tree);
                    iTreeModifyList.Add(aName, modifiable);
                    return (modifiable);
                }
            }

            throw (new GitStoreError(aName + " not found"));
        }

        public void AddBlob(byte[] aContents, string aName, string aMode)
        {
            CheckWritten();

            CheckAdd(aName);

            iModified = true;

            BlobModifiableNew addition = new BlobModifiableNew(iRepository, aContents, aName, aMode);

            iBlobAddList.Add(aName, addition);
        }

        public void ModifyBlob(byte[] aContents, string aName)
        {
            CheckWritten();

            CheckDelete(aName);

            if (iBlobModifyList.ContainsKey(aName))
            {
                throw (new GitStoreError(aName + " already staged for modification"));
            }

            ITreeEntry item = Find(aName);

            if (item != null)
            {
                IBlob blob = item.Item as IBlob;

                if (blob != null)
                {
                    BlobModifiable modifiable = new BlobModifiable(iRepository, aContents);
                    iBlobModifyList.Add(aName, modifiable);
                    iModified = true;
                    return;
                }
            }

            throw (new GitStoreError(aName + " not found"));
        }

        public void ChangeMode(string aName, string aMode)
        {
            CheckWritten();

            CheckDelete(aName);

            if (iModeChangeList.ContainsKey(aName))
            {
                throw (new GitStoreError(aName + " already staged for mode change"));
            }

            ITreeEntry item = Find(aName);

            if (Find(aName) == null)
            {
                throw (new GitStoreError(aName + " not found"));
            }

            iModified = true;

            iModeChangeList.Add(aName, aMode);
        }

        public void Delete(string aName)
        {
            CheckWritten();

            CheckDelete(aName);

            if (iModeChangeList.ContainsKey(aName))
            {
                throw (new GitStoreError(aName + " already staged for mode change"));
            }

            ITreeEntry item = Find(aName);

            if (Find(aName) == null)
            {
                throw (new GitStoreError(aName + " not found"));
            }

            iModified = true;

            iDeleteList.Add(aName);
        }

        internal bool Modified
        {
            get
            {
                return (iModified);
            }
        }

        private void CheckAdd(string aName)
        {
            if (iTreeAddList.ContainsKey(aName) || iBlobAddList.ContainsKey(aName))
            {
                throw (new GitStoreError(aName + " already staged for addition"));
            }

            if (Find(aName) != null)
            {
                throw (new GitStoreError(aName + " already exists"));
            }
        }

        private void CheckDelete(string aName)
        {
            if (iDeleteList.Contains(aName))
            {
                throw (new GitStoreError(aName + " already staged for deletion"));
            }
        }

        protected virtual ITreeEntry Find(string aName)
        {
            if (iTree != null)
            {
                foreach (ITreeEntry item in iTree.Items)
                {
                    if (item.Name == aName)
                    {
                        return (item);
                    }
                }
            }

            return (null);
        }

        internal ITree Write()
        {
            CheckWritten();

            iWritten = true;

            List<TreeItem> list = new List<TreeItem>();

            if (iTree != null)
            {
                foreach (ITreeEntry item in iTree.Items)
                {
                    string id = item.Id;
                    string mode = item.Mode;
                    string name = item.Name;

                    if (iDeleteList.Contains(name))
                    {
                        continue;
                    }

                    if (iModeChangeList.ContainsKey(name))
                    {
                        mode = iModeChangeList[name];
                    }

                    if (iTreeModifyList.ContainsKey(name))
                    {
                        TreeModifiable modified = iTreeModifyList[name];

                        ITree tree = modified.Write();

                        if (modified.Modified)
                        {
                            id = tree.Id;
                            iModified = true;
                        }
                    }
                    else if (iBlobModifyList.ContainsKey(name))
                    {
                        IBlob blob = iBlobModifyList[name].Write();
                        id = blob.Id;
                    }

                    list.Add(new TreeItem(iRepository, id, mode, name));
                }
            }

            foreach (TreeModifiableNew item in iTreeAddList.Values)
            {
                ITree tree = item.Write();
                list.Add(new TreeItem(iRepository, tree.Id, item.Mode, item.Name));
            }

            foreach (BlobModifiableNew item in iBlobAddList.Values)
            {
                IBlob blob = item.Write();
                list.Add(new TreeItem(iRepository, blob.Id, item.Mode, item.Name));
            }

            if (iModified | iTree == null)
            {
                iTree = Tree.Write(iRepository, list);
            }

            return (iTree);
        }

        private void CheckWritten()
        {
            if (iWritten)
            {
                throw (new GitStoreError("Already written"));
            }
        }

        private Repository iRepository;
        private ITree iTree;

        private bool iModified;

        private Dictionary<string, TreeModifiableNew> iTreeAddList;
        private Dictionary<string, TreeModifiable> iTreeModifyList;
        private Dictionary<string, BlobModifiableNew> iBlobAddList;
        private Dictionary<string, BlobModifiable> iBlobModifyList;
        private Dictionary<string, string> iModeChangeList;
        private List<string> iDeleteList;
        private bool iWritten;
    }

    class TreeModifiableNew : TreeModifiable
    {
        internal TreeModifiableNew(Repository aRepository, string aName, string aMode)
            : base(aRepository, null)
        {
            iName = aName;
            iMode = aMode;
        }

        internal string Name
        {
            get
            {
                return (iName);
            }
        }

        internal string Mode
        {
            get
            {
                return (iMode);
            }
        }

        protected override ITreeEntry Find(string aName)
        {
            return (null);
        }

        private string iName;
        private string iMode;
    }

    class Change : IChange
    {
        internal Change(Repository aRepository, Branch aBranch)
        {
            iRepository = aRepository;
            iBranch = aBranch;
            iParents = new List<IBranch>();
            iParents.Add(iBranch);
            iWritten = false;

            if (iBranch.IsEmpty)
            {
                iRoot = new TreeModifiable(iRepository, iBranch.Commit.Tree);
            }
            else
            {
                iRoot = new TreeModifiable(iRepository, null);
            }
        }

        public IBranch Branch
        {
            get
            {
                CheckWritten();

                return (iBranch);
            }
        }

        public ITreeModifiable Root
        {
            get
            {
                CheckWritten();

                return (iRoot);
            }
        }

        public void AddParent(IBranch aBranch)
        {
            CheckWritten();

            if (iParents.Contains(aBranch))
            {
                throw (new GitStoreError("Branch already added"));
            }

            iParents.Add(aBranch);
        }

        public ICommit Write(IPerson aAuthor, IPerson aCommitter, string aDescription)
        {
            CheckWritten();

            iWritten = true;

            ITree tree = iRoot.Write();

            if (!iRoot.Modified)
            {
                throw (new GitStoreError("No modifiction"));
            }

            // TODO clean this up

            ICommit commit = Commit.Write(iRepository, tree, new List<ICommit>(), aAuthor, aCommitter, aDescription);

            iRepository.UpdateBranch(iBranch, commit.Id);

            return (commit);
        }

        private void CheckWritten()
        {
            if (iWritten)
            {
                throw (new GitStoreError("Already written"));
            }
        }

        private Repository iRepository;
        private Branch iBranch;
        private TreeModifiable iRoot;
        private List<IBranch> iParents;
        bool iWritten;
    }
}
