using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BufLib.TextFormats.DataModels
{
    public class EntryInfo
    {
        /// <summary>
        /// Name hash
        /// </summary>
        public uint H { get; set; }

        /// <summary>
        /// Type
        /// </summary>
        public string T { get; set; }

        /// <summary>
        /// Offset
        /// </summary>
        public int O { get; set; }

        /// <summary>
        /// OriSize
        /// </summary>
        public int S { get; set; }

        /// <summary>
        /// Tiger index
        /// </summary>
        public int I { get; set; }

        /// <summary>
        /// Entry index
        /// </summary>
        public int E { get; set; }

        /// <summary>
        ///  Num line
        /// </summary>
        public int N { get; set; }
    }

    public class CINEInfo
    {
        /// <summary>
        /// Flag
        /// </summary>
        public bool F { get; set; }

        /// <summary>
        /// Index
        /// </summary>
        public int I { get; set; }

        /// <summary>
        /// Pos/Offset
        /// </summary>
        public long O { get; set; }

        public string[] T { get; set; }

        /// <summary>
        ///  Prefix
        /// </summary>
        public string P { get; set; }
    }

    public class SCHInfo
    {
        /// <summary>
        /// Begin/Start
        /// </summary>
        public string S { get; set; }

        //string U { get; set; } // enlish -> col Eng

        /// <summary>
        /// Middle
        /// </summary>
        public string M { get; set; }

        //string J { get; set; } // japanse -> col Vie

        /// <summary>
        /// End
        /// </summary>
        public string E { get; set; }

        public SCHInfo()
        {
            S = string.Empty;
            M = string.Empty;
            E = string.Empty;
        }
    }
}
