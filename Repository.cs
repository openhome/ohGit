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
        public static IRepository Initialise(string aPath, string aBranch)
        {
            if (Directory.Exists(Path.Combine(aPath, ".git")))
            {
                throw (new GitStoreError("Repository already exists"));
            }

            try
            {
                DirectoryInfo folder = Directory.CreateDirectory(aPath);

                DirectoryInfo git = folder.CreateSubdirectory(".git");

                //DirectoryInfo hooks = git.CreateSubdirectory("hooks");
                DirectoryInfo info = git.CreateSubdirectory("info");
                DirectoryInfo objects = git.CreateSubdirectory("objects");
                DirectoryInfo refs = git.CreateSubdirectory("refs");

                //DirectoryInfo objectsInfo = objects.CreateSubdirectory("info");
                //DirectoryInfo objectsPack = objects.CreateSubdirectory("pack");

                //DirectoryInfo refsHeads = refs.CreateSubdirectory("heads");
                //DirectoryInfo refsTags = refs.CreateSubdirectory("tags");

                FileStream exclude = File.Create(Path.Combine(info.FullName, "exclude"));
                StreamWriter excludeWriter = new StreamWriter(exclude);
                excludeWriter.Write("# git-ls-files --others --exclude-from=.git/info/exclude\n");
                excludeWriter.Write("# Lines that start with '#' are comments.\n");
                excludeWriter.Write("# For a project mostly in C, the following would be a good set of\n");
                excludeWriter.Write("# exclude patterns (uncomment them if you want to use them):\n");
                excludeWriter.Write("# *.[oa]\n");
                excludeWriter.Write("# *~\n");
                excludeWriter.Flush();
                exclude.Close();

                FileStream head = File.Create(Path.Combine(git.FullName, "HEAD"));
                StreamWriter headWriter = new StreamWriter(head);
                headWriter.Write("ref: refs/heads/" + aBranch + "\n");
                headWriter.Flush();
                head.Close();

                FileStream description = File.Create(Path.Combine(git.FullName, "description"));
                StreamWriter descriptionWriter = new StreamWriter(description);
                descriptionWriter.Write("Unnamed repository; edit this file 'description' to name the repository.\n");
                descriptionWriter.Flush();
                description.Close();

                FileStream config = File.Create(Path.Combine(git.FullName, "config"));
                StreamWriter configWriter = new StreamWriter(config);
                configWriter.Write("[core]\n");
                configWriter.Write("\trepositoryformatversion = 0\n");
                configWriter.Write("\tfilemode = false\n");
                configWriter.Write("\tbare = false\n");
                configWriter.Write("\tlogallrefupdates = true\n");
                configWriter.Write("\tsymlinks = false\n");
                configWriter.Write("\tignorecase = true\n");
                configWriter.Write("\thideDotFiles = dotGitOnly\n");
                configWriter.Flush();
                config.Close();
            }
            catch (Exception e)
            {
                throw (new GitStoreError("Unable to create specified repository", e));
            }

            Repository repository = new Repository(aPath);

            return (repository);
        }

        public Repository(string aPath)
        {
            string path = Path.Combine(aPath, ".git");

            try
            {
                iFolder = new DirectoryInfo(path);
            }
            catch (Exception e)
            {
                throw (new GitStoreError("Failed to open the specified git repository", e));
            }

            FindFolders();
            FindBranches();
            FindHead();
            FindTags();
            FindPackedRefs();
            FindPacks();

            iHash = new Hash();
        }

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

            FileStream file = File.Create(path);

            DeflaterOutputStream deflater = new DeflaterOutputStream(file);
            BinaryWriter binary = new BinaryWriter(deflater);
            binary.Write(obj);
            binary.Flush();

            deflater.Finish();

            file.Close();

            return (id);
        }

        internal IBranch CreateBranch(string aName, string aId)
        {
            String path = Path.Combine(iFolderHeads.FullName, aName);

            if (File.Exists(path))
            {
                throw (new GitStoreError("Branch already exists"));
            }

            FileStream file = File.Create(path);
            
            StreamWriter writer = new StreamWriter(file);
            writer.Write(aId + "\n");
            writer.Flush();
            
            file.Close();

            FileInfo info = new FileInfo(path);

            Branch branch = new Branch(new BranchFile(this, info));
            
            iBranches.Add(aName, branch);

            return (branch);
        }

        private void FindFolders()
        {
            iFolderHeads = GetSubFolder("refs\\heads");
            iFolderObjects = GetSubFolder("objects");
            iFolderTags = GetSubFolder("refs\\tags");
            //iFolderRemotes = GetSubFolder("refs\\remotes");
            iFolderPack = GetSubFolder("objects\\pack");
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

            FileStream file = info.Create();
            StreamWriter writer = new StreamWriter(file);
            writer.Write(aId + "\n");
            writer.Flush();
            file.Close();

            aBranch.Update(new BranchFile(this, info));
        }

        private void FindHead()
        {
            FileStream head = File.OpenRead(Path.Combine(iFolder.FullName, "HEAD"));
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
                throw (new GitStoreError("HEAD file corrupt"));
            }

            if (parts[0] != "ref:")
            {
                throw (new GitStoreError("HEAD file corrupt"));
            }

            if (!parts[1].StartsWith("refs/heads/"))
            {
                throw (new GitStoreError("HEAD file corrupt"));
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

            throw (new GitStoreError("HEAD file corrupt"));
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
            string packedRefsPath = Path.Combine(iFolder.FullName, "packed-refs");

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
            catch (Exception)
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

        private DirectoryInfo GetSubFolder(string aPath)
        {
            try
            {
                string path = Path.Combine(iFolder.FullName, aPath);
                return (new DirectoryInfo(path));
            }
            catch (Exception e)
            {
                throw (new GitStoreError("Specified repository does not contain a " + aPath + " folder", e));
            }
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
                throw (new GitStoreError("Error accessing " + aFolder.Name + " files", e));
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
                    throw (new GitStoreError("Unable to read object " + aId));
                }
            }

            return (obj);
        }

        private Object GetObjectLoose(string aId)
        {
            FileStream file;

            try
            {
                string path = Path.Combine(iFolderObjects.FullName, aId.Substring(0, 2), aId.Substring(2, 38));

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
                        throw (new GitStoreError("Illegal object header " + aId));
                    }

                    header[offset++] = (byte)b;
                }

                string[] parts = ASCIIEncoding.ASCII.GetString(header, 0, offset).Split(new char[] { ' ' });

                if (parts.Length != 2)
                {
                    throw (new GitStoreError("Illegal object header " + aId));
                }

                int length;

                if (!int.TryParse(parts[1], out length))
                {
                    throw (new GitStoreError("Illegal object length " + aId));
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
                        throw (new GitStoreError("Unrecognised object type " + aId));
                }
            }
            catch (GitStoreError)
            {
                throw;
            }
            catch (Exception e)
            {
                throw (new GitStoreError("Unable to read object " + aId, e));
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

        private DirectoryInfo iFolder;
        private DirectoryInfo iFolderHeads;
        private DirectoryInfo iFolderObjects;
        private DirectoryInfo iFolderTags;
        //private DirectoryInfo iFolderRemotes;
        private DirectoryInfo iFolderPack;

        private string iHead;
        private Dictionary<string, IBranch> iBranches;
        private Dictionary<string, IRef> iRefs;
        
        private List<Pack> iPacks;

        private Hash iHash;
    }
}
