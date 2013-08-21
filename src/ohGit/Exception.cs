using System;

namespace OpenHome.Git
{
    public class GitException : Exception
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

    public class GitHeadCorruptException : GitException
    {
        public GitHeadCorruptException()
            : base("HEAD file is corrupt")
        {
        }
    }

    public class GitOriginNotFoundException : GitException
    {
        public GitOriginNotFoundException()
            : base("Origin not found in configuration")
        {
        }
    }

    public class GitCorruptCommitException : GitException
    {
        public GitCorruptCommitException(string aId)
            : base("Corrupt commit : " + aId)
        {
        }
    }
}
