using System.Text;
using System.Collections.Generic;
using System.Collections;

namespace ExR.Format
{
    public class Line : IEnumerable<string>
    {
        private string _id;
        private string _eng;
        private string _vie;
        private string _jap;

        private List<string> _cols = new List<string>(256)
        {
            null, // id
            null, // eng
            null, // vie
            null  // jap
        };
        //public string ID { get; set; }
        //public string English { get; set; }
        //public string Vietnamese { get; set; }
        //public string Note { get; set; }

        public IEnumerator<string> GetEnumerator()
        {
            _cols[0] = _id;
            _cols[1] = _eng;
            _cols[2] = _vie;
            _cols[3] = _jap;
            // yield return
            return _cols.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int Count()
        {
            return _cols.Count;
        }

        public void Add(string s)
        {
            _cols.Add(s);
        }

        public string ID
        {
            get { return _id; }
            set { _id = value; }
        }
        public string English
        {
            get { return _eng; }
            set { _eng = value; }
        }
        public string Vietnamese
        {
            get { return _vie; }
            set { _vie = value; }
        }
        public string Note
        {
            get { return _jap; }
            set { _jap = value; }
        }

        public string this[int key]
        {
            get
            {
                switch (key)
                {
                    case 0: return _id;
                    case 1: return _eng;
                    case 2: return _vie;
                    case 3: return _jap;
                    default:
                        if (key < _cols.Count)
                            return _cols[key];
                        else
                            throw new System.IndexOutOfRangeException();
                }
            }
            set
            {
                switch (key)
                {
                    case 0: _id = value; break;
                    case 1: _eng = value; break;
                    case 2: _vie = value; break;
                    case 3: _jap = value; break;
                    default:
                        if (key < _cols.Count)
                        {
                            _cols[key] = value;
                        }
                        else
                        {
                            var add = key - _cols.Count;
                            while (add > 0)
                            {
                                _cols.Add(string.Empty);
                                add--;
                            } // empty
                            //if (add > 0)
                            //{
                            //    _cols.AddRange(new string[add]);
                            //} // null
                            _cols.Add(value);
                        }
                        break;
                }
            }
        }

        public string this[string key]
        {
            get
            {
                switch (key.ToLower())
                {
                    case "id":
                        return _id;
                    case "eng":
                    case "src":
                    case "english":
                        return _eng;
                    case "vie":
                    case "dst":
                    case "vietnamese":
                        return _vie;
                    case "note":
                        return _jap;
                    default: throw new System.IndexOutOfRangeException();
                }
            }
            set
            {
                switch (key.ToLower())
                {
                    case "id":
                        _id = value; break;
                    case "eng":
                    case "src":
                    case "english":
                        _eng = value; break;
                    case "vie":
                    case "dst":
                    case "vietnamese":
                        _vie = value; break;
                    case "note":
                        _jap = value; break;
                    default: throw new System.IndexOutOfRangeException();
                }
            }
        }

        public static string ToId(params object[] arg)
        {
            var sb = new StringBuilder();
            sb.Append(arg[0]);
            for (int i = 1; i < arg.Length; i++)
            {
                sb.Append('|');
                sb.Append(arg[i]);
            }
            return sb.ToString();
        }

        public static string[] FromId(string s)
        {
            return s.Split('|');
        }

        public Line()
        {
            _id = string.Empty;
            _eng = string.Empty;
            _vie = string.Empty;
            _jap = string.Empty;
        }

        public Line(string eng)
        {
            _id = string.Empty;
            _eng = eng;
            _vie = string.Empty;
            _jap = string.Empty;
        }

        public Line(string id, string eng)
        {
            _id = id;
            _eng = eng;
            _vie = string.Empty;
            _jap = string.Empty;
        }

        public Line(int id, string eng)
        {
            _id = id.ToString();
            _eng = eng;
            _vie = string.Empty;
            _jap = string.Empty;
        }

        public Line(uint id, string eng)
        {

            _id = id.ToString();
            _eng = eng;
            _vie = string.Empty;
            _jap = string.Empty;
        }

        public Line(string id, string eng, string vie, string jap)
        {
            _id = id;
            _eng = eng;
            _vie = vie;
            _jap = jap;
        }


        public float TrimIdIndex()
        {
            var split = ID.Split(new char[] { '_' }, 2);
            if (split.Length == 2)
            {
                ID = split[1];
                return float.Parse(split[0]);
            }
            //_Id = string.Empty;
            return float.Parse(ID); // number only
        }

        public override string ToString()
        {
            return _id.PadRight(8) + _eng;
        }
    }
}
