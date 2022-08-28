using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ExR.Format
{
    [Plugin("com.dc.regexText", "# Regex (plain text)", @"
- Mode: Extract\Repack (no optional data needed)
- Ensure Non-capturing group, check https://regex101.com/ or https://regexper.com/.
- Sample _init_.yaml (extract required, repack optional):
---
patterns:   # name=regex (Extract required)
  - Name=(?:abc)
  - Text=(?:def)
encoding: shift_jis # (default utf_8)
table: |- # [optinal, repack] char replace
  A=b
  B=c
...

")]
    class RegexText : TextFormat
    {
        readonly string keyPrefix = "_L0C_";
        Encoding _baseEncoding = null;
        string[] PatternNames;
        Regex regex0G = null;

        public override bool Init(Dictionary<string, object> dict)
        {
            if (dict.TryGetValue("encoding", out object encoding))
            {
                // shift_jis          932
                // https://docs.microsoft.com/en-us/dotnet/api/system.text.encoding.codepage?view=net-5.0
                _Encoding = Encoding.GetEncoding((string)encoding);
            }
            else
            {
                _Encoding = Encoding.UTF8; // default
            }

            if (RunMode == Mode.Repack)
            {
                if (dict.TryGetValue("table", out object table))
                {
                    _baseEncoding = _Encoding;
                    _Encoding = new StandardEncoding((string)table, _Encoding);
                }
            }
            else // Extract
            {
                if (dict.TryGetValue("patterns", out object patterns))
                {
                    var _patterns = ((List<object>)patterns).ConvertAll(x => (string)x);

                    var sb = new StringBuilder();
                    PatternNames = new string[_patterns.Count + 1];
                    for (int i = 0; i < _patterns.Count; i++)
                    {
                        var splited = _patterns[i].Split(new char[] { '=' }, 2);
                        var name = splited[0].Trim();
                        var pattern = splited[1];

                        if (pattern != string.Empty)
                        {
                            PatternNames[i + 1] = name;
                            sb.Append('(').Append(pattern).Append(")|");
                        }
                    }
                    var patternJoined = sb.ToString().TrimEnd('|');
                    Console.WriteLine("Final pattern: " + patternJoined);

                    regex0G = new Regex(patternJoined);
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        static int GetMatchIndex(GroupCollection gs)
        {
            // 0 = fullMath
            // --
            // 1 = string
            // --
            // 2 = noMatch
            // --
            // 3 = noMatch
            for (int i = 1; i < gs.Count; i++)
            {
                if (gs[i].Success)
                {
                    return i; // return 1
                }
            }

            return -1; // no way
        }

        public override List<Line> ExtractText(byte[] buf)
        {
            var lines = new List<Line>();
            var text = buf.ReadAllText(_Encoding);

            int num = 0;
            var patched = regex0G.Replace(text, m =>
            {
                var captureIndex = GetMatchIndex(m.Groups); // 1 2 3

                if (m.Value.Trim() == string.Empty)
                    return m.Value;
                else
                {
                    var id = keyPrefix + num++.ToString("X4");
                    lines.Add(new Line(id + "|" + PatternNames[captureIndex], m.Value));
                    return id;
                }
            });

            var bytes = _Encoding.GetBytes(patched);
            _PushEnd(lines, bytes);
            return lines;
        }

        public override byte[] RepackText(List<Line> lines)
        {
            int startIndex = 0;
            if (_baseEncoding != null)
            {
                var text = _baseEncoding.GetString(_PopEnd(lines));
                foreach (var line in lines)
                {
                    var english = line.English;
                    var raw = _Encoding.GetBytes(english);
                    english = _baseEncoding.GetString(raw);

                    var lineId = line.ID.Split(new char[] { '|' }, 2); // Backward compatibility, token|name
                    var token = lineId[0];
                    var pos = text.IndexOf(token, startIndex);
                    var endLen = pos + token.Length;
                    text = text.Substring(0, pos) + english + text.Substring(endLen);
                    startIndex = pos + english.Length;
                }

                return _baseEncoding.GetBytes(text);
            }
            else
            {
                var text = _Encoding.GetString(_PopEnd(lines));
                foreach (var line in lines)
                {
                    var english = line.English;

                    var lineId = line.ID.Split(new char[] { '|' }, 2);
                    var token = lineId[0];
                    var pos = text.IndexOf(token, startIndex);
                    var endLen = pos + token.Length;
                    text = text.Substring(0, pos) + english + text.Substring(endLen);
                    startIndex = pos + english.Length;
                }

                return _Encoding.GetBytes(text);
            }
        }
    }
}
