using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace GitStore
{
    internal class Config
    {
        internal Config(string aPath)
        {
            FileStream file;

            try
            {
                file = File.OpenRead(aPath);
            }
            catch (Exception e)
            {
                throw (new GitStoreError("Unable to open config file", e));
            }

            try
            {
                StreamReader reader = new StreamReader(file);
                iContents = reader.ReadToEnd();
            }
            catch (Exception e)
            {
                throw (new GitStoreError("Error reading config file", e));
            }
            
        }

        private string iContents;
    }
}
