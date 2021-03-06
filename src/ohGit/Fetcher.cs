﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;

using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace OpenHome.Git
{
    internal class Fetcher
    {
        internal static bool Fetch(IRepository aRepository, string aUri)
        {
            Repository repository = aRepository as Repository;

            if (repository == null)
            {
                throw (new GitException("Invalid repository"));
            }

            Uri uri;

            try
            {
                uri = new Uri(aUri);
            }
            catch (Exception e)
            {
                throw (new GitException("Invalid uri", e));
            }

            if (uri.Scheme != "git")
            {
                throw (new GitException("Invalid transfer protocol"));
            }

            int port = 9418;

            if (!uri.IsDefaultPort)
            {
                port = uri.Port;
            }

            try
            {
                TcpClient client = new TcpClient(uri.Host, port);

                using (client)
                {
                    return (Fetch(repository, uri, client.GetStream()));
                }
            }
            catch
            {
            }

            return (false);
        }

        private static bool Fetch(Repository aRepository, Uri aUri, NetworkStream aStream)
        {
            var writer = new BinaryWriter(aStream);

            using (var reader = new BinaryReader(aStream))
            {
                return (Fetch(aRepository, aUri, writer, reader));
            }
        }

        private static bool Fetch(Repository aRepository, Uri aUri, BinaryWriter aWriter, BinaryReader aReader)
        {
            // Request

            WriteFetchRequest(aUri, aWriter);

            // Collect HEAD and capabilities

            string sha1;

            byte[] bytes = ReadFetchHeader(aReader, out sha1);

            if (bytes.Length < 5 || bytes[4] != 0)
            {
                return (false);
            }

            string head = ASCIIEncoding.ASCII.GetString(bytes, 0, 4);

            if (head != "HEAD")
            {
                return (false);
            }

            string capabilities = ASCIIEncoding.ASCII.GetString(bytes, 5, bytes.Length - 5);

            if (capabilities.EndsWith("\n"))
            {
                capabilities = capabilities.Substring(0, capabilities.Length - 1);
            }

            List<string> caps = new List<string>(capabilities.Split(new char[] { ' ' }));

            if (!caps.Contains("ofs-delta"))
            {
                return (false);
            }

            // Collect remote branches

            Dictionary<string, string> branches = new Dictionary<string, string>();

            while (true)
            {
                bytes = ReadFetchHeader(aReader, out sha1);

                if (bytes == null)
                {
                    break;
                }

                string branch = ASCIIEncoding.ASCII.GetString(bytes);

                if (branch.StartsWith("refs/heads/"))
                {
                    branch = branch.Substring(11);

                    if (branch.EndsWith("\n"))
                    {
                        branch = branch.Substring(0, branch.Length - 1);
                    }

                    if (branch.Length > 0)
                    {
                        branches.Add(branch, sha1);
                    }
                }
            }

            // Find branches that are different

            Dictionary<string, string> update = new Dictionary<string, string>();
            Dictionary<string, string> create = new Dictionary<string, string>();

            foreach (KeyValuePair<string, string> entry in branches)
            {
                var branch = aRepository.Branch(entry.Key);

                if (branch != null)
                {
                    if (branch.IsEmpty || branch.Commit.Id != entry.Value)
                    {
                        update.Add(entry.Key, entry.Value);
                    }
                }
                else
                {
                    create.Add(entry.Key, entry.Value);
                }
            }

            if (update.Count + create.Count == 0)
            {
                return (false);
            }

            // Write request

            capabilities = "\0ofs-delta";

            if (caps.Contains("thin-pack"))
            {
                capabilities += " thin-pack";
            }

            foreach (string entry in update.Values)
            {
                string want = "want " + entry + capabilities + "\n";

                WriteFetchHeader(aWriter, want);

                capabilities = String.Empty;
            }

            foreach (string entry in create.Values)
            {
                string want = "want " + entry + capabilities + "\n";

                WriteFetchHeader(aWriter, want);

                capabilities = String.Empty;
            }

            WriteZeroHeader(aWriter);

            foreach (IBranch branch in aRepository.Branches)
            {
                if (!branch.IsEmpty)
                {
                    string have = "have " + branch.Commit.Id + "\n";

                    WriteFetchHeader(aWriter, have);
                }
            }

            WriteFetchHeader(aWriter, "done\n");

            // Read acks

            foreach (string entry in create.Values)
            {
                string ack = ReadFetchRecord(aReader);
                //Console.WriteLine("CREATE: {0}", ack);
            }

            foreach (string entry in update.Values)
            {
                string ack = ReadFetchRecord(aReader);
                //Console.WriteLine("UPDATE: {0}", ack);
            }

            /*
            foreach (IBranch branch in aRepository.Branches)
            {
                string ack = ReadFetchRecord(reader);

                if (!branch.IsEmpty)
                {
                    if (!ack.StartsWith("ACK "))
                    {
                        return (false);
                    }
                }
                else
                {
                    if (ack !="NAK\n")
                    {
                        return (false);
                    }
                }
            }
            */

            // Collect pack parts

            int count = 0;

            List<byte[]> parts = new List<byte[]>();

            while (true)
            {
                byte[] part = aReader.ReadBytes(10000);

                if (part.Length == 0)
                {
                    break;
                }

                count += part.Length;

                parts.Add(part);
            }

            // Combine pack parts

            byte[] pack = new byte[count];

            int index = 0;

            foreach (byte[] part in parts)
            {
                Array.Copy(part, 0, pack, index, part.Length);
                index += part.Length;
            }

            // Read pack header

            var pstream = new MemoryStream(pack);

            using (var preader = new BinaryReader(pstream))
            {
                Pack.ReadSignature(preader);

                Pack.ReadVersion(preader);

                uint objectcount = Pack.ReadItemCount(preader);

                // Inflate items

                Dictionary<long, Object> objects = new Dictionary<long, Object>();

                while (objectcount-- > 0)
                {
                    Object obj;

                    long length;

                    long start = pstream.Position;

                    int type = Pack.ReadItemTypeAndLength(preader, out length);

                    switch (type)
                    {
                        case 0:
                        case 5:
                            return (false);

                        case 6:
                            long offset = start - Pack.ReadDeltaOffset(preader);
                            obj = Pack.ApplyDelta(preader, objects[offset], length);
                            break;

                        case 7:
                            byte[] id = new byte[20];
                            preader.Read(id, 0, 20);
                            obj = Pack.ApplyDelta(preader, aRepository.GetObject(Hash.String(id)), length);
                            break;

                        default:
                            obj = Pack.ReadObject(preader, type, length);
                            break;

                    }

                    objects.Add(start, obj);

                    aRepository.WriteObject(obj.Contents, obj.Type);
                }
            }

            // Update existing branches

            foreach (var entry in update)
            {
                aRepository.UpdateBranch(entry.Key, entry.Value);
            }

            // Create new branches

            foreach (var entry in create)
            {
                aRepository.CreateBranch(entry.Key, entry.Value);
            }

            return (true);
        }

        public static void WriteFetchRequest(Uri aUri, BinaryWriter aWriter)
        {
            byte[] command = ASCIIEncoding.ASCII.GetBytes("git-upload-pack " + aUri.PathAndQuery);

            byte[] host = ASCIIEncoding.ASCII.GetBytes("host=" + aUri.Host);

            int len = command.Length + host.Length + 6;

            byte[] length = ASCIIEncoding.ASCII.GetBytes(len.ToString("x4"));

            byte[] request = new byte[len];

            Array.Copy(length, 0, request, 0, length.Length);

            Array.Copy(command, 0, request, length.Length, command.Length);

            request[length.Length + command.Length] = 0;

            Array.Copy(host, 0, request, length.Length + command.Length + 1, host.Length);

            request[length.Length + command.Length + host.Length + 1] = 0;

            aWriter.Write(request);
        }

        public static void WriteZeroHeader(BinaryWriter aWriter)
        {
            byte[] length = ASCIIEncoding.ASCII.GetBytes("0000");

            aWriter.Write(length);
        }

        public static void WriteFetchHeader(BinaryWriter aWriter, string aMessage)
        {
            byte[] message = ASCIIEncoding.ASCII.GetBytes(aMessage);

            int len = message.Length + 4;

            byte[] length = ASCIIEncoding.ASCII.GetBytes(len.ToString("x4"));

            byte[] header = new byte[len];

            Array.Copy(length, 0, header, 0, length.Length);

            Array.Copy(message, 0, header, length.Length, message.Length);

            aWriter.Write(header);
        }

        public static byte[] ReadFetchHeader(BinaryReader aReader, out string aSha1)
        {
            byte[] bytes = aReader.ReadBytes(4);

            string length = ASCIIEncoding.ASCII.GetString(bytes);

            int len = int.Parse(length, System.Globalization.NumberStyles.HexNumber);

            if (len == 0)
            {
                aSha1 = null;
                return (null);
            }

            aSha1 = ASCIIEncoding.ASCII.GetString(aReader.ReadBytes(40));

            string space = ASCIIEncoding.ASCII.GetString(aReader.ReadBytes(1));

            if (space != " ")
            {
                throw (new GitException("Communications error"));
            }

            return (aReader.ReadBytes(len - 45));
        }

        public static string ReadFetchRecord(BinaryReader aReader)
        {
            byte[] bytes = aReader.ReadBytes(4);

            string length = ASCIIEncoding.ASCII.GetString(bytes);

            int len = int.Parse(length, System.Globalization.NumberStyles.HexNumber);

            if (len == 0)
            {
                return (null);
            }

            bytes = aReader.ReadBytes(len - 4);

            return (ASCIIEncoding.ASCII.GetString(bytes));
        }
    }
}
