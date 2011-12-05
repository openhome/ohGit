using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GitStore
{
    internal class TreeItem : ITreeEntry
    {
        internal TreeItem(Repository aRepository, string aId, string aMode, string aName)
        {
            iRepository = aRepository;
            iId = aId;
            iMode = aMode;
            iName = aName;
        }

        internal TreeItem(Repository aRepository, string aId, byte[] aBytes)
        {
            iRepository = aRepository;
            iId = aId;

            string value = ASCIIEncoding.ASCII.GetString(aBytes);

            string[] parts = value.Split(new char[] { ' ' }, 2);

            if (parts.Length != 2)
            {
                throw (new GitStoreError("Tree item corrupt"));
            }

            iMode = parts[0];
            iName = parts[1];
        }

        internal byte[] Bytes()
        {
            byte[] mode = ASCIIEncoding.ASCII.GetBytes(iMode);
            byte[] name = ASCIIEncoding.ASCII.GetBytes(iName);
            byte[] sha1 = Hash.Bytes(iId);

            byte[] bytes = new byte[mode.Length + name.Length + sha1.Length + 2];

            Array.Copy(mode, 0, bytes, 0, mode.Length);
            bytes[mode.Length] = 0x20;
            Array.Copy(name, 0, bytes, mode.Length + 1, name.Length);
            bytes[mode.Length + name.Length + 1] = 0x00;
            Array.Copy(sha1, 0, bytes, mode.Length + name.Length + 2, sha1.Length);

            return (bytes);
        }

        public IRepository Repository
        {
            get
            {
                return (iRepository);
            }
        }

        public string Id
        {
            get
            {
                return (iId);
            }
        }

        public string Mode
        {
            get
            {
                return (iMode);
            }
        }

        public string Name
        {
            get
            {
                return (iName);
            }
        }

        public IObject Item
        {
            get
            {
                if (iObject == null)
                {
                    Object obj = iRepository.GetObject(iId);
                    iObject = obj.Create(iRepository, iId);
                }

                return (iObject);
            }
        }

        private Repository iRepository;
        private string iId;
        private string iMode;
        private string iName;
        private IObject iObject;
    }

    internal interface ITreeRef
    {
        Repository Repository { get; }
        string Id { get; }
        byte[] Contents { get; }
    }

    internal class TreeRef : ITreeRef
    {
        internal TreeRef(Repository aRepository, string aId, byte[] aContents)
        {
            iRepository = aRepository;
            iId = aId;
            iContents = aContents;
        }

        internal TreeRef(Repository aRepository, string aId)
            : this(aRepository, aId, null)
        {
        }

        public Repository Repository
        {
            get
            {
                return (iRepository);
            }
        }

        public string Id
        {
            get
            {
                return (iId);
            }
        }

        public byte[] Contents
        {
            get
            {
                if (iContents == null)
                {
                    Object obj = iRepository.GetObject(iId);

                    if (obj.Type != EObjectType.Tree)
                    {
                        throw (new GitStoreError("Tree " + Id + " corrupt"));
                    }

                    iContents = obj.Contents;
                }

                return (iContents);
            }
        }

        private Repository iRepository;
        private string iId;
        private byte[] iContents;
    }

    internal class Tree : ITree
    {
        internal Tree(ITreeRef aReference)
        {
            iReference = aReference;
        }

        void Resolve()
        {
            if (iItems == null)
            {
                iItems = new List<ITreeEntry>();

                int offset = 0;

                byte[] bytes = iReference.Contents;

                try
                {
                    while (offset < bytes.Length)
                    {
                        int begin = offset;

                        while (bytes[offset++] != 0) ;

                        int length = offset - begin - 1;

                        byte[] item = new byte[length];

                        Array.Copy(bytes, begin, item, 0, length);

                        string id = Hash.String(bytes, offset);

                        iItems.Add(new TreeItem(iReference.Repository, id, item));

                        offset += 20;
                    }
                }
                catch (IndexOutOfRangeException)
                {
                    throw (new GitStoreError("Tree " + Id + " corrupt"));
                }
            }
            
        }

        internal static ITree Write(Repository aRepository, IEnumerable<TreeItem> aItems)
        {
            int count = 0;

            List<byte[]> list = new List<byte[]>();

            foreach (TreeItem item in aItems)
            {
                byte[] bytes = item.Bytes();
                list.Add(bytes);
                count += bytes.Length;
            }

            byte[] obj = new byte[count];

            int offset = 0;

            foreach (byte[] item in list)
            {
                Array.Copy(item, 0, obj, offset, item.Length);
                offset += item.Length;
            }

            string id = aRepository.WriteObject(obj, EObjectType.Tree);

            // TODO

            return (new Tree(new TreeRef(aRepository, id, obj)));
        }

        internal static ITree WriteRoot(Repository aRepository)
        {
            return (Write(aRepository, new List<TreeItem>()));
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

        public ICollection<ITreeEntry> Items
        {
            get
            {
                Resolve();
                return (iItems);
            }
        }

        public void Resolve(IObjectResolver aHandler)
        {
            aHandler.Resolve(this);
        }

        private ITreeRef iReference;
        private List<ITreeEntry> iItems;
    }

}
