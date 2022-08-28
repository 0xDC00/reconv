using System.Collections.Generic;
using System.IO;
using System.Text;
using System;

namespace ExR.Format
{
    [Plugin("com.dc.tew2", "The Evil Within 2 (.lanb)", @"Extract & Repack, No _init_.yaml needed.")]
    class TheEvilWithin2 : TextFormat
    {
        public TheEvilWithin2()
        {
            Extensions = new string[] { ".lanb" };
            _Encoding = Encoding.UTF8;
        }

        public override List<Line> ExtractText(byte[] buf)
        {
            using (var ms = new MemoryStream(buf))
            using (var br = new BinaryReader(ms))
            {
                var magic = br.ReadUInt32();
                var dummy = br.ReadUInt32();
                var numLine = br.ReadInt32();

                var lines = new List<Line>(numLine + 1)
                {
                    new Line(magic + "_" + dummy, string.Empty),
                };

                for (; numLine > 0; numLine--)
                {
                    var id = br.ReadUInt32();
                    var strCodeLen = br.ReadInt32();  // txtId - #key_of_text
                    var strCode = _Encoding.GetString(br.ReadBytes(strCodeLen));

                    var strLineLen = br.ReadInt32();  // txtValue - NewGame
                    var strLine = _Encoding.GetString(br.ReadBytes(strLineLen));

                    lines.Add(new Line(id + "_" + strCode, strLine));
                }

                return lines;
            }
        }

        public override byte[] RepackText(List<Line> lines)
        {
            using (var ms = new MemoryStream(_10MB))
            {
                using (var bw = new BinaryWriter(ms))
                {
                    var tmp = lines[0].ID.Split('_');
                    lines.RemoveAt(0);

                    bw.Write(uint.Parse(tmp[0])); // magic
                    bw.Write(uint.Parse(tmp[1])); // dummy
                    bw.Write(lines.Count);       //

                    foreach (var line in lines)
                    {
                        tmp = line.ID.Split('_', 2);
                        bw.Write(uint.Parse(tmp[0])); // id

                        var raw = _Encoding.GetBytes(tmp[1]);
                        bw.Write(raw.Length);
                        bw.Write(raw);

                        raw = _Encoding.GetBytes(line.English);
                        bw.Write(raw.Length);
                        bw.Write(raw);
                    }
                }

                return ms.ToArray();
            }
        }
    }
}
