using BufLib.Common.IO;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ExR.Format
{
    [Plugin("com.dc.cd_sottr", "Shadow of the Tomb Raider (bigfile.*_english_*.tiger)", "Extract/Repack")]
    class CrystalDynamics_ShadowoftheTombRaider : TextFormat
    {
        public CrystalDynamics_ShadowoftheTombRaider()
        {
            Extensions = new string[] { ".tiger" };
        }

        public override List<Line> ExtractText(byte[] buf)
        {
            using (var ms = new MemoryStream(buf))
            using (var br = new EndianBinaryReader(ms))
            {
                var tigerHeaderSize = 0x60;
                var tigerHeader = br.ReadBytes(tigerHeaderSize);
                var binLines = ExtractBin(br.ReadBytes((int)br.BaseStream.Length - tigerHeaderSize));
                binLines.Insert(0, new Line(tigerHeader.ByteArrayToString(), string.Empty));

                return binLines;
            }
        }

        public override byte[] RepackText(List<Line> lines)
        {
            var tigerHeader = lines[0].ID.HexStringToByteArray();
            lines.RemoveAt(0);
            var newBin = RepackBin(lines);
            using (var ms = new MemoryStream(_10MB))
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(tigerHeader);
                bw.Write(newBin);

                // fix size
                bw.BaseStream.Position = 0x48;
                bw.Write(newBin.Length);

                return ms.ToArray();
            }
        }

        private List<Line> ExtractBin(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var br = new EndianBinaryReader(ms))
            {
                return ExtractBin(br);
            }
        }

        private List<Line> ExtractBin(EndianBinaryReader br)
        {
            string pattern1 = @"([ ]*)(\/\/)(\[[\d]+\.[\d]+\])(\/\/)([ ]*)"; // space*|//|[num+.num+]|//|space*
            var regex1 = new System.Text.RegularExpressions.Regex(pattern1, System.Text.RegularExpressions.RegexOptions.Multiline);

            var langId = br.ReadInt32();
            var numLine = br.ReadInt32();
            var pointers = br.ReadInt64s(numLine);
            var lines = new List<Line>(numLine);
            foreach (var pointer in pointers)
            {
                if (pointer == 0)
                {
                    lines.Add(new Line(string.Empty));
                }
                else
                {
                    br.BaseStream.Position = pointer;
                    var line = br.ReadTerminatedString(Encoding.UTF8);
                    var splitedLine = line.Split(new char[] { ' ' }, 2);
                    var id = splitedLine[0];
                    var value = splitedLine[1];

                    // human format
                    var replaced1 = regex1.Replace(value, m => "\n" + m.Groups[3].Value + " ");

                    lines.Add(new Line(id, replaced1));
                }
            }

            return lines;
        }

        private byte[] RepackBin(List<Line> lines)
        {
            string pattern2 = @"([ ]*)([\n]*)([ ]*)(\[[\d]+\.[\d]+\])([ ]*)"; // space*|\n*|space*|num|space*
            var regex2 = new System.Text.RegularExpressions.Regex(pattern2, System.Text.RegularExpressions.RegexOptions.Multiline);

            using (var ms = new MemoryStream(_10MB))
            using (var bw = new EndianBinaryWriter(ms))
            {
                bw.Write(0);
                bw.Write(lines.Count);
                bw.Write(new byte[lines.Count * 8]); // 64bit

                var pointers = new long[lines.Count];
                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    if (line.English != string.Empty)
                    {
                        pointers[i] = ms.Length;
                        var value = line.English;

                        // restore format
                        var replaced2 = regex2.Replace(value, delegate (System.Text.RegularExpressions.Match m) {
                            return "//" + m.Groups[4].Value + "//";
                        });

                        value = line.ID + ' ' + replaced2;
                        bw.WriteTerminatedString((i + 1) + "_" + value, Encoding.UTF8);
                    }
                }

                bw.BaseStream.Position = 8;
                bw.Write(pointers);

                return ms.ToArray();
            }
        }
    }
}
