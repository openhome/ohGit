using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GitStore
{
    internal class Hash
    {
        internal Hash()
        {
            iAlgorithm = new System.Security.Cryptography.SHA1Managed();
        }

        internal byte[] Compute(byte[] aObject)
        {
            return (iAlgorithm.ComputeHash(aObject));
        }

        internal static string String(byte[] aSha1)
        {
            return (String(aSha1, 0));
        }

        internal static string String(byte[] aSha1, int aOffset)
        {
            StringBuilder builder = new StringBuilder();

            for (uint i = 0; i < 20; i++)
            {
                builder.Append(aSha1[aOffset + i].ToString("x2"));
            }

            return (builder.ToString());
        }

        internal static byte[] Bytes(string aId)
        {
            byte[] bytes = new byte[20];

            for (int i = 0; i < 20; i++)
            {
                bytes[i] = byte.Parse(aId.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber);
            }

            return (bytes);
        }

        private System.Security.Cryptography.SHA1Managed iAlgorithm;
    }
}
