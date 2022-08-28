/*
https://www.webtoolkitonline.com/xml-minifier.html
https://www.webtoolkitonline.com/xml-formatter.html

https://github.com/shps951023/MiniExcel/blob/master/src/MiniExcel/Utils/XmlEncoder.cs
https://github.com/shps951023/MiniExcel/blob/master/src/MiniExcel/OpenXml/ExcelOpenXmlUtils.cs#L12
*/
#if BLAZOR
using Microsoft.JSInterop;
#endif
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Zio;

namespace ExR.Format
{
    [Plugin("com.dc.csv2xlsx", "! CSV -> XLSX", @"
- Repack: csv(s) -> xlsx
- Extract: xlsx -> csv(s)

Note: Any csv, xlsx
Sample result: https://docs.google.com/spreadsheets/d/1dhqXQspTgivoEE1mfcDsiBsB1o_iK26eFJMRGhHQ0XM/")]
    internal class A_csv2xlsx : TextFormat
    {
        OutputProviders.CsvTextIOProvider _csv = null;
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
                _csv = new OutputProviders.CsvTextIOProvider();
                Console.WriteLine("CSV(s) -> XLSX...");
                await Repack(FsIn, FsOut);
            }
            else
            {
                Console.WriteLine("XLSX -> CSV(s)...");
                Extract(FsIn, FsOut);
            }
            Console.WriteLine();
            throw new ExceptionWithoutStackTrace("csv2xlsx Done!\n");
            //return false;
        }

        public override List<Line> ExtractText(byte[] bytes)
        {
            return null;
        }

        public override byte[] RepackText(List<Line> lines)
        {
            return null;
        }

        void Extract(IFileSystem fsIn, IFileSystem fsOut)
        {

            var files = fsIn.EnumerateFiles(UPath.Root, "*.xlsx", SearchOption.AllDirectories);
            foreach (var file in files)
            {
#if BLAZOR
                var raw = fsIn.ReadAllBytes(file);
                var pathAndData = JSUn.InvokeUnmarshalled<byte[], int, string[]>("LoadXlsxUn", raw, -1);
                for (int i = 0; i < pathAndData.Length; i += 2)
                {
                    var _path = pathAndData[i];
                    _path = Path.ChangeExtension(_path, ".csv"); // TODO: skip _init_.yaml
                    var path = ((UPath)_path).ToAbsolute();
                    var csv = pathAndData[i + 1];


                    var pathDir = path.GetDirectory();
                    if (pathDir != UPath.Root)
                    {
                        fsOut.CreateDirectory(pathDir);
                    }
                    fsOut.WriteAllText(path, csv);
                }
#else
                Console.WriteLine("TODO...");
#endif
            }

        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct InteropStruct
        {
            [FieldOffset(0)]
            public string Name;
        }

        internal static string CreateSheetName(HashSet<string> sheetNames, string path, int index)
        {
            var baseNameWithoutExtensions = Path.GetFileName(path).Split('.', 2)[0];
            // TOOD: replace unk char
            baseNameWithoutExtensions = baseNameWithoutExtensions.Replace(' ', '_').Replace('\'', '_').Replace('[', '_').Replace(']', '_');
            baseNameWithoutExtensions += '_' + index.ToString();
            // Len fix
            if (baseNameWithoutExtensions.Length > 27) // 31 => max=27 (9999sheet)
            {
                baseNameWithoutExtensions = baseNameWithoutExtensions.Substring(0, 27);
            }
            var sheetName = baseNameWithoutExtensions;
            var c = 'a';
            while (sheetNames.Contains(sheetName))
            {
                sheetName = baseNameWithoutExtensions + c;
                c++;
            }
            sheetNames.Add(sheetName);

            return sheetName;
        }

        internal static string CreateTocCsv(Dictionary<string, string> dict)
        {
            var csvTOC = new StringBuilder();
            csvTOC.AppendLine("Name,Path");
            foreach (var item in dict)
            {
                csvTOC.AppendLine(item.Key + "," + item.Value);
            }
            return csvTOC.ToString();
        }

        async Task RepackAny(IEnumerable<UPath> files, IFileSystem fsIn, IFileSystem fsOut)
        {
#if BLAZOR
            // Use SheetJS (no style)
            var sheetNames = new HashSet<string>();
            var dict = new Dictionary<string, string>();
            int index = 0;
            using (var wb = JSUn.InvokeUnmarshalled<IJSUnmarshalledObjectReference>("CreateXlsx"))
            {
                foreach (var file in files)
                {
                    index++;
                    var rel = file.ToRelative().FullName;
                    var sheetName = new InteropStruct()
                    {
                        Name = CreateSheetName(sheetNames, rel, index)
                    };                
                    dict.Add(sheetName.Name, rel);

                    Console.WriteLine(sheetName.Name.PadLeft(31) + ' ' + rel);
                    await Task.Delay(2);

                    var csvBytes = fsIn.ReadAllBytes(file);

                    
                    _ = wb.InvokeUnmarshalled<byte[], InteropStruct, bool>("ImportCsvUn", csvBytes, sheetName);
                }

                var tocCsv = CreateTocCsv(dict);
                var tocName = new InteropStruct()
                {
                    Name = "TOC_IDX"
                };
                _ = wb.InvokeUnmarshalled<byte[], InteropStruct, bool>("ImportCsvUn", Encoding.UTF8.GetBytes(tocCsv), tocName);

                //var base64 = wb.Invoke<string>("ToBase64");
                //var bytes = Convert.FromBase64String(base64);
                /*
                ToBase64: function () {
                    return XLSX.write(this.wb, { type: 'base64' });
                }
                */

                // .NET 6.0 Preview 6
                // https://github.com/dotnet/aspnetcore/pull/33015
                // https://rob-blackbourn.github.io/blog/webassembly/wasm/array/arrays/javascript/c/2020/06/07/wasm-arrays.html
                // https://github.com/dotnet/aspnetcore/issues/27885
                /*
                 ToArray: function () {
                    const u8 = new Uint8Array(XLSX.write(this._wb, { type: 'array' }));
                    //const bytes = BINDING.js_array_to_mono_array(u8); // type
                    return u8;
                 }
                 */
                var bytes = wb.Invoke<byte[]>("ToArray");

                fsOut.WriteAllBytes("/_PACK_.xlsx", bytes);
            }
#else
            Console.WriteLine("TODO");
#endif
        }

        async Task Repack(IFileSystem fsIn, IFileSystem fsOut)
        {
            int bufSize = _10MB >> 3;
            var files = fsIn.EnumeratePaths(UPath.Root, "*.csv", SearchOption.AllDirectories);

            if (!files.Any()) // https://referencesource.microsoft.com/#System.Core/System/Linq/Enumerable.cs,8788153112b7ffd0
            //if (!files.GetEnumerator().MoveNext()) // IEnumerator implements IDisposable => using
            {
                Console.WriteLine("No csv was found.");
                return;
            }

            // check valid 4 column csv
            using (var fsCsv = FsIn.OpenFile(files.First(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (!OutputProviders.CsvTextIOProvider.IsValid(fsCsv, false))
                {
                    Console.WriteLine("CSV is not valid, try create TOC_INDEX version.");
                    await RepackAny(files, fsIn, fsOut);
                    return;
                }
            }

            // default process
            using (var ms = fsOut.CreateFile("/_PACK_.xlsx"))
            {
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    int index = 1;
                    var template_content_Type_Row = "<Override ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\" PartName=\"/xl/worksheets/sheet{0}.xml\"/>";
                    var template_workBook_Row = "<sheet state=\"visible\" name=\"{0}\" sheetId=\"{1}\" r:id=\"rId{2}\"/>";
                    var template_workBookRels_Row = "<Relationship Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Id=\"rId{0}\" Target=\"worksheets/sheet{1}.xml\"/>";
                    var xmlWSheetHeader = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:mx=\"http://schemas.microsoft.com/office/mac/excel/2008/main\" xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\" xmlns:mv=\"urn:schemas-microsoft-com:mac:vml\" xmlns:x14=\"http://schemas.microsoft.com/office/spreadsheetml/2009/9/main\" xmlns:x15=\"http://schemas.microsoft.com/office/spreadsheetml/2010/11/main\" xmlns:x14ac=\"http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac\" xmlns:xm=\"http://schemas.microsoft.com/office/excel/2006/main\"><sheetPr><outlinePr summaryBelow=\"0\" summaryRight=\"0\"/></sheetPr><sheetViews><sheetView workbookViewId=\"0\"/></sheetViews><sheetFormatPr customHeight=\"1\" defaultColWidth=\"14.43\" defaultRowHeight=\"15.75\"/>";

                    // sheet1.xml
                    var sbSheetToc = new StringBuilder(bufSize);
                    sbSheetToc.Append(xmlWSheetHeader);
                    sbSheetToc.Append("<cols><col customWidth=\"1\" min=\"1\" max=\"1\" width=\"64.43\"/><col customWidth=\"1\" min=\"2\" max=\"2\" width=\"27.29\"/><col customWidth=\"1\" min=\"3\" max=\"3\" width=\"18.71\"/><col customWidth=\"1\" min=\"4\" max=\"4\" width=\"53.0\"/></cols><sheetData><row r=\"1\"><c r=\"A1\" s=\"1\" t=\"s\"><v>0</v></c><c r=\"B1\" s=\"1\" t=\"s\"><v>1</v></c><c r=\"C1\" s=\"1\" t=\"s\"><v>2</v></c><c r=\"D1\" s=\"1\" t=\"s\"><v>3</v></c></row>");
                    var sbSheetTocLink = new StringBuilder(bufSize);
                    sbSheetTocLink.Append("<hyperlinks>");

                    // [Content_Types].xml
                    var sbContent_Types = new StringBuilder(bufSize);
                    sbContent_Types.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default ContentType=\"application/xml\" Extension=\"xml\"/><Default ContentType=\"application/vnd.openxmlformats-package.relationships+xml\" Extension=\"rels\"/>");
                    sbContent_Types.AppendFormat(template_content_Type_Row, index);

                    // workbook.xml.rels
                    var sbWorkbookRels = new StringBuilder(bufSize);
                    sbWorkbookRels.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme\" Target=\"theme/theme1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/><Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings\" Target=\"sharedStrings.xml\"/>");
                    sbWorkbookRels.AppendFormat(template_workBookRels_Row, index + 3, index);

                    // workbook.xml
                    var sbWorkbook = new StringBuilder(bufSize);
                    sbWorkbook.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:mx=\"http://schemas.microsoft.com/office/mac/excel/2008/main\" xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\" xmlns:mv=\"urn:schemas-microsoft-com:mac:vml\" xmlns:x14=\"http://schemas.microsoft.com/office/spreadsheetml/2009/9/main\" xmlns:x15=\"http://schemas.microsoft.com/office/spreadsheetml/2010/11/main\" xmlns:x14ac=\"http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac\" xmlns:xm=\"http://schemas.microsoft.com/office/excel/2006/main\"><workbookPr/><sheets>");
                    sbWorkbook.AppendFormat(template_workBook_Row, "TOC", index, index + 3);

                    var sheetNames = new HashSet<string>();
                    //var sharedString = new HashSet<string>();
                    var sharedString = new Dictionary<string, string>(); // string => formula (ref)

                    var strXMLEscape = string.Empty;
                    foreach (var file in files)
                    {
                        index++; // 2+

                        sbContent_Types.AppendFormat(template_content_Type_Row, index);

                        sbWorkbookRels.AppendFormat(template_workBookRels_Row, index + 3, index);

                        var sheetName = CreateSheetName(sheetNames, file.FullName, index);

                        var sheetNameEsc = EscapeStr(sheetName);
                        sbWorkbook.AppendFormat(template_workBook_Row, sheetNameEsc, index, index + 3);

                        var sbSheetCSV = new StringBuilder(bufSize);
                        sbSheetCSV.Append(xmlWSheetHeader);
                        sbSheetCSV.Append("<cols><col customWidth=\"1\" min=\"1\" max=\"1\" width=\"15.86\"/><col customWidth=\"1\" min=\"2\" max=\"3\" width=\"64.43\"/><col customWidth=\"1\" min=\"4\" max=\"4\" width=\"57.29\"/></cols><sheetData><row r=\"1\"><c r=\"A1\" s=\"5\" t=\"s\"><v>4</v></c><c r=\"B1\" s=\"1\" t=\"s\"><v>5</v></c><c r=\"C1\" s=\"1\" t=\"s\"><v>6</v></c><c r=\"D1\" s=\"1\" t=\"s\"><v>3</v></c></row>");
                        using (var fs = fsIn.OpenFile(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            var lines = _csv.ReadAllLine_NoMerge_NoPQ(fs); // csv -> xlsx 4 column
                            var endRowIndex = lines.Count + 1;
                            strXMLEscape = file.ToRelative().FullName;
                            Console.WriteLine(lines.Count.ToString().PadLeft(6) + ' ' + strXMLEscape);
                            await Task.Delay(1);

                            // TOC link
                            sbSheetToc.Append($"<row r=\"{index}\"><c r=\"A{index}\" s=\"2\" t=\"inlineStr\"><is><t>");
                            strXMLEscape = EscapeStr(strXMLEscape); // relative path
                            sbSheetToc.Append(strXMLEscape);
                            sbSheetToc.Append("</t></is></c>");

                            sbSheetTocLink.Append($"<hyperlink display=\"{strXMLEscape}\" location=\"{sheetNameEsc}!A1\" ref=\"A{index}\"/>");

                            sbSheetToc.Append($"<c r=\"B{index}\" s=\"3\"/><c r=\"C{index}\" s=\"4\"><f>");
                            strXMLEscape = $"SUMPRODUCT(--(LEN({sheetName}!C$2:C${endRowIndex})>1))/SUMPRODUCT(--(LEN({sheetName}!B$2:B${endRowIndex})>1))";
                            strXMLEscape = EscapeStr(strXMLEscape);
                            sbSheetToc.Append(strXMLEscape);
                            sbSheetToc.Append($"</f><v>0</v></c><c r=\"D{index}\" s=\"3\"/></row>");

                            for (int i = 0; i < lines.Count; i++)
                            {
                                var line = lines[i];
                                var row = i + 2;

                                sbSheetCSV.Append($"<row r=\"{row}\"><c r=\"A{row}\" s=\"6\" t=\"inlineStr\"><is><t>");
                                strXMLEscape = EscapeStr(line.ID);
                                sbSheetCSV.Append(strXMLEscape);
                                sbSheetCSV.Append("</t></is></c>");

                                if (string.IsNullOrEmpty(line.English))
                                {
                                    sbSheetCSV.Append($"<c r=\"B{row}\" s=\"3\"/>");
                                }
                                else
                                {
                                    sbSheetCSV.Append($"<c r=\"B{row}\" s=\"3\" t=\"inlineStr\"><is><t>");
                                    strXMLEscape = EscapeStr(line.English);
                                    sbSheetCSV.Append(strXMLEscape);
                                    sbSheetCSV.Append("</t></is></c>");

                                    // TODO: auto ref
                                    //if (!sharedString.ContainsKey(line.English))
                                    //{
                                    //    sharedString.Add(line.English, sheetNameEsc + "!C" + row.ToString());
                                    //}
                                }

                                if (string.IsNullOrEmpty(line.Vietnamese))
                                {

                                    sbSheetCSV.Append($"<c r=\"C{row}\" s=\"3\"/>");
                                }
                                else
                                {
                                    sbSheetCSV.Append($"<c r=\"C{row}\" s=\"3\" t=\"inlineStr\"><is><t>");
                                    strXMLEscape = EscapeStr(line.Vietnamese);
                                    sbSheetCSV.Append(strXMLEscape);
                                    sbSheetCSV.Append("</t></is></c>");
                                }

                                if (string.IsNullOrEmpty(line.Note))
                                {
                                    sbSheetCSV.Append($"<c r=\"D{row}\" s=\"3\"/>");
                                }
                                else
                                {
                                    sbSheetCSV.Append($"<c r=\"D{row}\" s=\"3\" t=\"inlineStr\"><is><t>");
                                    strXMLEscape = EscapeStr(line.Note);
                                    sbSheetCSV.Append(strXMLEscape);
                                    sbSheetCSV.Append("</t></is></c>");
                                }

                                sbSheetCSV.Append("</row>");
                            }

                            sbSheetCSV.Append("</sheetData>");
                            // highlight formula
                            sbSheetCSV.Append($"<conditionalFormatting sqref=\"C2:C{endRowIndex}\"><cfRule type=\"expression\" dxfId=\"0\" priority=\"1\"><formula>ISFORMULA(C2:C62)</formula></cfRule></conditionalFormatting>");
                            // link jump toc
                            sbSheetCSV.Append($"<hyperlinks><hyperlink display=\"ID\" location=\"TOC!A{index}\" ref=\"A1\"/>");
                            sbSheetCSV.Append("</hyperlinks></worksheet>");
                        }
                        
                        var outPath = $"xl/worksheets/sheet{index}.xml";
                        using (var entryStream = zip.CreateEntry(outPath, CompressionLevel.SmallestSize).Open())
                        using (var streamWriter = new StreamWriter(entryStream))
                        {
                            streamWriter.Write(sbSheetCSV);
                        }
                        // done sheet2+
                    } // done all sheets
                    if (index > 200)
                    {
                        Console.WriteLine($"\nWarning: {index} sheets, >200 sheets per workbook (GoogleSheets Limit).\nPlease try CSV merge!");
                    }

                    // [Content_Types].xml
                    using (var entryStream = zip.CreateEntry("[Content_Types].xml", CompressionLevel.SmallestSize).Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        sbContent_Types.Append("<Override ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\" PartName=\"/xl/sharedStrings.xml\"/><Override ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\" PartName=\"/xl/styles.xml\"/><Override ContentType=\"application/vnd.openxmlformats-officedocument.theme+xml\" PartName=\"/xl/theme/theme1.xml\"/><Override ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\" PartName=\"/xl/workbook.xml\"/></Types>");
                        streamWriter.Write(sbContent_Types);
                    }

                    // xl/workbook.xml
                    using (var entryStream = zip.CreateEntry("xl/workbook.xml", CompressionLevel.SmallestSize).Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        sbWorkbook.Append("</sheets><definedNames/><calcPr/></workbook>");
                        streamWriter.Write(sbWorkbook);
                    }

                    // xl/_rels/workbook.xml.rels"
                    using (var entryStream = zip.CreateEntry("xl/_rels/workbook.xml.rels", CompressionLevel.SmallestSize).Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        sbWorkbookRels.Append("</Relationships>");
                        streamWriter.Write(sbWorkbookRels);
                    }

                    // xl/worksheets/sheet1.xml
                    using (var entryStream = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.SmallestSize).Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        sbSheetTocLink.Append("</hyperlinks>");
                        sbSheetToc.Append("</sheetData>");
                        sbSheetToc.Append($"<conditionalFormatting sqref=\"C2:C{index}\">");
                        sbSheetToc.Append("<cfRule type=\"colorScale\" priority=\"1\"><colorScale><cfvo type=\"min\"/><cfvo type=\"max\"/><color rgb=\"FFFFFFFF\"/><color rgb=\"FF57BB8A\"/></colorScale></cfRule></conditionalFormatting>");
                        sbSheetToc.Append(sbSheetTocLink);
                        sbSheetToc.Append("</worksheet>"); // done sheet TOC
                        streamWriter.Write(sbSheetToc);
                    }

                    // _rels/.rels
                    using (var entryStream = zip.CreateEntry("_rels/.rels", CompressionLevel.SmallestSize).Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        streamWriter.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                    }

                    // xl/sharedStrings.xml
                    using (var entryStream = zip.CreateEntry("xl/sharedStrings.xml", CompressionLevel.SmallestSize).Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        streamWriter.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" uniqueCount=\"7\"><si><t>File</t></si><si><t>Translator</t></si><si><t>Status</t></si><si><t>Note</t></si><si><t>ID</t></si><si><t>English</t></si><si><t>Vietnamese</t></si></sst>");
                    }

                    // xl/styles.xml
                    using (var entryStream = zip.CreateEntry("xl/styles.xml", CompressionLevel.SmallestSize).Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        streamWriter.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:x14ac=\"http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac\" xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\"><fonts count=\"6\"><font><sz val=\"10.0\"/><color rgb=\"FF000000\"/><name val=\"Consolas\"/></font><font><b/><sz val=\"12.0\"/><color rgb=\"FFFFFFFF\"/><name val=\"Consolas\"/></font><font><u/><sz val=\"12.0\"/><color rgb=\"FF0000FF\"/><name val=\"Consolas\"/></font><font><sz val=\"12.0\"/><name val=\"Consolas\"/></font><font><sz val=\"12.0\"/><color theme=\"1\"/><name val=\"Consolas\"/></font><font><b/><u/><sz val=\"12.0\"/><color rgb=\"FFFFFFFF\"/><name val=\"Consolas\"/></font></fonts><fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"lightGray\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF434343\"/><bgColor rgb=\"FF434343\"/></patternFill></fill></fills><borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf borderId=\"0\" fillId=\"0\" fontId=\"0\" numFmtId=\"0\" applyAlignment=\"1\" applyFont=\"1\"/></cellStyleXfs><cellXfs count=\"7\"><xf borderId=\"0\" fillId=\"0\" fontId=\"3\" numFmtId=\"49\" xfId=\"0\" applyAlignment=\"1\" applyFont=\"1\" applyNumberFormat=\"1\"><alignment readingOrder=\"0\" shrinkToFit=\"0\" vertical=\"top\" wrapText=\"1\"/></xf><xf borderId=\"0\" fillId=\"2\" fontId=\"1\" numFmtId=\"49\" xfId=\"0\" applyAlignment=\"1\" applyFont=\"1\" applyNumberFormat=\"1\" applyFill=\"1\"><alignment horizontal=\"center\" readingOrder=\"0\" shrinkToFit=\"0\" vertical=\"top\" wrapText=\"1\"/></xf><xf borderId=\"0\" fillId=\"0\" fontId=\"2\" numFmtId=\"49\" xfId=\"0\" applyAlignment=\"1\" applyFont=\"1\" applyNumberFormat=\"1\"><alignment readingOrder=\"0\" shrinkToFit=\"0\" vertical=\"top\" wrapText=\"1\"/></xf><xf borderId=\"0\" fillId=\"0\" fontId=\"3\" numFmtId=\"49\" xfId=\"0\" applyAlignment=\"1\" applyFont=\"1\" applyNumberFormat=\"1\"><alignment readingOrder=\"0\" shrinkToFit=\"0\" vertical=\"top\" wrapText=\"1\"/></xf><xf borderId=\"0\" fillId=\"0\" fontId=\"4\" numFmtId=\"10\" xfId=\"0\" applyAlignment=\"1\" applyFont=\"1\" applyNumberFormat=\"1\"><alignment horizontal=\"center\" shrinkToFit=\"0\" vertical=\"top\" wrapText=\"1\"/></xf><xf borderId=\"0\" fillId=\"2\" fontId=\"5\" numFmtId=\"49\" xfId=\"0\" applyAlignment=\"1\" applyFont=\"1\" applyNumberFormat=\"1\" applyFill=\"1\"><alignment horizontal=\"center\" readingOrder=\"0\" shrinkToFit=\"0\" vertical=\"top\" wrapText=\"1\"/></xf><xf borderId=\"0\" fillId=\"0\" fontId=\"3\" numFmtId=\"49\" xfId=\"0\" applyAlignment=\"1\" applyFont=\"1\" applyNumberFormat=\"1\"><alignment horizontal=\"right\" readingOrder=\"0\" vertical=\"top\"/></xf></cellXfs><cellStyles count=\"1\"><cellStyle xfId=\"0\" name=\"Normal\" builtinId=\"0\"/></cellStyles><dxfs count=\"1\"><dxf><font/><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFD9D9D9\"/><bgColor rgb=\"FFD9D9D9\"/></patternFill></fill><border/></dxf></dxfs></styleSheet>"); // <dxfs count=\"0\"/> -> <dxfs count=\"1\"><dxf><font/><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFD9D9D9\"/><bgColor rgb=\"FFD9D9D9\"/></patternFill></fill><border/></dxf></dxfs>
                    }

                    // xl/theme/theme1.xml
                    using (var entryStream = zip.CreateEntry("xl/theme/theme1.xml", CompressionLevel.SmallestSize).Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        streamWriter.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><a:theme xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" name=\"Sheets\"><a:themeElements><a:clrScheme name=\"Sheets\"><a:dk1><a:srgbClr val=\"000000\"/></a:dk1><a:lt1><a:srgbClr val=\"FFFFFF\"/></a:lt1><a:dk2><a:srgbClr val=\"000000\"/></a:dk2><a:lt2><a:srgbClr val=\"FFFFFF\"/></a:lt2><a:accent1><a:srgbClr val=\"4285F4\"/></a:accent1><a:accent2><a:srgbClr val=\"EA4335\"/></a:accent2><a:accent3><a:srgbClr val=\"FBBC04\"/></a:accent3><a:accent4><a:srgbClr val=\"34A853\"/></a:accent4><a:accent5><a:srgbClr val=\"FF6D01\"/></a:accent5><a:accent6><a:srgbClr val=\"46BDC6\"/></a:accent6><a:hlink><a:srgbClr val=\"1155CC\"/></a:hlink><a:folHlink><a:srgbClr val=\"1155CC\"/></a:folHlink></a:clrScheme><a:fontScheme name=\"Sheets\"><a:majorFont><a:latin typeface=\"Arial\"/><a:ea typeface=\"Arial\"/><a:cs typeface=\"Arial\"/></a:majorFont><a:minorFont><a:latin typeface=\"Arial\"/><a:ea typeface=\"Arial\"/><a:cs typeface=\"Arial\"/></a:minorFont></a:fontScheme><a:fmtScheme name=\"Office\"><a:fillStyleLst><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:gradFill rotWithShape=\"1\"><a:gsLst><a:gs pos=\"0\"><a:schemeClr val=\"phClr\"><a:lumMod val=\"110000\"/><a:satMod val=\"105000\"/><a:tint val=\"67000\"/></a:schemeClr></a:gs><a:gs pos=\"50000\"><a:schemeClr val=\"phClr\"><a:lumMod val=\"105000\"/><a:satMod val=\"103000\"/><a:tint val=\"73000\"/></a:schemeClr></a:gs><a:gs pos=\"100000\"><a:schemeClr val=\"phClr\"><a:lumMod val=\"105000\"/><a:satMod val=\"109000\"/><a:tint val=\"81000\"/></a:schemeClr></a:gs></a:gsLst><a:lin ang=\"5400000\" scaled=\"0\"/></a:gradFill><a:gradFill rotWithShape=\"1\"><a:gsLst><a:gs pos=\"0\"><a:schemeClr val=\"phClr\"><a:satMod val=\"103000\"/><a:lumMod val=\"102000\"/><a:tint val=\"94000\"/></a:schemeClr></a:gs><a:gs pos=\"50000\"><a:schemeClr val=\"phClr\"><a:satMod val=\"110000\"/><a:lumMod val=\"100000\"/><a:shade val=\"100000\"/></a:schemeClr></a:gs><a:gs pos=\"100000\"><a:schemeClr val=\"phClr\"><a:lumMod val=\"99000\"/><a:satMod val=\"120000\"/><a:shade val=\"78000\"/></a:schemeClr></a:gs></a:gsLst><a:lin ang=\"5400000\" scaled=\"0\"/></a:gradFill></a:fillStyleLst><a:lnStyleLst><a:ln w=\"6350\" cap=\"flat\" cmpd=\"sng\" algn=\"ctr\"><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:prstDash val=\"solid\"/><a:miter lim=\"800000\"/></a:ln><a:ln w=\"12700\" cap=\"flat\" cmpd=\"sng\" algn=\"ctr\"><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:prstDash val=\"solid\"/><a:miter lim=\"800000\"/></a:ln><a:ln w=\"19050\" cap=\"flat\" cmpd=\"sng\" algn=\"ctr\"><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:prstDash val=\"solid\"/><a:miter lim=\"800000\"/></a:ln></a:lnStyleLst><a:effectStyleLst><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst><a:outerShdw blurRad=\"57150\" dist=\"19050\" dir=\"5400000\" algn=\"ctr\" rotWithShape=\"0\"><a:srgbClr val=\"000000\"><a:alpha val=\"63000\"/></a:srgbClr></a:outerShdw></a:effectLst></a:effectStyle></a:effectStyleLst><a:bgFillStyleLst><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"><a:tint val=\"95000\"/><a:satMod val=\"170000\"/></a:schemeClr></a:solidFill><a:gradFill rotWithShape=\"1\"><a:gsLst><a:gs pos=\"0\"><a:schemeClr val=\"phClr\"><a:tint val=\"93000\"/><a:satMod val=\"150000\"/><a:shade val=\"98000\"/><a:lumMod val=\"102000\"/></a:schemeClr></a:gs><a:gs pos=\"50000\"><a:schemeClr val=\"phClr\"><a:tint val=\"98000\"/><a:satMod val=\"130000\"/><a:shade val=\"90000\"/><a:lumMod val=\"103000\"/></a:schemeClr></a:gs><a:gs pos=\"100000\"><a:schemeClr val=\"phClr\"><a:shade val=\"63000\"/><a:satMod val=\"120000\"/></a:schemeClr></a:gs></a:gsLst><a:lin ang=\"5400000\" scaled=\"0\"/></a:gradFill></a:bgFillStyleLst></a:fmtScheme></a:themeElements></a:theme>");
                    }
                }
            }

            //
        }

        static string EscapeStr(string s)
        {
            return System.Security.SecurityElement.Escape(s);
        }

        //internal static string EncodeXML(string value) => value == null
        //          ? string.Empty
        //          : XmlEncoder.EncodeString(value).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");

        //internal static class XmlEncoder
        //{
        //    private static readonly Regex xHHHHRegex = new Regex("_(x[\\dA-Fa-f]{4})_", RegexOptions.Compiled);
        //    public static string EncodeString(string encodeStr)
        //    {
        //        if (encodeStr == null) return null;

        //        encodeStr = xHHHHRegex.Replace(encodeStr, "_x005F_$1_");

        //        var sb = new StringBuilder(encodeStr.Length);

        //        foreach (var ch in encodeStr)
        //        {
        //            if (System.Xml.XmlConvert.IsXmlChar(ch))
        //                sb.Append(ch);
        //            else
        //                sb.Append(System.Xml.XmlConvert.EncodeName(ch.ToString()));
        //        }

        //        return sb.ToString();
        //    }
        //}
    }
}
