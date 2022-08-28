#if BLAZOR
using Microsoft.JSInterop;
#endif
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zio;

namespace ExR.Format
{
    [Plugin("com.dc.json2xlsx", "! JSON -> XLSX", @"
- Repack: json(s) -> xlsx;
- Extract: xlsx -> json(s)

Note:
- XLSX, any.
- JSON, array obj, example:
[
  {
    ""ID"": ""1"",
    ""English"": ""foo""
  },
  {
    ""ID"": ""2"",
    ""English"": ""bar""
  }
]
")]
    class A_json2xlsx : TextFormat
    {
        // TODO: https://github.com/json5/json5
        public override async Task<bool> InitAsync(Dictionary<string, object> dict)
        {
#if BLAZOR
            var files = FsOut.EnumerateFiles(UPath.Root, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                FsOut.DeleteFile(file);
            }
#endif

            if (RunMode == Mode.Repack)
            {
                Console.WriteLine("JSON(s) -> XLSX...");
                await Repack(FsIn, FsOut);
            }
            else
            {
                Console.WriteLine("XLSX -> JSON(s)...");
                Extract(FsIn, FsOut);
            }
            Console.WriteLine();
            throw new ExceptionWithoutStackTrace("json2xlsx Done!\n");
        }

        private async Task Repack(IFileSystem fsIn, IFileSystem fsOut)
        {
            // TODO: TOCP sheetName->Path
            var dict = new Dictionary<string, string>();
            var files = fsIn.EnumerateFiles(UPath.Root, "*.json", SearchOption.AllDirectories);
            var sheetNames = new HashSet<string>();

#if BLAZOR
            using (var wb = JSUn.InvokeUnmarshalled<IJSUnmarshalledObjectReference>("CreateXlsx"))
            {
                int index = 0;
                foreach (var file in files)
                {
                    index++;
                    var rel = file.ToRelative().FullName;

                    var sheetName = A_csv2xlsx.CreateSheetName(sheetNames, rel, index);                    
                    dict.Add(sheetName, rel);

                    Console.WriteLine(sheetName.PadLeft(31) + ' ' + rel);
                    await Task.Delay(2);

                    var str = fsIn.ReadAllText(file);

                    wb.InvokeVoid("ImportJson", str, sheetName);
                }

                var tocCsv = A_csv2xlsx.CreateTocCsv(dict);
                var tocName = new A_csv2xlsx.InteropStruct()
                {
                    Name = "TOC_IDX"
                };
                _ = wb.InvokeUnmarshalled<byte[], A_csv2xlsx.InteropStruct, bool>("ImportCsvUn", Encoding.UTF8.GetBytes(tocCsv), tocName);

                //var base64 = wb.Invoke<string>("ToBase64");
                //var bytes = Convert.FromBase64String(base64);

                var bytes = wb.Invoke<byte[]>("ToArray");

                fsOut.WriteAllBytes("/_PACK_.xlsx", bytes);
            }
#else
            Console.WriteLine("TODO...");
#endif
        }

        private void Extract(IFileSystem fsIn, IFileSystem fsOut)
        {
            var files = fsIn.EnumerateFiles(UPath.Root, "*.xlsx", SearchOption.AllDirectories);
            foreach (var file in files)
            {
#if BLAZOR
                var raw = fsIn.ReadAllBytes(file);
                using (var xlsx = JSUn.InvokeUnmarshalled<byte[], IJSUnmarshalledObjectReference>("CreateXlsx", raw))
                {
                    var pathAndData = xlsx.Invoke<string[]>("ExportJson");
                    for (int i = 0; i < pathAndData.Length; i += 2)
                    {
                        var _path = pathAndData[i];
                        _path = Path.ChangeExtension(_path, ".json"); // TODO: skip _init_.yaml
                        var path = ((UPath)_path).ToAbsolute();
                        var csv = pathAndData[i + 1];

                        var pathDir = path.GetDirectory();
                        if (pathDir != UPath.Root)
                        {
                            fsOut.CreateDirectory(pathDir);
                        }
                        fsOut.WriteAllText(path, csv);
                    }
                }
#else
                Console.WriteLine("TODO...");
#endif
            }
        }

        public override List<Line> ExtractText(byte[] bytes)
        {
            return null;
        }

        public override byte[] RepackText(List<Line> lines)
        {
            return null;
        }
    }
}
