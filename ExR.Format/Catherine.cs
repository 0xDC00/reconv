/*
201C=“
201D=”
3041=<i>
3080=à
3081=á
3088=è
3089=é
308A=ê
308C=ì
308D=í
30A7=ú
3091=ñ
3092=ò
3093=ó
305F=¿
30A1=ô
TBL
*/

using BufLib.Common.IO;
using BufLib.TextFormats.BinaryModels.Catherine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ExR.Format
{
    //[Plugin("com.dc.cath_psVita", "Catherine Classis - PSVita, PS4 (.bmd, .bf, .DAT, .BIN, .pac)", "ReimportMode: .DAT, .pac")]
    //class CatherinePSVita : Catherine
    //{
    //    protected override Platform PF => Platform.PSVita;
    //}

    [Plugin("com.dc.cath_steam", "Catherine Classis - Steam (.bmd, .bf, .DAT, .BIN, .pac, .EXE)", "ReimportMode: .DAT, .pac, .exe")]
    class CatherinePC : Catherine
    {
        protected override Platform PF => Platform.Steam_Classis;
    }

    [Plugin("com.dc.cath_ps3j", "Catherine - PS3 [BLJM-60215] (.bmd, .bf, .DAT, .BIN, .pac, .ELF)", "ReimportMode: .DAT, .pac, .elf")]
    class CatherineJP : Catherine
    {
        protected override Platform PF => Platform.PS3_JP;
    }

    [Plugin("com.dc.cath_ps3e", "Catherine - PS3 [BLUS-30428] (.bmd, .bf, .DAT, .BIN, .pac, .ELF)", "ReimportMode: .DAT, .pac, .elf")]
    class Catherine : TextFormat
    {
        protected virtual Platform PF => Platform.PS3_EN;

        public override bool Init(Dictionary<string, object> dict)
        {
            Extensions = new string[] { ".bmd", ".bf", ".DAT", ".BIN", ".pac", ".elf", ".exe" };

            DAT.Init(PF);
            EBOOT.Init(PF);
            BIN.Init(PF);
            // for endian
            BMD.Init(PF);
            BF.Init(PF);
            // PAC little endian

            return true;
        }

        //private Endian endian = Endian.BigEndian; // TODO endian swap for PS4/PSVita

        public override List<Line> ExtractText(byte[] buf)
        {
            using (var ms = new MemoryStream(buf))
            using (var br = new EndianBinaryReader(ms))
            {
                List<Line> result = null;
                string ext = Path.GetExtension(CurrentFilePath);

                switch (ext)
                {
                    case ".bmd":
                        result = BMD.ExtractText(br);

                        ///* Run Test - thuật toán nén cho kết quả khác -> không compare DATA -> ...*/
                        ////if(result.Count > 0)
                        ////{
                        ////    var oldData = File.ReadAllBytes(CurrentFilePath);
                        ////    var newData = BMD.RepackText(result);
                        ////    if (oldData.SequenceEqual(newData) == false)
                        ////        Console.WriteLine("[W] BMD repack fail");
                        ////}
                        //if(result.Count > 0)
                        //{
                        //    var newBmd = BMD.RepackText(new List<Line>(result));
                        //    var newResult = BMD.ExtractText(newBmd);
                        //    TryCompare(result, newResult, "[W] BMD repack fail");
                        //}
                        break;

                    case ".bf":
                        result = BF.ExtractText(br);
                        //if (result.Count > 0)
                        //{
                        //    var newBf = BF.RepackText(new List<Line>(result));
                        //    var newResult = BF.ExtractText(newBf);
                        //    TryCompare(result, newResult, "[W] BF repack fail");
                        //}
                        break;

                    case ".DAT":
                        result = DAT.ExtractText(br);
                        //if (result.Count > 0)
                        //{
                        //    var oldDAT = ReadCurrentFileData();
                        //    var newDAT = DAT.RepackText(result, (byte[])oldDAT.Clone());
                        //    if (oldDAT.SequenceEqual(newDAT) == false)
                        //        throw new Exception("[W] DAT repack fail");
                        //}
                        break;
                    case ".BIN":
                        result = BIN.ExtractText(br);
                        //if (result.Count > 0)
                        //{
                        //    var oldDAT = ReadCurrentFileData();
                        //    var newDAT = BIN.RepackText(result);
                        //    if (oldDAT.SequenceEqual(newDAT) == false)
                        //        throw new Exception("[W] BIN repack fail");
                        //}
                        break;
                    case ".pac":
                        result = PAC.ExtractText(br);
                        //if (result.Count > 0)
                        //{
                        //    var oldPAC = ReadCurrentFileData();
                        //    var newPAC = PAC.RepackText(new List<Line>(result), oldPAC);
                        //    var newResult = PAC.ExtractText(newPAC);
                        //    TryCompare(result, newResult, "[W] PAC repack fail");
                        //}
                        break;

                    case ".exe":
                    case ".elf":
                        if (ext == ".exe")
                        {
                            EBOOT.Encoding = Encoding.Unicode;
                            EBOOT.Endian = Endian.LittleEndian;
                            EBOOT.Align = 4; // 32bit
                        }
                        result = EBOOT.ExtractText(br);
                        //if (result.Count > 0)
                        //{
                        //    var oldELF = ReadCurrentFileData();
                        //    var newELF = EBOOT.RepackText(result, (byte[])oldELF.Clone());
                        //    if (oldELF.SequenceEqual(newELF) == false)
                        //    {
                        //        throw new Exception("[W] ELF repack fail");
                        //    }
                        //}
                        break;
                }

                return result;
            }
        }

        public override byte[] RepackText(List<Line> lines)
        {
            byte[] result;
            string ext = Path.GetExtension(CurrentFilePath);

            switch (ext)
            {
                case ".bmd":
                    result = BMD.RepackText(lines);
                    break;
                case ".bf":
                    result = BF.RepackText(lines);
                    break;
                case ".DAT":
                    var mailData = ReadCurrentFileData();
                    result = DAT.RepackText(lines, mailData);
                    break;
                case ".BIN":
                    result = BIN.RepackText(lines);
                    break;
                case ".pac":
                    var pacData = ReadCurrentFileData();
                    result = PAC.RepackText(lines, pacData);
                    break;
                case ".exe":
                case ".elf":
                    if (ext == ".exe")
                    {
                        EBOOT.Encoding = Encoding.Unicode;
                        EBOOT.Endian = Endian.LittleEndian;
                        EBOOT.Align = 4;
                    }
                    var elfData = ReadCurrentFileData();
                    result = EBOOT.RepackText(lines, elfData);
                    break;

                default:
                    throw new ExceptionWithoutStackTrace("Impossible! " + ext);
            }

            return result;
        }

        private static void TryCompare(List<Line> lines, List<Line> lines2, string message)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                // khong compare Id, vi thuat toan nen khac -> khac header
                var line1 = lines[i];
                var line2 = lines2[i];
                if (line1.English != line2.English)
                {
                    throw new Exception(message);
                }
            }
        }
    }
}
