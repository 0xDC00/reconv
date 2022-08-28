// https://github.com/mbystryantsev/consolgames-tools/blob/master/legacy/projects/Silent%20Hill%202/tables/english.tbl

using BufLib.Common.Compression.Nintendo;
using BufLib.Common.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExR.Format
{
    [Plugin("com.dc.pz2", "Project Zero 2: Wii Edition (~Shinku no Chou~) [SL2P01] (msg.objb, .dol)", @"
Extract\Repack
_init_.yaml (required)
---
table: |-
  00= 
  01=A
  02=B
  03=C
  04=D
  05=E
  06=F
...

Fatal Frame II: Crimson Butterfly
")]
    class ProjectZero2 : TextFormat
    {
        public override bool Init(Dictionary<string, object> dict)
        {
            dict.TryGetValue("table", out var tableStr);
            if (tableStr == null)
            {
                if (RunMode == Mode.Extract)
                {
                    _Encoding = new CustomEncoding(TABLE);
                    return true;
                }
                else
                {
                    Console.WriteLine("We need `_init_.yaml`! (TABLE)");
                    Console.WriteLine(TABLE);
                    return false;
                }

            }
            else
            {
                _Encoding = new CustomEncoding((string)tableStr);
                return true;
            }
        }

        public class OffsetInfo
        {
            public int IndexPointer { get; set; } // trong bảng pointer table
            public int IndexData { get; set; } // trong phần dữ liệu
            public int Size { get; set; }

            public OffsetInfo(int index, int size)
            {
                IndexPointer = index;
                Size = size;
            }
        }

        public override List<Line> ExtractText(byte[] buf)
        {
            // TODO: check lz11 and decompress

            using (var ms = new MemoryStream(buf))
            using (var br = new EndianBinaryReader(ms))
            {
                br.Endianness = Endian.BigEndian; // Wii
                //var peak = br.ReadUInt32();
                //br.BaseStream.Position -= 4;
                //if (peak > 0x01000000)
                //    br.Endianness = Endian.LittleEndian; // PS2


                var target = Path.GetExtension(CurrentFilePath).ToLower();
                if (target == ".dol")
                {
                    return ExtractDolChunk(br);
                }


                /* Đọc offset từng block một */
                var dictTextOffsetIndex = new Dictionary<int, OffsetInfo>(); // offset -> (index, size)
                int indexTextBlock = 0;

                // giả sử file chứa toàn pointer
                int offsetEndOfFilePointerTable = (int)br.BaseStream.Length;

                // lặp cho đến cuối
                while (br.BaseStream.Position < offsetEndOfFilePointerTable)
                {
                    int offsetTextBlock = br.ReadInt32();
                    dictTextOffsetIndex[offsetTextBlock] = new OffsetInfo(indexTextBlock++, 0);

                    // giá trị pointer nhỏ nhất chính là offset cuối của pointer table.
                    if (offsetTextBlock < offsetEndOfFilePointerTable)
                    {
                        offsetEndOfFilePointerTable = offsetTextBlock;
                    }
                }

                /* tính size cho mỗi block text */
                var sizeTextBlocks = new int[dictTextOffsetIndex.Count];
                var dictTextOffsetIndexSorted = dictTextOffsetIndex.OrderBy(x => x.Key).ToList();
                dictTextOffsetIndexSorted.Add(new KeyValuePair<int, OffsetInfo>((int)br.BaseStream.Length, null)); // phần tử cuối để tính size cho block cuối
                for (int i = 0; i < dictTextOffsetIndex.Count; i++)
                {
                    var current = dictTextOffsetIndexSorted[i];
                    var next = dictTextOffsetIndexSorted[i + 1];
                    current.Value.Size = next.Key - current.Key;
                    current.Value.IndexData = i;

                    dictTextOffsetIndex[current.Key] = current.Value;
                }

                /* extract từng file text */
                var lines = new List<Line>();
                lines.Add(new Line(dictTextOffsetIndex.Count, string.Empty)); // lưu để biết bên trong có bao nhiêu file
                foreach (var offsetText in dictTextOffsetIndex)
                {
                    br.BaseStream.Position = offsetText.Key;

                    // tính số pointer
                    var firstPointer = br.ReadInt32();
                    br.BaseStream.Position -= 4; // peak
                    var numPointer = (firstPointer - (int)br.BaseStream.Position) / 4;
                    var numPointerF = numPointer + 1;

                    // đọc pointer table
                    var pointers = br.ReadInt32s(numPointerF);
                    pointers[numPointer] = offsetText.Value.Size + offsetText.Key; // offset EOF, để tính size cho dòng cuối.
                                                                                   //Array.Sort(pointers); // PS2

                    lines.Add(new Line(offsetText.Value.IndexPointer.ToString() + '|'
                        + offsetText.Value.IndexData.ToString() + '|'
                        + numPointer.ToString(),
                        string.Empty)); // lưu để repack riêng 1 block
                    for (int i = 0; i < numPointer; i++)
                    {
                        var current = pointers[i];
                        var lenght = pointers[i + 1] - current;

                        if (lenght == 0)
                        {
                            lines.Add(new Line("_NULL_"));
                            continue;
                        }

                        br.BaseStream.Position = current;
                        var raw = br.ReadBytes(lenght);
                        if (raw[lenght - 1] != 0xFF) // FF = \0, tested luôn là \0
                            Console.WriteLine(raw[lenght - 1].ByteToString());

                        var line = _Encoding.GetString(raw).TrimEnd('\0');
                        Console.WriteLine(line);
                        lines.Add(new Line(line));
                    }
                }

                return lines;
            }
        }

        public override byte[] RepackText(List<Line> lines)
        {
            var target = Path.GetExtension(CurrentFilePath).ToLower();

            if (target == ".dol")
            {
                return RepackDolChunk(lines);
            }

            int index = 0;
            var numFile = int.Parse(lines[index++].ID);
            var filePointers = new int[numFile];

            using (var ms = new MemoryStream(_10MB))
            using (var bw = new EndianBinaryWriter(ms, Endian.BigEndian))
            {
                bw.Write(new byte[numFile * 4]); // ghi file pointer table rỗng.

                for (int i = 0; i < numFile; i++)
                {
                    int basePointer = (int)bw.BaseStream.Position;
                    filePointers[i] = basePointer;

                    // tách line
                    var fields = lines[index++].ID.Split('|');

                    // TODO: xắp xếp trật tự pointer & data như gốc (không cần thiết)
                    //var indexPointer = int.Parse(fields[0]);
                    //var indexData = int.Parse(fields[1]);
                    var numLine = int.Parse(fields[2]);

                    var subLines = lines.GetRange(index, numLine);
                    index += numLine;
                    if (subLines.Count != numLine)
                        throw new Exception("Line mismatch.");

                    // repack
                    var textBlock = RepackBlock(subLines, basePointer);
                    bw.Write(textBlock);
                }

                // cập nhật file pointer table
                bw.BaseStream.Position = 0;
                for (int i = 0; i < filePointers.Length; i++)
                {
                    bw.Write(filePointers[i]);
                }

                return BufLib.Common.Compression.Nintendo.Nintendo.Compress(ms.ToArray(), Method.LZ11);
                //return LZ11.Compress(ms.ToArray());
            }
        }

        byte[] RepackBlock(List<Line> lines, int fileBasePointer)
        {
            using (var ms = new MemoryStream(_10MB))
            using (var bw = new EndianBinaryWriter(ms, Endian.BigEndian))
            {
                int basePointer = lines.Count * 4;
                int currentPointer = basePointer + fileBasePointer; // <=> EOF

                var bytes = new List<byte>();
                for (int i = 0; i < lines.Count; i++)
                {
                    bw.Write(currentPointer);

                    var raw = _Encoding.GetBytes(lines[i].English + '\0');
                    bytes.AddRange(raw);

                    currentPointer += raw.Length;
                }
                bw.Write(bytes.ToArray());
                bw.Write((byte)0xFF);

                return ms.ToArray();
            }
        }

        List<Line> ExtractDolChunk(EndianBinaryReader br)
        {
            br.BaseStream.Position += 4;

            var lines = new List<Line>();
            var firstPointer = br.ReadInt32();
            br.BaseStream.Position -= 4; // peak
            var numPointer = (firstPointer - (int)br.BaseStream.Position) / 4;

            var pointers = br.ReadInt32s(numPointer);

            for (int i = 0; i < pointers.Length; i++)
            {
                br.BaseStream.Position = pointers[i];
                var raw = br.ReadTerminatedArray(0xFF);
                var line = _Encoding.GetString(raw).TrimEnd('\0');
                lines.Add(new Line(line));
            }

            return lines;
        }

        byte[] RepackDolChunk(List<Line> lines)
        {
            using (var ms = new MemoryStream(_10MB))
            using (var bw = new EndianBinaryWriter(ms, Endian.BigEndian))
            {
                bw.Write(4);
                bw.Write(RepackBlock(lines, 4));

                var result = ms.ToArray();
                if (result.Length > 0x313)
                    Console.WriteLine("[W] NewSize > 0x313");
                return result;
            }
        }

        internal class PZ2_Encoding : CustomEncoding
        {
            public PZ2_Encoding(string tbl) : base(tbl) { }

            public override string DecodeFallBack(List<string> hex, byte[] bytes, ref int index, int byteLeft)
            {
                var magic = bytes[index];
                if (magic == 0xFD || magic == 0xFC)
                {
                    var cursize = 2;
                    var hexInput = string.Concat(hex.GetRange(index, cursize));
                    index += cursize;
                    return hexInput;
                }
                else
                {
                    return base.DecodeFallBack(hex, bytes, ref index, byteLeft);
                }
            }
        }

        const string TABLE = @"00= 
01=A
02=B
03=C
04=D
05=E
06=F
07=G
08=H
09=I
0A=J
0B=K
0C=L
0D=M
0E=N
0F=O
10=P
11=Q
12=R
13=S
14=T
15=U
16=V
17=W
18=X
19=Y
1A=Z
1B=a
1C=b
1D=c
1E=d
1F=e
20=f
21=g
22=h
23=i
24=j
25=k
26=l
27=m
28=n
29=o
2A=p
2B=q
2C=r
2D=s
2E=t
2F=u
30=v
31=w
32=x
33=y
34=z
35={0}
36={1}
37={2}
38={3}
39={4}
3A={5}
3B={6}
3C={7}
3D={8}
3E={9}
3F=0
40=1
41=2
42=3
43=4
44=5
45=6
46=7
47=8
48=9
49={+}
FD304AFD01={A}
4B={B}
4C={-}
4D=(
4E=)
4F=,
50=?
51=!
52=/
53=
54=:
55=*
56=~
57=-
58='
59=.
5A=
5B=""
5C={jpChar_1}
5D={jpChar_2}
5E={jpChar_3}
5F={jpChar_4}
60={jpChar_5}
61={jpChar_6}
62={jpChar_7}
63={jpChar_8}
64={jpChar_9}
65={jpChar_10}
66={jpChar_11}
67={jpChar_12}
68={jpChar_13}
69={jpChar_14}
6A={jpChar_15}
6B={jpChar_16}
6C={jpChar_17}
6D=&
6E={II}
6F={``}
70={!?}
71=À
72=Á
73=Â
74={A:}
75=È
76=É
77=Ê
78={E:}
79=Ì
7A=Í
7B={I^}
7C={I:}
7D=Ò
7E=Ó
7F=Ô
80={O:}
81=Ù
82=Ú
83={U^}
84={U:}
85={N8}
86={Beta}
87={CE}
88={Ch}
89={Il}
8A=à
8B=á
8C=â
8D={a:}
8E=è
8F=é
90=ê
91={e:}
92=ì
93=í
94={i^}
95={i:}
96=ò
97=ó
98=ô
99={o:}
9A=ù
9B=ú
9C={u^}
9D={u:}
9E={rr}
9F={(}
A0={)}
A1={!reverse}
A2={?reverse}
A3={...center}
A4={;}
A5={|^}
A6={_|}
A7={/check}
A8={X}
A9={pts}
AA={c3}
AB={0up}
AC={ae}
AD={circle}
AE={=}
AF={,,}
B0={copyright}
B1={n~}
B2={C3}
B3={+.2}
B4={moNgoacKep}
B5={dongNgoacKep}
B6={N~}
B7={<<}
B8={>>}
B9={aup}
BA={#up}
BB={%}
BC={[}
BD={]}
BE={-.2}
BF={0up2}
C0={-.3}
C1={@}
C2={~}
C3={|}
C4={>}
C5={<}
C6={#}
C7={^}
C8=─
C9={infinity}
// end 0->C9 = CA = 202 <=> 21*9 + 13 = 189 + 13 = 202
// VWF im dol contain D0 chars -> D0 - CA = 6 (odd)
// CA->CF blank?
CA=
CB=
CC=
CD=
CE=
CF=
// CF end , F? -> control
FB=\n[>]\n
FE=\n
FF=\0
FB=\n[>]";
    }
}
