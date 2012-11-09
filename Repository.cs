using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;

using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace OpenHome.Git
{
    internal class Repository : IRepository
    {
        private readonly string iPath;
        private readonly string iUri;
        private readonly string iBranch;

        private readonly DirectoryInfo iFolder;
        private readonly DirectoryInfo iFolderGit;
        private readonly DirectoryInfo iFolderObjects;
        private readonly DirectoryInfo iFolderInfo;
        private readonly DirectoryInfo iFolderPack;
        private readonly DirectoryInfo iFolderRefs;
        private readonly DirectoryInfo iFolderHeads;
        private readonly DirectoryInfo iFolderTags;
        //private readonly DirectoryInfo iFolderRemotes;

        private string iHead;
        private Dictionary<string, IBranch> iBranches;
        private Dictionary<string, IRef> iRefs;

        private List<Pack> iPacks;

        private Hash iHash;

        public Repository(string aPath, string aUri, string aBranch)
        {
            iPath = aPath;
            iUri = aUri;
            iBranch = aBranch;

            while (true)
            {
                if (!Directory.Exists(aPath))
                {
                    iFolder = Directory.CreateDirectory(iPath);

                    iFolderGit = iFolder.CreateSubdirectory(".git");

                    //DirectoryInfo hooks = git.CreateSubdirectory("hooks");
                    iFolderObjects = iFolderGit.CreateSubdirectory("objects");
                    iFolderInfo = iFolderObjects.CreateSubdirectory("info");
                    iFolderPack = iFolderObjects.CreateSubdirectory("pack");

                    iFolderRefs = iFolderGit.CreateSubdirectory("refs");
                    iFolderHeads = iFolderRefs.CreateSubdirectory("heads");
                    iFolderTags = iFolderRefs.CreateSubdirectory("tags");

                    var info = iFolderGit.CreateSubdirectory("info");

                    using (var exclude = new FileStream(Path.Combine(info.FullName, "exclude"), FileMode.CreateNew, FileAccess.Write))
                    {
                        using (var writer = new StreamWriter(exclude))
                        {
                            writer.Write("# git-ls-files --others --exclude-from=.git/info/exclude\n");
                            writer.Write("# Lines that start with '#' are comments.\n");
                            writer.Write("# For a project mostly in C, the following would be a good set of\n");
                            writer.Write("# exclude patterns (uncomment them if you want to use them):\n");
                            writer.Write("# *.[oa]\n");
                            writer.Write("# *~\n");
                        }
                    }

                    using (var head = new FileStream(Path.Combine(iFolderGit.FullName, "HEAD"), FileMode.CreateNew, FileAccess.Write))
                    {
                        using (var writer = new StreamWriter(head))
                        {
                            writer.Write("ref: refs/heads/" + aBranch + "\n");
                        }
                    }

                    using (var description = new FileStream(Path.Combine(iFolderGit.FullName, "description"), FileMode.CreateNew, FileAccess.Write))
                    {
                        using (var writer = new StreamWriter(description))
                        {
                            writer.Write("Unnamed repository; edit this file 'description' to name the repository.\n");
                        }
                    }

                    using (var config = new FileStream(Path.Combine(iFolderGit.FullName, "config"), FileMode.CreateNew, FileAccess.Write))
                    {
                        using (var writer = new StreamWriter(config))
                        {
                            writer.Write("[core]\n");
                            writer.Write("\trepositoryformatversion = 0\n");
                            writer.Write("\tfilemode = false\n");
                            writer.Write("\tbare = false\n");
                            writer.Write("\tlogallrefupdates = true\n");
                            writer.Write("\tsymlinks = false\n");
                            writer.Write("\tignorecase = true\n");
                            writer.Write("\thideDotFiles = dotGitOnly\n");
                            writer.Write("[remote \"origin\"]\n");
                            writer.Write("\turl = {0}\n", iUri);
                            writer.Write("[branch \"master\"]\n");
                            writer.Write("\tremote = origin\n");
                        }
                    }
                }
                else
                {
                    try
                    {
                        iFolder = new DirectoryInfo(iPath);
                        iFolderGit = GetSubFolder(iFolder, ".git");
                        iFolderObjects = GetSubFolder(iFolderGit, "objects");
                        iFolderInfo = GetSubFolder(iFolderObjects, "info");
                        iFolderPack = GetSubFolder(iFolderObjects, "pack");
                        iFolderRefs = GetSubFolder(iFolderGit, "refs");
                        iFolderHeads = GetSubFolder(iFolderRefs, "heads");
                        iFolderTags = GetSubFolder(iFolderRefs, "tags");
                        //iFolderRemotes = GetSubFolder(iFolderRefs, "remotes");
                        break;
                    }
                    catch
                    {
                        Delete();
                    }
                }
            }

            FindBranches();
            FindHead();
            FindTags();
            FindPackedRefs();
            FindPacks();

            iHash = new Hash();
        }

        private DirectoryInfo GetSubFolder(DirectoryInfo aFolder, string aSubFolder)
        {
            var path = Path.Combine(aFolder.FullName, aSubFolder);
            return (new DirectoryInfo(path));
        }

        private FileInfo[] GetSubFolderFiles(DirectoryInfo aFolder)
        {
            try
            {
                FileInfo[] files = aFolder.GetFiles();
                return (files);
            }
            catch (Exception e)
            {
                throw (new GitError("Error accessing " + aFolder.Name + " files", e));
            }
        }

        internal string WriteObject(byte[] aContent, EObjectType aType)
        {
            byte zero = 0;
            byte space = (byte)' ';
            byte[] type = Object.TypeBytes(aType);
            byte[] length = ASCIIEncoding.ASCII.GetBytes(aContent.Length.ToString());
            byte[] obj = new byte[type.Length + length.Length + aContent.Length + 2];

            Array.Copy(type, 0, obj, 0, type.Length);
            obj[type.Length] = space;
            Array.Copy(length, 0, obj, type.Length + 1, length.Length);
            obj[type.Length + length.Length + 1] = zero;
            Array.Copy(aContent, 0, obj, type.Length + length.Length + 2, aContent.Length);

            string id = Hash.String(iHash.Compute(obj));

            DirectoryInfo folder = iFolderObjects.CreateSubdirectory(id.Substring(0, 2));
            
            String path = Path.Combine(folder.FullName, id.Substring(2));

            if (File.Exists(path))
            {
                return (id);
            }

            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            {
                DeflaterOutputStream deflater = new DeflaterOutputStream(file);

                using (var writer = new BinaryWriter(deflater))
                {
                    writer.Write(obj);
                    deflater.Finish();
                }
            }

            return (id);
        }

        internal IBranch CreateBranch(string aName, string aId)
        {
            String path = Path.Combine(iFolderHeads.FullName, aName);

            if (File.Exists(path))
            {
                throw (new GitError("Branch already exists"));
            }

            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            {
                using (var writer = new StreamWriter(file))
                {
                    writer.Write(aId + "\n");
                }
            }

            FileInfo info = new FileInfo(path);

            Branch branch = new Branch(new BranchFile(this, info));
            
            iBranches.Add(aName, branch);

            return (branch);
        }


        private void FindBranches()
        {
            FileInfo[] branches = GetSubFolderFiles(iFolderHeads);

            iBranches = new Dictionary<string, IBranch>();

            foreach (FileInfo file in branches)
            {
                iBranches.Add(file.Name, new Branch(new BranchFile(this, file)));
            }
        }

        internal void UpdateBranch(Branch aBranch, string aId)
        {
            string path = Path.Combine(iFolderHeads.FullName, aBranch.Name);

            FileInfo info = new FileInfo(path);

            using (var file = info.Create())
            {
                using (var writer = new StreamWriter(file))
                {
                    writer.Write(aId + "\n");
                }
            }

            aBranch.Update(new BranchFile(this, info));
        }

        private void FindHead()
        {
            FileStream head = File.OpenRead(Path.Combine(iFolderGit.FullName, "HEAD"));
            StreamReader reader = new StreamReader(head);
            string contents = reader.ReadToEnd();
            head.Close();

            if (contents.EndsWith("\n"))
            {
                contents = contents.Substring(0, contents.Length - 1);
            }

            string[] parts = contents.Split(new char[] { ' ' });

            if (parts.Length != 2)
            {
                throw (new GitError("HEAD file corrupt"));
            }

            if (parts[0] != "ref:")
            {
                throw (new GitError("HEAD file corrupt"));
            }

            if (!parts[1].StartsWith("refs/heads/"))
            {
                throw (new GitError("HEAD file corrupt"));
            }

            iHead = parts[1].Substring(11);

            if (iBranches.ContainsKey(iHead))
            {
                return;
            }

            if (iBranches.Count == 0)
            {
                iBranches.Add(iHead, new Branch(new BranchEmpty(this, iHead)));
                return;
            }

            throw (new GitError("HEAD file corrupt"));
        }

        void FindTags()
        {
            FileInfo[] tags = GetSubFolderFiles(iFolderTags);

            iRefs = new Dictionary<string, IRef>();

            foreach (FileInfo file in tags)
            {
                iRefs.Add(file.Name, new RefFile(this, file));
            }
        }

        private void FindPackedRefs()
        {
            string packedRefsPath = Path.Combine(iFolderGit.FullName, "packed-refs");

            try
            {
                FileStream file = File.OpenRead(packedRefsPath);

                StreamReader reader = new StreamReader(file);

                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();

                    if (line.Length > 0)
                    {
                        if (Char.IsLetterOrDigit(line, 0))
                        {
                            string[] parts = line.Split(new char[] { ' ' });

                            if (parts.Length == 2)
                            {
                                if (parts[0].Length == 40)
                                {
                                    if (parts[1].StartsWith("refs/tags/"))
                                    {
                                        string name = parts[1].Substring(10);
                                        iRefs.Add(name, new RefPacked(this, name, parts[0]));
                                    }
                                    /*
                                    else if (parts[1].StartsWith("refs/remotes/"))
                                    {
                                        iBranches.Add(new BranchRefPacked(this, parts[1].Substring(13), parts[0]));
                                    }
                                    */
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private void FindPacks()
        {
            FileInfo[] packs = GetSubFolderFiles(iFolderPack);

            iPacks = new List<Pack>();

            foreach (FileInfo file in packs)
            {
                if (file.Extension == ".idx")
                {
                    iPacks.Add(new Pack(file.FullName));
                }
            }
        }

        internal Object GetObject(string aId)
        {
            Object obj = GetObjectLoose(aId);

            if (obj == null)
            {
                obj = GetObjectPacked(aId);

                if (obj == null)
                {
                    throw (new GitError("Unable to read object " + aId));
                }
            }

            return (obj);
        }

        private Object GetObjectLoose(string aId)
        {
            FileStream file;

            try
            {
                string path = Path.Combine(iFolderObjects.FullName, aId.Substring(0, 2));
                path = Path.Combine(path, aId.Substring(2, 38));

                file = File.OpenRead(path);
            }
            catch (Exception)
            {
                return (null);
            }

            try
            {
                InflaterInputStream inflater = new InflaterInputStream(file);

                int offset = 0;

                byte[] header = new byte[100];

                while (true)
                {
                    int b = inflater.ReadByte();

                    if (b == 0)
                    {
                        break;
                    }

                    if (offset >= 100)
                    {
                        throw (new GitError("Illegal object header " + aId));
                    }

                    header[offset++] = (byte)b;
                }

                string[] parts = ASCIIEncoding.ASCII.GetString(header, 0, offset).Split(new char[] { ' ' });

                if (parts.Length != 2)
                {
                    throw (new GitError("Illegal object header " + aId));
                }

                int length;

                if (!int.TryParse(parts[1], out length))
                {
                    throw (new GitError("Illegal object length " + aId));
                }

                byte[] bytes = new byte[length];

                inflater.Read(bytes, 0, length);

                switch (parts[0])
                {
                    case "commit":
                        return (new Object(EObjectType.Commit, bytes));
                    case "tag":
                        return (new Object(EObjectType.Tag, bytes));
                    case "tree":
                        return (new Object(EObjectType.Tree, bytes));
                    case "blob":
                        return (new Object(EObjectType.Blob, bytes));
                    default:
                        throw (new GitError("Unrecognised object type " + aId));
                }
            }
            catch (GitError)
            {
                throw;
            }
            catch (Exception e)
            {
                throw (new GitError("Unable to read object " + aId, e));
            }
        }

        private Object GetObjectPacked(string aId)
        {
            foreach (Pack file in iPacks)
            {
                Object obj = file.Read(Hash.Bytes(aId));

                if (obj != null)
                {
                    return (obj);
                }
            }

            return (null);
        }

        // IRepository

        public string Head
        {
            get
            {
                return (iHead);
            }
        }

        public IDictionary<string, IBranch> Branches
        {
            get
            {
                return (iBranches);
            }
        }

        public IDictionary<string, IRef> Refs
        {
            get
            {
                return (iRefs);
            }
        }

        public void Delete()
        {
            Directory.Delete(iPath, true);
        }

        public bool Fetch()
        {
            return (Fetcher.Fetch(this, iUri));
        }
    }
}
