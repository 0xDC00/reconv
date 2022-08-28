#define DEBUG_VIE
// https://github.com/greenjaed/tablelib.NET/blob/master/tablelib/TableReader.cs
// https://github.com/abbaye/WpfHexEditorControl/blob/14ba6fe7259716b32802b27ff02033524c521c35/Sources/WPFHexaEditor/Core/CharacterTable/TBLStream.cs
// TODO: refactor

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ExR.Format
{
    public class CustomEncoding : Encoding
    {
        Dictionary<char, int> CharIndex; // for fast detect block size by char
        Dictionary<char, bool> UpToLowConvert;
        int LongestHex; // maxsize for decode
        int mostHex; // UNK block size for decode

        // https://github.com/WerWolv/ImHex/issues/26
        // comment = #
        // TODO: control code, dynamic len support
        // !FA=<[Sound]>,1
        // => # Output for FA00 will be: [Sound]($00), NOT [Sound]A

        // TODO:
        // # FF is the end control code
        // /FF=[END]

        /* nếu có khóa trùng */
        // GetString - Ưu tiên khóa (hex) nằm trước (trên)
        // vd = 01=a
        //      01=b
        // GetString(1) -> sẽ trả về a

        Dictionary<string, string>[] cDecode;
        Dictionary<string, int> ControlCode;

        // GetBytes - Ưu tiên khóa (chuổi) năm sau (dưới)
        // vd: 01=a
        //     02=a
        // GetBytes("a") -> sẽ trả về 02
        // => table tiếng việt (không thể bị trùng khóa ký tự) để ở dưới
        // => nếu bị trùng <=> chuyển ký tự sang ô khác (ít khi)
        Dictionary<string, byte[]>[] cEncode;

        Regex regexUnkHex; // ($001122FF) for DF encode

        /// <summary>
        /// BIRDGE_DOTNET - text table
        /// .NET - path
        /// </summary>
        /// <param name="tbl"></param>
        public CustomEncoding(string tbl)
        {
            LongestHex = 0;
            CharIndex = new Dictionary<char, int>();
            UpToLowConvert = new Dictionary<char, bool>();

            regexUnkHex = new Regex(@"\G\(\$([0-9a-fA-F]{2})+\)", RegexOptions.Compiled); // \G instead of ^
            
            if (string.IsNullOrEmpty(tbl))
                return;

            var lines = LoadTBL(tbl);

            // nếu không có table.
            if (lines.Length == 0)
                return;

            ControlCode = new Dictionary<string, int>();

            // init decode
            LongestHex = lines.Select(x => x[0]).OrderByDescending(x => x.Length).First().Length;
            var maxSize = LongestHex + 1;
            LongestHex /= 2;
            cDecode = new Dictionary<string, string>[maxSize];
            for (int i = 0; i < maxSize; i++)
            {
                cDecode[i] = new Dictionary<string, string>();
            }

            // init encode
            var maxSizeT = lines.Select(x => x[1]).OrderByDescending(x => x.Length).First().Length + 1;
            cEncode = new Dictionary<string, byte[]>[maxSizeT];
            for (int i = 0; i < maxSizeT; i++)
            {
                cEncode[i] = new Dictionary<string, byte[]>();
            }

            foreach (var data in lines)
            {
                // 00 A
                // UPDATE DECODE
                var _hex = data[0];
                var _str = data[1];

                if (_hex[0] == '!' && _hex.Length % 2 == 1)
                {
                    // dynamic control
                    var hexcode = _hex.Substring(1);
                    var index = hexcode.Length;

                    if (cDecode.Length <= index)
                    {
                        var oldSize = cDecode.Length;
                        Array.Resize(ref cDecode, index + 1);
                        for (var i = oldSize; i < cDecode.Length; i++)
                        {
                            cDecode[i] = new Dictionary<string, string>();
                        }
                    }

                    if (cDecode[index].ContainsKey(hexcode))
                    {
                        // do nothing
                    }
                    else
                    {
                        var splited = _str.Split(',', 2, StringSplitOptions.RemoveEmptyEntries);
                        string controlName;
                        int controlLen;
                        if (splited.Length == 1)
                        {
                            // num only
                            controlLen = int.Parse(splited[0]);
                            controlName = null;
                        }
                        else if (splited.Length == 2)
                        {
                            controlName = splited[0].Substring(1, splited[0].Length - 2);
                            controlLen = int.Parse(splited[1]);

                            // update encode
                            var indexT = controlName.Length;
                            if (cEncode.Length <= indexT)
                            {
                                var oldSize = cEncode.Length;
                                Array.Resize(ref cEncode, indexT + 1);
                                for (var i = oldSize; i < cEncode.Length; i++)
                                {
                                    cEncode[i] = new Dictionary<string, byte[]>();
                                }
                            }
                            cEncode[indexT][controlName] = hexcode.HexStringToByteArray();

                            // UPDATE LONGEST TEXT
                            var _fistCharOfStr = controlName[0];
                            updateCharIndex(_fistCharOfStr, controlName.Length);
                        }
                        else
                        {
                            throw new Exception("Invalid: " + _hex + "=" + _str);
                        }
                        cDecode[index].Add(hexcode, controlName);
                        ControlCode[hexcode] = controlLen;
                    }
                }
                else
                {
                    var hex = _hex.HexStringToByteArray();
                    var index = _hex.Length;

                    if (cDecode[index].ContainsKey(_hex) == false)
                    {
                        cDecode[index].Add(_hex, _str);
                    }
                    else
                    {
                        //Program.Logger.Debug("[Decoder] Skip: " + data[0] + '=' + data[1]);
                    }

                    // UPDATE ENCODE
                    var indexT = _str.Length;
                    //if (cEncode[indexT].ContainsKey(data[1]))
                    //{
                    //    //Program.Logger.Debug("[Encoder] Update: " + data[0] + '=' + data[1]);
                    //}
                    cEncode[indexT][_str] = hex; // update or add


                    // UPDATE LONGEST TEXT
                    var _fistCharOfStr = _str[0];
                    updateCharIndex(_fistCharOfStr, _str.Length);
                }
            }

            // firstDict (most item) -> firstKey -> Length
            mostHex = cDecode.OrderByDescending(x => x.Count).First().First().Key.Length / 2;
        }

        void updateCharIndex(char _fistCharOfStr, int len)
        {
            if (CharIndex.ContainsKey(_fistCharOfStr))
            {
                if (CharIndex[_fistCharOfStr] < len)
                {
                    CharIndex[_fistCharOfStr] = len;
                }
                else
                {
                    // do nothing
                }
            }
            else
            {
                CharIndex.Add(_fistCharOfStr, len);
            }
        }

        private string[][] LoadTBL(string tbl)
        {
            string[][] lines;


            // Hỗ trợ xuống dòng trong table.
#if BLAZOR
            var tbls = tbl.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            lines = tbls
                .Select(x => x.Split(new char[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries))
                .Where(x => x.Length == 2 && x[0][0] != '#')
                .ToArray();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                // hex char
                line[1] = line[1].Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\0", "\0");
            }
#else
            if (File.Exists(tbl))
            {
                lines = File.ReadLines(tbl, Encoding.UTF8)
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
                line[1] = line[1].Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\0", "\0");
            });
#endif
            return lines;
        }

        // Add/Update decoder
        // for game have many table
        // offline only
        // TODO: giải pháp an toàn? tạo 1 đối tượng mới và inject thêm ký tự vào.
        public void UpdateDecode(string tbl)
        {
            var lines = LoadTBL(tbl);

            foreach (var data in lines)
            {
                // UPDATE DECODE
                var index = data[0].Length;
                cDecode[index][data[0]] = data[1]; // force update

                // UPDATE LONGEST TEXT
                if (CharIndex.ContainsKey(data[1][0]))
                {
                    if (CharIndex[data[1][0]] < data[1].Length)
                    {
                        CharIndex[data[1][0]] = data[1].Length;
                    }
                }
                else
                {
                    CharIndex.Add(data[1][0], data[1].Length);
                }
            }
        }

        public override string GetString(byte[] bytes)
        {
            var tempbuf = bytes.Select(b => b.ByteToString()).ToList();
            var sb = new StringBuilder(512); // faster than string+
            //string strOut;
            string hexInput;
            int hexOff = 0;

            int cursize;
            while (hexOff < tempbuf.Count)
            {
                var byteLeft = tempbuf.Count - hexOff;

                // find in table
                for (cursize = Math.Min(byteLeft, LongestHex); cursize > 0; --cursize)
                {
                    if (cDecode[cursize * 2].Count == 0)
                        continue;

                    hexInput = string.Concat(tempbuf.GetRange(hexOff, cursize));
                    if (cDecode[hexInput.Length].TryGetValue(hexInput, out var strOut))
                    {
                        // if decoded is control
                        if (ControlCode.TryGetValue(hexInput, out var byteCount))
                        {
                            if (strOut == null) // mark noName
                            {
                                sb.Append("($");
                                sb.Append(string.Concat(tempbuf.GetRange(hexOff, byteCount)));
                                sb.Append(")");
                                hexOff += byteCount;
                            }
                            else
                            {
                                sb.Append(strOut);
                                hexOff += cursize;

                                if (byteCount > 0)
                                {
                                    sb.Append("($");
                                    sb.Append(string.Concat(tempbuf.GetRange(hexOff, byteCount)));
                                    sb.Append(")");
                                    hexOff += byteCount;
                                }
                            }
                        }
                        else
                        {
                            sb.Append(strOut);
                            hexOff += cursize;
                        }
                       
                        break;
                    }
                }

                if (cursize == 0) // if not found => raw -> ($hex)
                {
                    hexInput = DecodeFallBack(tempbuf, bytes, ref hexOff, byteLeft);

                    sb.Append("($");
                    sb.Append(hexInput);
                    sb.Append(")");
                }
            }

            return sb.ToString();
        }

        public override byte[] GetBytes(string s)
        {
#if TRACE_VIE
            Console.WriteLine(s);
#endif
            var encodedData = new List<byte>();
            int strOffset = 0;
            string subInput = string.Empty; // tempbuf
            byte[] subOutput = null;
            Match regMatch;

            while (strOffset < s.Length)
            {
                // MAIN PROCESS
                // check start with unk hex block ($hex) -> raw
                regMatch = regexUnkHex.Match(s, strOffset);
                if (regMatch.Success)
                {
                    // skip ($
                    // last ) auto skip
                    subOutput = regMatch.Value.CustomHexStringToByteArray();

                    AfterEncodeChar(ref subOutput, encodedData.Count); // Slient Hill Control Padding
                    encodedData.AddRange(subOutput);
                    strOffset += regMatch.Value.Length;
                }
                else // default process
                {
                    // hack - Force Upper to Lower
                    if (CharIndex.ContainsKey(s[strOffset]) == false)
                    {
                        char replaceChar = s[strOffset];
                        if (char.IsUpper(replaceChar))
                        {
                            replaceChar = char.ToLower(replaceChar); // Ð (00D0 - 208) => lỗi; Đ (0110 - 272) => pass
                            if (CharIndex.ContainsKey(replaceChar))
                            {
#if DEBUG_VIE
                                if (!UpToLowConvert.ContainsKey(s[strOffset]))
                                {
                                    UpToLowConvert.Add(s[strOffset], true);
                                    Console.WriteLine("\n[M1] Offset: " + strOffset + ", char: " + s[strOffset] + ", uni(d): " + (int)s[strOffset]);
                                    Console.WriteLine(" -> Lower: " + replaceChar);

                                }
#endif
                            }
                            else
                            {
                                // hack - again!, remove
                                replaceChar = GetReplaceChar(replaceChar);
#if DEBUG_VIE
                                if (!UpToLowConvert.ContainsKey(s[strOffset]))
                                {
                                    UpToLowConvert.Add(s[strOffset], true);
                                    Console.WriteLine("\n[M2] Offset: " + strOffset + ", char: " + s[strOffset] + ", uni(d): " + (int)s[strOffset]);
                                    Console.WriteLine(" -> " + replaceChar);
                                }
#endif

                            }
                        }
                        else
                        {
                            // hack - again!, remove mark
                            replaceChar = GetReplaceChar(replaceChar);
#if DEBUG_VIE
                            if (!UpToLowConvert.ContainsKey(s[strOffset]))
                            {
                                UpToLowConvert.Add(s[strOffset], true);
                                Console.WriteLine("\n[M2] Offset: " + strOffset + ", char: " + s[strOffset] + ", uni(d): " + (int)s[strOffset]);
                                Console.WriteLine(" -> " + replaceChar);
                            }
#endif

                        }
                        //Program.Logger.Warning(s);
                        s = s.Replace(s[strOffset], replaceChar);
                    }

                    var longest = CharIndex[s[strOffset]];
                    if (longest + strOffset >= s.Length)
                    {
                        subInput = s.Substring(strOffset);
                        if (ReferenceEquals(subInput, s)) // subImput phải là chuổi mới
                        {
                            //var newString = String.Copy(s); // Obs
                            //var newString = new string(s.ToArray()); // Bridge.NET chưa hỗ trợ String.Copy
                            var newString = new string(s);
                            subInput = newString.Substring(strOffset); // clone dont work when same size -> TODO!
                        }
                    }
                    else
                    {
                        subInput = s.Substring(strOffset, longest);
                    }

                    var newLenght = Encode(ref subInput, ref subOutput);
                    if (newLenght == 0) // ->STOP
                    {
                        throw new Exception("Unk block: " + s.Substring(strOffset)); // unknow char
                    }

                    AfterEncodeChar(ref subOutput, encodedData.Count); // Slient Hill Control Padding
                    encodedData.AddRange(subOutput);


                    strOffset += newLenght;
                }
            }

            return encodedData.ToArray();
        }

        /// <summary>
        /// trường hợp encode sang byte[]
        /// nếu k tìm thấy ký tự trong table => xóa dấu
        /// </summary>
        /// <param name="replaceChar"></param>
        /// <returns></returns>
        private char GetReplaceChar(char replaceChar)
        {
#if TRACE_VIE
            Console.WriteLine(replaceChar);
#endif
            // current only
            if (replaceChar == 'đ')
            {
                //inputString.SetChar(strOffset, 'd');
                replaceChar = 'd';
            }
            else // only for VN char
            {
                var depack1 = replaceChar.ToString().Normalize(NormalizationForm.FormD);
                var formd = depack1.Normalize(NormalizationForm.FormD); // max len = 3

                var temp = formd.ToArray();
                Array.Resize(ref temp, temp.Length - 1); // len = 2
                var depack2 = new string(temp);
#if DEBUG_VIE
                //Console.WriteLine(" -> NFD: " + depack2);
#endif

                if (CharIndex.ContainsKey(depack2[0]) == false)
                {
                    Array.Resize(ref temp, temp.Length - 1); // len = 1
                    depack2 = new string(temp);
#if DEBUG_VIE
                    //Console.WriteLine(" -> NFD: " + depack2);
#endif
                }

                replaceChar = depack2[0];
            }
            return replaceChar;
        }

        // encode single char to hex using dictionary
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
                    pi[-1] = maxlenght; // unsafe: mono vs CLR (TODO)
                    p[maxlenght] = '\0';
                }

                pi[-1] = oriLenght; // need restore, revent CLR error
            }

            return maxlenght;
        }

        // Process control
        public virtual string DecodeFallBack(List<string> hex, byte[] bytes, ref int index, int byteLeft)
        {
            var cursize = Math.Min(byteLeft, mostHex);
            var result = string.Concat(hex.GetRange(index, cursize));
            index += cursize;
            return result;
        }

        // padding when need
        public virtual void AfterEncodeChar(ref byte[] encodedChar, int totalEncodedByte)
        { }

#region NotImplementedException
        public override int GetByteCount(char[] chars, int index, int count)
        {
            throw new NotImplementedException();
        }

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
        {
            throw new NotImplementedException();
        }

#if !BRIDGE_DOTNET
        public override int GetCharCount(byte[] bytes, int index, int count)
        {
            throw new NotImplementedException();
        }

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        public override int GetMaxCharCount(int byteCount)
        {
            throw new NotImplementedException();
        }
#endregion
    }
}
