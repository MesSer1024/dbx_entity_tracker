using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbxEntityTracker
{
    class DbxParsingData
    {
        public string Filepath { get; set; }
        public List<int> LineNumbers { get; private set; }
        public List<string> EntityTypes { get; private set; }

        public DbxParsingData()
        {
            LineNumbers = new List<int>();
            EntityTypes = new List<string>();
            Filepath = "";
        }
    }

    public class DbxMatch
    {
        public string FilePath { get; set; }
        public int LineNumber { get; set; }
        public string EntityType { get; set; }

        public override string ToString()
        {
            return FilePath;
        }
    }

}
