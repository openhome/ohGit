using System;
using System.Runtime.Serialization;

namespace OpenHome.Git
{
    [Serializable]
    public class GitException : Exception, ISerializable
    {
        internal GitException(string aMessage)
            : base(aMessage)
        {
        }

        internal GitException(string aMessage, Exception aInnerException)
            : base(aMessage, aInnerException)
        {
        }
    }

    [Serializable]
    public class GitHeadCorruptException : GitException
    {
        public GitHeadCorruptException()
            : base("HEAD file is corrupt")
        {
        }
    }

    [Serializable]
    public class GitOriginNotFoundException : GitException
    {
        public GitOriginNotFoundException()
            : base("Origin not found in configuration")
        {
        }
    }

    [Serializable]
    public class GitCorruptCommitException : GitException
    {
        public GitCorruptCommitException(string aId)
            : base("Corrupt commit : " + aId)
        {
        }
    }
}
