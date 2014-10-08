using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbxEntityTracker
{
    class DbxMatches
    {
        public string Filepath { get; set; }
        public List<int> LineNumbers { get; private set; }
        public List<string> EntityType { get; private set; }

        public DbxMatches()
        {
            LineNumbers = new List<int>();
            EntityType = new List<string>();
            Filepath = "";
        }
    }
}
