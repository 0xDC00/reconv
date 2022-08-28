using BufLib.Common.IO;
using BufLib.TextFormats.DataModels;
using ExR.Format;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BufLib.TextFormats.BinaryModels.NieRAutomata
{
    internal static partial class BIN
    {
        public static List<Line> ExtractText(EndianBinaryReader br)
        {
            Console.WriteLine("BIN.ExtractText");
            br.Endianness = Endian.BigEndian;

            var currentRecord = -1;
            var remainRecord = 0;
            var rite = br.ReadStruct<RITE>();
            var irep_section = br.ReadStruct<IREP_SECTION>();
            var IREP_RECORD = new IREP_RECORD(br);

            /* scan text record... */
            List<Line> lines;
            do
            {
                IREP_RECORD.Read();
                lines = IREP_RECORD.ExtractLines(); // 1. extract first

                currentRecord++;
                remainRecord--;
                remainRecord += IREP_RECORD.rlen;
            }
            while (lines.Count == 0 && remainRecord > 0); // 2. neu van khong co text, va con record khac -> tiep tuc scan

            if (lines.Count > 0)
                lines.Insert(0, new Line(currentRecord, string.Empty));  // need for re-import

            return lines; // 4. done
        }

        public static List<Line> ExtractText(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var br = new EndianBinaryReader(ms, Endian.BigEndian))
                return ExtractText(br);
        }
    }
}
