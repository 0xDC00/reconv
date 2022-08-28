#if !BLAZOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Zio;
using ExR.Format;
using Priority_Queue;
using Zio.FileSystems;
using OfficeOpenXml.FormulaParsing.ExcelUtilities;
using System.Reflection;
using OfficeOpenXml;

namespace ExR
{
    public partial class TextConv
    {
        //public async Task<MemoryStream> RepackXlsx(Stream xlsx, Stream zipOut_Payload)
        //{
        //    var memIn = await MountXlsx2Csv_EPPlus(xlsx);

        //    var memOut = new MemoryFileSystem();
        //    if (zipOut_Payload != null && zipOut_Payload.Length > 0)
        //    {
        //        memOut.ImportZip(zipOut_Payload); // importPayload
        //    }

        //    // https://github.com/ExcelDataReader/ExcelDataReader
        //    // Auto-detect format, supports:
        //    //  - Binary Excel files (2.0-2003 format; *.xls)
        //    //  - OpenXml Excel files (2007 format; *.xlsx, *.xlsb)
        //    await Repack(memIn, memOut);
        //    var ms = new MemoryStream();
        //    memOut.ExportZip(ms);
        //    return ms;
        //}

        public async Task RepackXlsx(Stream xlsx, string outPath)
        {
            var memIn = await MountXlsx2Csv_EPPlus(xlsx);

            var pyOut = new PhysicalFileSystem();
            var _outPath = pyOut.ConvertPathFromInternal(outPath);
            pyOut.CreateDirectory(_outPath);
            var fsOut = new SubFileSystem(pyOut, _outPath);

            await Repack(memIn, fsOut);
        }

        // private

        private static string GetSheetNameFromPercentFormula(string formula)
        {
            //Logger.Warning(formula);
            var start = formula.IndexOf("LEN(") + 4;
            var len = formula.IndexOf("!C2") - start; // end - start
            var result = formula.Substring(start, len).Trim('\'');
            return result;
        }

        private static void TryGetInitYaml(ExcelPackage p, IFileSystem memIn)
        {
            var _init_Sheet = p.Workbook.Worksheets["_init_.yaml"];
            if (_init_Sheet == null)
            {
                var tbl = p.Workbook.Worksheets["TABLE"];
                if (tbl != null)
                {
                    // TABLE => _init_.yaml
                    var sb = new StringBuilder();
                    sb.AppendLine("---");
                    sb.AppendLine("table: |-");
                    var _table = string.Empty;
                    var cellA1 = tbl.Cells[tbl.Dimension.Start.Row, 1].Text; // null value but text empty
                    var cellB1 = tbl.Cells[tbl.Dimension.Start.Row, 2].Text;
                    if (cellA1 != string.Empty)
                    {
                        _table = cellA1;
                    }
                    if (cellB1 != string.Empty)
                    {
                        _table = _table + "\n" + cellB1;
                    }
                    if (_table != string.Empty)
                    {
                        // convert to yaml str
                        var splited = _table.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i < splited.Length; i++)
                        {
                            sb.AppendLine("  " + splited[i]);
                        }
                        sb.AppendLine("...");

                        memIn.WriteAllText(INIT_FILE_PATH, sb.ToString());
                    }
                }
            }
            else
            {
                var inf = _init_Sheet.Cells[_init_Sheet.Dimension.Start.Row, 1].Text;
                if (inf != string.Empty)
                    memIn.WriteAllText(INIT_FILE_PATH, inf);
            }
        }

