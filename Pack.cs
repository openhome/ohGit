using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace GitStore
{
    internal class Pack
    {
        const uint kGitIndexVersion = 0x02000000;
        const uint kGitIndexSignature = 0x634f74ff;

        const uint kGitPackVersion2 = 0x02000000;
        const uint kGitPackVersion3 = 0x02000000;
        const uint kGitPackSignature = 0x4b434150;

        internal Pack(string aIndexPath)
        {
            iIndexPath = aIndexPath;

            iPackPath = Path.ChangeExtension(iIndexPath, ".pack");

            using (FileStream index = File.OpenRead(iIndexPath))
            {
                try
                {
                    BinaryReader reader = new BinaryReader(index);

                    uint signature = reader.ReadUInt32();

                    if (signature != kGitIndexSignature)
                    {
                        throw (new GitStoreError("Pack index file " + iIndexPath + " has an invalid signature"));
                    }

                    uint version = reader.ReadUInt32();

                    if (version != kGitIndexVersion)
                    {
                        throw (new GitStoreError("Pack index file " + iIndexPath + " has an incomaptible version"));
                    }

                    iIndexFanout = reader.ReadBytes(256 * 4);

                    iObjectCount = GetEntry(iIndexFanout, 255);

                    iIndexSha1 = reader.ReadBytes((int)(iObjectCount * 20));
                    iIndexCrc = reader.ReadBytes((int)(iObjectCount * 4));
                    iIndexOffset = reader.ReadBytes((int)(iObjectCount * 4));
                    iChecksumPack = reader.ReadBytes(20);
                    iChecksumIndex = reader.ReadBytes(20);
                }
                catch (GitStoreError)
                {
                    throw;
                }
                catch (Exception e)
                {
                    throw (new GitStoreError("Unable to read " + iIndexPath, e));
                }
            }

            using (FileStream pack = File.OpenRead(iPackPath))
            {
                try
                {
                    BinaryReader reader = new BinaryReader(pack);

                    ReadSignature(reader);

                    iPackVersion = ReadVersion(reader);

                    uint count = ReadItemCount(reader);

                    if (count == iObjectCount)
                    {
                        return;
                    }
                }
                catch (Exception e)
                {
                    throw (new GitStoreError("Unable to read " + iPackPath, e));
                }

                throw (new GitStoreError("Pack file " + iPackPath + " and index file " + iIndexPath + " containt unequal numbers of objects"));
            }
        }

        internal static void ReadSignature(BinaryReader aReader)
        {
            uint signature = aReader.ReadUInt32();

            if (signature != kGitPackSignature)
            {
                throw (new GitStoreError("Pack has an invalid signature"));
            }
        }

        internal static uint ReadVersion(BinaryReader aReader)
        {
            uint version = aReader.ReadUInt32();

            if (version == kGitPackVersion2)
            {
                return (2);
            }

            if (version == kGitPackVersion3)
            {
                return (3);
            }

            throw (new GitStoreError("Pack file has an incomaptible version"));
        }

        internal static uint ReadItemCount(BinaryReader aReader)
        {
            byte[] items = aReader.ReadBytes(4);
            return (GetEntry(items, 0));
        }

        internal Object Read(byte[] aId)
        {
            uint first = aId[0];

            uint index = 0;

            if (first > 0)
            {
                index = GetEntry(iIndexFanout, first - 1);
            }

            while (index < iObjectCount)
            {
                if (Sha1Equals(index, aId))
                {
                    uint offset = GetEntry(iIndexOffset, index);

                    return (Read(offset));
                }

                index++;
            }

            return (null);
        }

        internal Object Read(uint aOffset)
        {
            using (FileStream pack = File.OpenRead(iPackPath))
            {
                try
                {
                    pack.Position = aOffset;

                    BinaryReader reader = new BinaryReader(pack);

                    long length;

                    int type = ReadItemTypeAndLength(reader, out length);

                    switch (type)
                    {
                        case 0:
                        case 5:
                            throw (new GitStoreError("Illegal object in " + iPackPath + " at offset " + aOffset));

                        case 6:
                            uint offset = aOffset - ReadDeltaOffset(reader);
                            return (ApplyDelta(reader, Read(offset), length));
                        
                        case 7:
                            byte[] id = reader.ReadBytes(20);
                            return (ApplyDelta(reader, Read(id), length));
                        
                        default:
                            return (ReadObject(reader, type, length));
                    }
                }
                catch (GitStoreError)
                {
                    throw;
                }
                catch (Exception e)
                {
                    throw (new GitStoreError("Unable to read " + iPackPath, e));
                }
            }
        }

        internal static int ReadItemTypeAndLength(BinaryReader aReader, out long aLength)
        {
            byte b = aReader.ReadByte();

            int type = b >> 4 & 0x07;

            aLength = b & 0x0f;

            int shift = 4;

            while ((b & 0x80) > 0)
            {
                b = aReader.ReadByte();

                aLength += (b & 0x7f) << shift;

                shift += 7;
            }

            return (type);
        }

        internal static uint ReadDeltaOffset(BinaryReader aReader)
        {
            byte b = aReader.ReadByte();

            uint offset = b & 0x7fu;

            while ((b & 0x80u) != 0)
            {
                b = aReader.ReadByte();
                offset = ((offset + 1) << 7) + (uint)(b & 0x7f);
            }

            return (offset);
        }

        private static byte[] Inflate(BinaryReader aReader, long aLength)
        {
            Stream raw = aReader.BaseStream;

            Inflater inflater = new Inflater();

            InflaterInputStream stream = new InflaterInputStream(raw, inflater);

            long position = raw.Position;

            byte[] bytes = new byte[aLength];

            stream.Read(bytes, 0, (int)aLength);

            raw.Position = position + inflater.TotalIn + 4; // don't know why we add 4 but it works

            return (bytes);
        }

        internal static Object ReadObject(BinaryReader aReader, int aType, long aLength)
        {
            byte[] bytes = Inflate(aReader, aLength);

            return (new Object((EObjectType)aType, bytes));
        }

        internal static Object ApplyDelta(BinaryReader aReader, Object aObject, long aLength)
        {
            byte[] delta = Inflate(aReader, aLength);

            byte b;

            int shift = 7;

            uint deltaOffset = 0;

            b = delta[deltaOffset++];

            uint inputLength = b & 0x7fu;

            while ((b & 0x80u) != 0)
            {
                b = delta[deltaOffset++];
                inputLength += (b & 0x7fu) << shift;
                shift += 7;
            }

            shift = 7;

            b = delta[deltaOffset++];

            uint outputLength = b & 0x7fu;

            while ((b & 0x80u) != 0)
            {
                b = delta[deltaOffset++];
                outputLength += (b & 0x7fu) << shift;
                shift += 7;
            }

            byte[] output = new byte[outputLength];

            uint outputOffset = 0;

            while (deltaOffset < delta.Length)
            {
                byte opcode = delta[deltaOffset++];

                if ((opcode & 0x80u) != 0)
                {   // copy
                    shift = 0;

                    uint chunkOffset = 0;
                    uint chunkLength = 0;

                    for (uint i = 0; i < 4; i++)
                    {
                        if ((opcode & 0x01u) > 0)
                        {
                            uint x = delta[deltaOffset++];
                            chunkOffset += (x << shift);
                        }

                        opcode >>= 1;

                        shift += 8;
                    }

                    shift = 0;

                    for (uint i = 0; i < 3; i++)
                    {
                        if ((opcode & 0x01u) > 0)
                        {
                            uint x = delta[deltaOffset++];
                            chunkLength += (x << shift);
                        }

                        opcode >>= 1;

                        shift += 8;
                    }

                    if (chunkLength == 0)
                    {
                        chunkLength = 1 << 16;
                    }

                    Array.Copy(aObject.Contents, chunkOffset, output, outputOffset, chunkLength);

                    outputOffset += chunkLength;
                }
                else
                {   // insert
                    uint chunkLength = (opcode & 0x7fu);

                    Array.Copy(delta, deltaOffset, output, outputOffset, chunkLength);

                    deltaOffset += chunkLength;

                    outputOffset += chunkLength;
                }
            }

            return (new Object(aObject.Type, output));
        }

        private static uint GetEntry(byte[] aTable, uint aIndex)
        {
            uint index = aIndex * 4;

            uint value = (uint)aTable[index++] << 24;
            value += (uint)aTable[index++] << 16;
            value += (uint)aTable[index++] << 8;
            value += (uint)aTable[index];

            return (value);
        }

        private bool Sha1Equals(uint aIndex, byte[] aId)
        {
            uint offset = aIndex * 20;

            for (uint i = 0; i < 20; i++)
            {
                if (iIndexSha1[offset + i] != aId[i])
                {
                    return (false);
                }
            }

            return (true);
        }

        private string iIndexPath;
        private string iPackPath;
        private uint iPackVersion;

        private byte[] iIndexFanout;
        private byte[] iIndexSha1;
        private byte[] iIndexCrc;
        private byte[] iIndexOffset;
        private byte[] iChecksumPack;
        private byte[] iChecksumIndex;

        private uint iObjectCount;
    }
}
