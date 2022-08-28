using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ExR.Format
{
    public class StandardEncoding : Encoding
    {
        Dictionary<char, int> CharIndex; // for fast detect block size by char
        Dictionary<string, byte[]>[] cEncode;
        Regex regexUnkHex; // ($001122FF) for DF encode
        Encoding _encoding;

        // table, encode replace a -> %
        // a=%


        // 00=A
        // 01=B
        // !FA=<[Sound]>,1
        // # Output for FA00 will be: [Sound]($00), NOT [Sound]A
        int LongestHexControl; // maxsize for decode control
        class ControlInfo
        {
            public string Name { get; set; }
            public int ByteCount { get; set; }
        }
        Dictionary<string, ControlInfo>[] cDecode;

        public StandardEncoding(string tbl, Encoding encoding)
        {
            _encoding = encoding;

            // encode str => byte
            regexUnkHex = new Regex(@"\G\(\$([0-9a-fA-F]{2})+\)", RegexOptions.Compiled); // \G instead of ^
            CharIndex = new Dictionary<char, int>();

            if (string.IsNullOrEmpty(tbl))
                return;

            // nếu không có file table thì table là dạng text.
            string[][] lines;
            // thêm 3 dòng cho breakline (không cần!, khi repack phải đưa về chuẩn)
            /*
            var lenght = lines.Length;
            Array.Resize(ref lines, lenght + 3);
            lines[lenght++] = new string[] { "[\\r]", "\r" }; // [\r]=\r
            lines[lenght++] = new string[] { "[\\n]", "\n" }; // [\n]=\n
            lines[lenght++] = new string[] { "[\\t]", "\t" }; // [\t]=\t
            */

            // mục đích table chỉ dùng cho replace -> không cần hỗ trợ: \r\n\0\t =>
            // Không!,
            // Hỗ trợ xuống dòng trong table.

#if BLAZOR
            var tbls = tbl.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            lines = tbls
                .Select(x => x.Split(new char[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries))
                .Where(x => x.Length == 2 && x[0][0] != '#')
                .ToArray();

            for (int i = 0; i < lines.Length; i++)
            {
                //lines[i][1] = lines[i][1].Replace("\\r\\n", Environment.NewLine);
                var line = lines[i];
                line[0] = line[0].Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\0", "\0");
                line[1] = line[1].Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\0", "\0");
            }
#else
            if (System.IO.File.Exists(tbl))
            {
                lines = System.IO.File.ReadLines(tbl, Encoding.UTF8)
                    .Select(x => x.Split(new char[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries))
                    .Where(x => x.Length == 2 && x[0][0] != '#')
                    .ToArray();
            }
            else
            {
                var tbls = tbl.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                lines = tbls
                    .Select(x => x.Split(new char[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries))
                    .Where(x => x.Length == 2 && x[0][0] != '#')
                    .ToArray();
            }

            System.Threading.Tasks.Parallel.For(0, lines.Length, i =>
            {
                var line = lines[i];
                //lines[i][1] = lines[i][1].Replace("\\r\\n", Environment.NewLine);
                line[0] = line[0].Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\0", "\0");
                line[1] = line[1].Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\0", "\0");
            });
#endif
            // nếu không có table.
            if (lines.Length == 0)
                return;

            const int indexHex = 1;
            const int indexText = 0;

            // init decode
            var hexontrols = lines.Select(x => x[0]).Where(x => x[0] == '!' && x.Length % 2 == 1)
                .OrderByDescending(x => x.Length).FirstOrDefault();
            if (hexontrols != null)
            {
                LongestHexControl = hexontrols.Length;
                LongestHexControl -= 1; // exclude !
                var maxSize = LongestHexControl + 1;
                LongestHexControl /= 2;
                cDecode = new Dictionary<string, ControlInfo>[maxSize];
                for (int i = 0; i < maxSize; i++)
                {
                    cDecode[i] = new Dictionary<string, ControlInfo>();
                }
            }
            else
            {
                LongestHexControl = 0;
            }
            

            // init encode
            var maxSizeT = lines.Select(x => x[indexText]).OrderByDescending(x => x.Length).First().Length + 1;
            cEncode = new Dictionary<string, byte[]>[maxSizeT];
            for (int i = 0; i < maxSizeT; i++)
            {
                cEncode[i] = new Dictionary<string, byte[]>();
            }

            foreach (var data in lines)
            {
                var _strOri = data[indexHex];
                var _strNew = data[indexText]; // new char need replace to ori

                if (_strNew[0] == '!' && _strNew.Length % 2 == 1)
                {
                    var hexcode = _strNew.Substring(1);
                    var index = hexcode.Length;
                    if (cDecode[index].ContainsKey(hexcode) == false)
                    {
                        // !0011=<[$SOUND]>,1
                        var controlInfo = new ControlInfo();
                        var splited = _strOri.Split(',', 2, StringSplitOptions.RemoveEmptyEntries);
                        if (splited.Length == 1)
                        {
                            // num only
                            controlInfo.ByteCount = int.Parse(splited[0]);
                        }
                        else if (splited.Length == 2)
                        {
                            controlInfo.Name = splited[0].Substring(1, splited[0].Length - 2);
                            controlInfo.ByteCount = int.Parse(splited[1]);

                            // update encode
                            var indexT = controlInfo.Name.Length;
                            if (cEncode.Length <= indexT)
                            {
                                var oldSize = cEncode.Length;
                                Array.Resize(ref cEncode, indexT + 1);
                                for (var i=oldSize; i<cEncode.Length; i++)
                                {
                                    cEncode[i] = new Dictionary<string, byte[]>();
                                }
                            }

                            cEncode[indexT][controlInfo.Name] = hexcode.HexStringToByteArray();
                            updateCharIndex(controlInfo.Name[0], controlInfo.Name.Length);
                        }
                        else
                        {
                            throw new Exception("Invalid: " + _strOri + "=" + _strNew);
                        }
                        cDecode[index].Add(hexcode, controlInfo);
                    }
                    else
                    {
                        //Program.Logger.Debug("[Decoder] Skip: " + data[0] + '=' + data[1]);
                    }
                }
                else
                {
                    var hex = _encoding.GetBytes(_strOri);

                    // UPDATE ENCODE
                    var indexT = _strNew.Length;
                    if (cEncode[indexT].ContainsKey(_strNew))
                    {
                        Console.WriteLine("[Encoder] Update: " + _strOri + '=' + _strNew);
                    }
                    cEncode[indexT][_strNew] = hex; // update or add


                    // UPDATE LONGEST TEXT
                    var _fistCharOfStr = _strNew[0];
                    updateCharIndex(_fistCharOfStr, _strNew.Length);
                }
            }
        }

        void updateCharIndex(char _fistCharOfStr, int len)
        {
            if (CharIndex.ContainsKey(_fistCharOfStr))
            {
                if (CharIndex[_fistCharOfStr] < len)
                {
                    CharIndex[_fistCharOfStr] = len;
                }
            }
            else
            {
                CharIndex.Add(_fistCharOfStr, len); // first char, max lenght
            }
        }

        public override byte[] GetBytes(string s)
        {
            var encodedData = new List<byte>();
            int strOffset = 0;
            string subInput = string.Empty; // tempbuf
            byte[] subOutput = null;
            var inputStringArr = s.ToArray();
            Match regMatch;

            while (strOffset < s.Length)
            {
                regMatch = regexUnkHex.Match(s, strOffset);
                if (regMatch.Success)
                {
                    subOutput = regMatch.Value.CustomHexStringToByteArray();
                    encodedData.AddRange(subOutput);
                    strOffset += regMatch.Value.Length;
                    continue;
                }

                // MAIN PROCESS
                if (CharIndex.TryGetValue(s[strOffset], out var longest))
                {
                    // check char and to lower
                    longest = CharIndex[s[strOffset]];
                    if (longest + strOffset >= s.Length)
                    {
                        subInput = s.Substring(strOffset);
                        if (ReferenceEquals(subInput, s))
                        {
                            //var newString = String.Copy(s); // OBs
                            //var newString = new string(s.ToArray()); // Bridge.NET chưa hỗ trợ String.Copy
                            var newString = new string(s);
                            subInput = newString.Substring(strOffset);
                        }
                    }
                    else
                    {
                        subInput = s.Substring(strOffset, longest);
                    }

                    var newLenght = Encode(ref subInput, ref subOutput); // input will change => affect newString
                    if (newLenght == 0) // ->STOP
                    {
                        // throw new Exception("Unk block: " + inputString.Substring(strOffset)); // unknow char
                        subOutput = _encoding.GetBytes(inputStringArr, strOffset, 1); // UTF8
                        encodedData.AddRange(subOutput);
                        strOffset++;
                    }
                    else
                    {
                        // found
                        encodedData.AddRange(subOutput);
                        strOffset += newLenght;
                    }
                }
                else // no need replace -> seek 1 char
                {
                    subOutput = _encoding.GetBytes(inputStringArr, strOffset, 1);
                    encodedData.AddRange(subOutput);
                    strOffset++;
                }

            }

            return encodedData.ToArray();
        }

        // not support decode back
        // or only 1->1 ? all? (use case?)
        // or only support control Table
        public override string GetString(byte[] bytes)
        {
            // nếu k có load table thì dùng base để encode
            if (cEncode == null)
                return _encoding.GetString(bytes);

            //return GetStringRv(bytes);

            /* If no control => reverse back?*/
            // /FF=[END]
            // huffman (bit)
            // %010=A
            // %1101=F
            var tempbuf = bytes.Select(b => b.ByteToString()).ToList();
            var sb = new StringBuilder(512); // faster than string+
            //string strOut;
            string hexInput;
            int hexOff = 0;
            int cursize;
            using (var ms = new System.IO.MemoryStream(bytes))
            using (var br = new System.IO.BinaryReader(ms))
            {
                while (hexOff < tempbuf.Count)
                {
                    var byteLeft = tempbuf.Count - hexOff;
                    for (cursize = Math.Min(byteLeft, LongestHexControl); cursize > 0; --cursize)
                    {
                        // bo qua neu k co tu dien
                        if (cDecode[cursize * 2].Count == 0)
                            continue;

                        // kiem tra trong tu dien
                        hexInput = string.Concat(tempbuf.GetRange(hexOff, cursize));
                        if (cDecode[hexInput.Length].TryGetValue(hexInput, out var strOut))
                        {
                            if (string.IsNullOrEmpty(strOut.Name))
                            {
                                // noname -> size = full block
                                sb.Append("($");
                                sb.Append(string.Concat(tempbuf.GetRange(hexOff, strOut.ByteCount)));
                                sb.Append(")");
                                hexOff += strOut.ByteCount;
                            }
                            else
                            {
                                // name -> size = part2
                                sb.Append(strOut.Name);
                                hexOff += cursize;

                                if (strOut.ByteCount > 0)
                                {
                                    sb.Append("($");
                                    sb.Append(string.Concat(tempbuf.GetRange(hexOff, strOut.ByteCount)));
                                    sb.Append(")");
                                    hexOff += strOut.ByteCount;
                                }
                            }
                            break;
                        }
                    }

                    if (cursize == 0) // if not found => pick 1 char
                    {
                        br.BaseStream.Position = hexOff;
                        sb.Append(br.ReadChar());
                        hexOff = (int)br.BaseStream.Position;
                    }
                }
            }
            
            return sb.ToString();

            
        }

        // decode replace back
        //string GetStringRv(byte[] bytes)
        //{
        //    var tempbuf = bytes.Select(b => b.ByteToString()).ToList();
        //    var sb = new StringBuilder(512); // faster than string+
        //    //string strOut;
        //    string hexInput;
        //    int hexOff = 0;

        //    int cursize;

        //    while (hexOff < tempbuf.Count)
        //    {
        //        var byteLeft = tempbuf.Count - hexOff;

        //        // check control code
        //        // loop in control dict vs 


        //        // find in table
        //        for (cursize = Math.Min(byteLeft, 2); cursize > 0; --cursize)
        //        {
        //            if (cEncode[cursize * 2].Count == 0)
        //                continue;

        //            hexInput = string.Concat(tempbuf.GetRange(hexOff, cursize));
        //            hexInput = _encoding.GetString(hexInput.HexStringToByteArray());
        //            if (cEncode[hexInput.Length].TryGetValue(hexInput, out var strOut))
        //            {
        //                sb.Append(_encoding.GetString(strOut));
        //                hexOff += cursize;
        //                break;
        //            }
        //        }

        //        if (cursize == 0) // if not found => raw -> ($hex)
        //        {
        //            sb.Append(_encoding.GetString(bytes));
        //            hexOff += bytes.Length;
        //        }
        //    }

        //    return sb.ToString();
        //}

        // encode1 char to hex using dict
        unsafe int Encode(ref string input, ref byte[] hex)
        {
            int maxlenght = input.Length;
            var oriLenght = maxlenght;

            fixed (char* p = input)
            {
                int* pi = (int*)p;

                // MAIN PROCESS
                while (cEncode[maxlenght].TryGetValue(input, out hex) == false)
                {
                    do
                    {
                        maxlenght--;
                        if (maxlenght == 0)
                        {
                            // end table -> not found
                            return maxlenght; // return -1, hex=null
                        }
                    }
                    while (cEncode[maxlenght].Count == 0); // seek

                    // resize hex to table
                    pi[-1] = maxlenght; // unsafe: mono vs CLR
                    p[maxlenght] = '\0';
                }

                pi[-1] = oriLenght; // need restore, revent CLR error
            }

            return maxlenght;
        }

#region NotImplementedException
        public override int GetByteCount(char[] chars, int index, int count)
        {
            return _encoding.GetByteCount(chars, index, count);
            //throw new NotImplementedException();
        }

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
        {
            return _encoding.GetBytes(chars, charIndex, charCount, bytes, byteIndex);
            //throw new NotImplementedException();
        }

#if !BRIDGE_DOTNET
        public override int GetCharCount(byte[] bytes, int index, int count)
        {
            return _encoding.GetCharCount(bytes, index, count);
            //throw new NotImplementedException();
        }

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
        {
            return _encoding.GetChars(bytes, byteIndex, byteCount, chars, charIndex);
            //throw new NotImplementedException();
        }
#endif

#if BRIDGE_DOTNET
        protected override byte[] Encode(string s, byte[] outputBytes, int outputIndex, out int writtenBytes)
        {
            throw new NotImplementedException();
        }

        protected override string Decode(byte[] bytes, int index, int count, char[] chars, int charIndex)
        {
            throw new NotImplementedException();
        }
#endif

        public override int GetMaxByteCount(int charCount)
        {
            return _encoding.GetMaxByteCount(charCount);
        }

        public override int GetMaxCharCount(int byteCount)
        {
            return _encoding.GetMaxCharCount(byteCount);
        }
#endregion
    }
}
