using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace OpenHome.Git
{
    enum EObjectType
    {
        Commit = 1,
        Tree = 2,
        Blob = 3,
        Tag = 4
    }

    internal class Object
    {
        internal static string TypeString(EObjectType aType)
        {
            switch (aType)
            {
                case EObjectType.Blob:
                    return ("blob");
                case EObjectType.Commit:
                    return ("commit");
                case EObjectType.Tree:
                    return ("tree");
                case EObjectType.Tag:
                    return ("tag");
                default:
                    throw (new GitException("Unknown object type"));
            }
        }

        internal static byte[] TypeBytes(EObjectType aType)
        {
            return (ASCIIEncoding.ASCII.GetBytes(TypeString(aType)));
        }

        internal static byte TypeByte(EObjectType aType)
        {
            return ((byte)aType);
        }

        internal Object(EObjectType aType, byte[] aContents)
        {
            iType = aType;
            iContents = aContents;
        }

        internal EObjectType Type
        {
            get
            {
                return (iType);
            }
        }

        internal byte[] Contents
        {
            get
            {
                return (iContents);
            }
        }

        internal IObject Create(Repository aRepository, string aId)
        {
            switch (iType)
            {
                case EObjectType.Blob:
                    return (new Blob(aRepository, aId, iContents));
                case EObjectType.Commit:
                    return (new Commit(new CommitRef(aRepository, aId, iContents)));
                case EObjectType.Tag:
                    return (new Tag(aRepository, aId, iContents));
                case EObjectType.Tree:
                    return (new Tree(new TreeRef(aRepository, aId, iContents)));
                default:
                    throw (new GitException("Object " + aId + " corrupt"));
            }
        }

        private EObjectType iType;
        private byte[] iContents;
    }

    internal class ObjectRefFile
    {
        const int kSha1Bytes = 40;

        protected ObjectRefFile(Repository aRepository, FileInfo aFileInfo)
        {
            iRepository = aRepository;
            iFileInfo = aFileInfo;
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
                return (iFileInfo.Name);
            }
        }

        public string Id
        {
            get
            {
                ReadId();
                return (iId);
            }
        }

        internal void ReadId()
        {
            if (iId == null)
            {
                if (iFileInfo.Length != kSha1Bytes + 1) // extra one for a newline
                {
                    throw (new GitException(iFileInfo.FullName + " is not " + kSha1Bytes + " in length"));
                }

                FileStream file = File.OpenRead(iFileInfo.FullName);

                byte[] bytes = new byte[kSha1Bytes];

                int count = file.Read(bytes, 0, kSha1Bytes);

                if (count != kSha1Bytes)
                {
                    throw (new GitException("Unable to read " + iFileInfo.FullName));
                }

                try
                {
                    iId = System.Text.ASCIIEncoding.ASCII.GetString(bytes);
                }
                catch (Exception e)
                {
                    throw (new GitException(iFileInfo.FullName + " does not contain a sha1 object id", e));
                }
            }
        }

        public void Update(string aId)
        {
            iId = aId;

            FileStream file = iFileInfo.Create();

            StreamWriter writer = new StreamWriter(file);
            writer.Write(iId + "\n");
            writer.Flush();

            file.Close();
        }

        private Repository iRepository;
        private FileInfo iFileInfo;
        private string iId;
    }
}