        private async Task<IFileSystem> MountXlsx2Csv_EPPlus(Stream xlsx)
        {
            Log.Info("Reading sheets...");
            await Task.Delay(2);

            var memIn = new MemoryFileSystem();
            using (var p = new ExcelPackage(xlsx))
            {
                //p.Workbook.CalcMode = ExcelCalcMode.Manual;
                // TABLE and _init_.yaml
                TryGetInitYaml(p, memIn);

                var toc = p.Workbook.Worksheets["TOC"];

                for (var rowNum = toc.Dimension.Start.Row + 1; rowNum <= toc.Dimension.End.Row; rowNum++)
                {
                    var curPath = new UPath(Path.ChangeExtension(toc.Cells[rowNum, 1].Text, null)).ToAbsolute(); // local path
                    if (curPath == string.Empty)
                        continue; // skip empty row

                    double cellPercentValue;
                    string sheetname = string.Empty;
                    try
                    {
                        var cellPercent = toc.Cells[rowNum, 3];
                        var link = toc.Cells[rowNum, 1].Hyperlink;
                        if (link != null && link is ExcelHyperLink)
                        {
                            var adr = ((ExcelHyperLink)link).ReferenceAddress;
                            var bsr = new ExcelAddressBase(adr, p, null);
                            object _WorkSheetName = bsr.GetType()
                                .GetProperty("WorkSheet", BindingFlags.NonPublic | BindingFlags.Instance)
                                ?.GetValue(bsr);
                            if (_WorkSheetName != null)
                            {
                                sheetname = (string)_WorkSheetName;
                            }
                            else
                            {
                                sheetname = GetSheetNameFromPercentFormula(cellPercent.Formula);
                            }
                        }
                        cellPercentValue = cellPercent.GetValue<double>();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        cellPercentValue = 0.1; // force repack
                    }

                    if (cellPercentValue > 0 && sheetname != string.Empty)
                    {
                        Log.Info($"{cellPercentValue:0.##}% - {curPath}");
                        var sheet = p.Workbook.Worksheets[sheetname];
                        await Task.Delay(2);

                        // packed
                        if (curPath.FullName.EndsWith(".csv.x"))
                        {
                            for (var i = sheet.Dimension.Start.Row + 1; i <= sheet.Dimension.End.Row; i++)
                            {
                                try
                                {
                                    curPath = new UPath(Path.ChangeExtension(sheet.Cells[i, 1].Text, null)).ToAbsolute();
                                    Log.Info(" > " +curPath.FullName);

                                    var numLine = int.Parse(sheet.Cells[++i, 1].Text);
                                    var pqLines = new SimplePriorityQueue<Line>();
                                    while (numLine-- > 0) // lặp numLine lần.
                                    {
                                        ++i;
                                        var inf = sheet.Cells[i, 1].Text;
                                        if (inf == string.Empty)
                                        {
                                            numLine++; // only count valid row
                                            continue; // allow empty row
                                        }

                                        var vie = sheet.Cells[i, 3].Text;
                                        var line = new Line()
                                        {
                                            ID = inf,
                                            English = vie != string.Empty ? vie : sheet.Cells[i, 2].Text,
                                            Note = sheet.Cells[i, 4].Text
                                        };
                                        line.Vietnamese = line.English; // same
                                        var prio = line.TrimIdIndex();
                                        pqLines.Enqueue(line, prio);
                                    }

                                    var lines = pqLines.ToList(); // PQ, tự sắp xếp.S
                                    var curPathDir = curPath.GetDirectory();
                                    if (curPathDir != UPath.Root)
                                    {
                                        memIn.CreateDirectory(curPathDir);
                                    }
                                    _textIO.WriteAllLines(memIn.CreateFile(curPath + _textIO.Extension), lines);
                                    await Task.Delay(2);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex);
                                    Log.Error($"[csv.x.csv] {sheetname}, row: {i}");
                                }
                            } // end loop
                        }
                        else
                        {
                            var lines = ReadLocazCSV(sheet);
                            var curPathDir = curPath.GetDirectory();
                            if (curPathDir != UPath.Root)
                            {
                                memIn.CreateDirectory(curPathDir);
                            }
                            _textIO.WriteAllLines(memIn.CreateFile(curPath + _textIO.Extension), lines);
                        }
                    }
                    else
                    {
                        // [toc] line not valid
                    }
                }
            }
            return memIn;
        }

        private static List<Line> ReadLocazCSV(ExcelWorksheet sheet)
        {
            var result = new SimplePriorityQueue<Line>();

            try
            {
                for (var rowNum = sheet.Dimension.Start.Row + 1; rowNum <= sheet.Dimension.End.Row; rowNum++)
                {
                    var inf = sheet.Cells[rowNum, 1].Text;
                    if (inf == string.Empty) // allow empty row
                    {
                        continue;
                    }

                    var vie = sheet.Cells[rowNum, 3].Text;
                    var jap = sheet.Cells[rowNum, 4].Text;
                    if (vie == string.Empty)
                    {
                        vie = sheet.Cells[rowNum, 2].Text; // vie = eng
                    }

                    var line = new Line(inf, vie, string.Empty, jap);
                    line.Vietnamese = line.English; // same
                    var prio = line.TrimIdIndex();
                    result.Enqueue(line, prio);
                }
            }
            catch (Exception ex)
            {
                var ret = result.ToList();
                Console.WriteLine(ex);
                Console.WriteLine("Try Repack");
                Console.WriteLine("Sheet: " + sheet.Name);
                Console.WriteLine("curLines: " + result.Count);
                Console.WriteLine("Last: " + ret[ret.Count - 1].ID);
                return ret;
            }

            return result.ToList();
        }
    }
}
#endif