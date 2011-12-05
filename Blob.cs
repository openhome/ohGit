using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GitStore
{
    class Blob : IBlob
    {
        public Blob(Repository aRepository, string aId, byte[] aContents)
        {
            iRepository = aRepository;
            iId = aId;
            iContents = aContents;
        }

        internal static IBlob Write(Repository aRepository, string aContents)
        {
            byte[] contents = UTF8Encoding.UTF8.GetBytes(aContents);
            return (Write(aRepository, contents));
        }

        internal static IBlob Write(Repository aRepository, byte[] aContents)
        {
            string id = aRepository.WriteObject(aContents, EObjectType.Blob);
            return (new Blob(aRepository, id, aContents));
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

        public byte[] Contents
        {
            get
            {
                return (iContents);
            }
        }

        public void Resolve(IObjectResolver aHandler)
        {
            aHandler.Resolve(this);
        }

        private Repository iRepository;
        private string iId;

        byte[] iContents;
    }
}
