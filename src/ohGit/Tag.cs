using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace OpenHome.Git
{
    internal class RefFile : ObjectRefFile, IRef
    {
        internal RefFile(Repository aRepository, FileInfo aFileInfo)
            : base(aRepository, aFileInfo)
        {
        }

        public IObject Item
        {
            get
            {
                Object obj = Repository.GetObject(Id);
                return (obj.Create(Repository, Id));
            }
        }
    }


    internal class RefPacked : IRef
    {
        internal RefPacked(Repository aRepository, string aName, string aId)
        {
            iRepository = aRepository;
            iName = aName;
            iId = aId;
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
                Object obj = iRepository.GetObject(iId);
                return (obj.Create(iRepository, iId));
            }
        }

        private Repository iRepository;
        private string iName;
        private string iId;
    }

    internal class Tag : ITag
    {
        internal Tag(Repository aRepository, string aId, byte[] aBytes)
        {
            iRepository = aRepository;
            iId = aId;

            string id = null;
            string type = null;

            string tag = ASCIIEncoding.ASCII.GetString(aBytes);

            string[] lines = tag.Split(new char[] { '\n' });

            int offset = 1;

            foreach (string line in lines)
            {
                if (line.Length == 0)
                {
                    break;
                }

                offset += line.Length + 1;

                int keyEnd = line.IndexOf(' ');

                CorruptIf(keyEnd < 0);

                string key = line.Substring(0, keyEnd);

                string value = line.Substring(keyEnd + 1);

                switch (key)
                {
                    case "object":
                        id = value;
                        break;

                    case "type":
                        type = value;
                        break;

                    case "tag":
                        iName = value;
                        break;

                    case "tagger":
                        iTagger = PersonTime.Create(value);
                        break;

                    default:
                        CorruptIf(true);
                        break;
                }
            }

            iDescription = tag.Substring(offset);

            CorruptIf(iName == null);
            CorruptIf(iTagger == null);

            CorruptIf(id == null);
            CorruptIf(type == null);

            switch (type)
            {
                case "commit":
                    iType = EObjectType.Commit;
                    break;
                case "blob":
                    iType = EObjectType.Blob;
                    break;
                case "tree":
                    iType = EObjectType.Tree;
                    break;
                case "tag":
                    iType = EObjectType.Tag;
                    break;
                default:
                    CorruptIf(true);
                    break;
            }
        }

        private void CorruptIf(bool aCondition)
        {
            if (aCondition)
            {
                throw (new GitException("Tag " + iId + " corrupt"));
            }
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

        public string Type
        {
            get
            {
                switch (iType)
                {
                    case EObjectType.Blob:
                        return ("blob");
                    case EObjectType.Commit:
                        return ("commit");
                    case EObjectType.Tag:
                        return ("tag");
                    case EObjectType.Tree:
                        return ("tree");
                    default:
                        throw (new GitException("Object type unknown"));
                }
            }
        }

        public IObject Item
        {
            get
            {
                if (iObject == null)
                {
                    Object obj = iRepository.GetObject(iId);

                    if (obj.Type != iType)
                    {
                        throw (new GitException("Item " + iId + " corrupt"));
                    }

                    iObject = obj.Create(iRepository, iId);
                }

                return (iObject);
            }
        }

        public string Name
        {
            get
            {
                return (iName);
            }
        }

        public IPersonTime Tagger
        {
            get
            {
                return (iTagger);
            }
        }

        public string Description
        {
            get
            {
                return (iDescription);
            }
        }

        public void Resolve(IObjectResolver aHandler)
        {
            aHandler.Resolve(this);
        }

        private Repository iRepository;
        private string iId;

        private IObject iObject;
        private string iName;
        private IPersonTime iTagger;
        private string iDescription;
        private EObjectType iType;
    }

}
