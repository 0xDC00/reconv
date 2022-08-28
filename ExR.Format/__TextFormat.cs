using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Zio;

namespace ExR.Format
{
    public abstract class TextFormat
    {
        protected static readonly int _10MB = 10485760; // A00000=10485760=10MiB

        public enum Mode
        {
            Extract,
            Repack
        }

        public abstract byte[] RepackText(List<Line> lines);

        public abstract List<Line> ExtractText(byte[] bytes);

        public virtual bool Init(Dictionary<string, object> dict) { return true; }
        //public virtual async Task<bool> InitAsync(Dictionary<string, object> dict, Mode mode) { return await Task.Run(() => Init(dict, mode)); }
        public virtual async Task<bool> InitAsync(Dictionary<string, object> dict) { return await Task.FromResult(true); }

        public virtual void End() { }
        protected Encoding _Encoding;
        public string[] Extensions { get; protected set; }

        public string CurrentFilePath { get; set; }
        public IFileSystem FsIn { get; set; }
        public IFileSystem FsOut { get; set; }

        public Mode RunMode { get; set; }
        public virtual bool DynamicCSV
        {
            get
            {
                return false;
            }
        }

#if BLAZOR
        public Microsoft.JSInterop.IJSUnmarshalledRuntime JSUn { get; set; }
        public Microsoft.JSInterop.IJSInProcessRuntime JSIn { get; set; }
#endif

        protected void _PushEnd(List<Line> lines, byte[] data)
        {
            // Sheets 50,000 characters | Excel: 32,767 characters
            // https://support.office.com/en-us/article/excel-specifications-and-limits-1672b34d-7043-467e-8e27-269d656771c3#ID0EBABAAA=Newer_versions
            var payload = System.Convert.ToBase64String(data.DeflateCompress());
            var payloads = payload.Split(32747); // 49984 | 32747
            int i = 0;
            foreach (var item in payloads)
            {
                lines.Add(new Line(item, string.Empty));
                i++;
            }
            lines.Add(new Line(i, string.Empty));
        }
        protected byte[] _PopEnd(List<Line> lines)
        {
            var sb = new StringBuilder();

            var lastLine = lines.Count - 1;
            var numChunk = int.Parse(lines[lastLine].ID);
            lines.RemoveAt(lastLine);

            for (int i = 0; i < numChunk; i++)
            {
                lastLine = lines.Count - 1;
                //sb.Append(lines[lastLine].Id);
                sb.Insert(0, lines[lastLine].ID);
                lines.RemoveAt(lastLine);
            }

            return System.Convert.FromBase64String(sb.ToString()).DeflateUncompress();
        }

        protected byte[] ReadCurrentFileData()
        {
            if (RunMode == Mode.Repack)
            {
                if (FsOut.FileExists(CurrentFilePath))
                    return FsOut.ReadAllBytes(CurrentFilePath);

                if (FsIn.FileExists(CurrentFilePath))
                    return FsIn.ReadAllBytes(CurrentFilePath);

                throw new ExceptionWithoutStackTrace($"[Output missing!] Could not find file `{CurrentFilePath}`.");
            }
            else
            {
                return FsIn.ReadAllBytes(CurrentFilePath);
            }
        }

        public class ExceptionWithoutStackTrace : Exception
        {
            public ExceptionWithoutStackTrace(string message) : base(message)
            {}

            public override string StackTrace
            {
                get { return string.Empty; }
            }
        }
    }


    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class PluginAttribute : Attribute
    {
        public string Command { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }

        public PluginAttribute(string command, string name, string decryption = "")
        {
            Command = command;
            Name = name;
            Description = decryption;
        }
    }
}
