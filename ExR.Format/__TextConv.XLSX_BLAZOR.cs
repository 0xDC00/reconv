#if BLAZOR
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Zio;
using Zio.FileSystems;

namespace ExR
{
    public partial class TextConv
    {
        //public async Task<MemoryStream> RepackXlsx(Stream xlsx, Stream zipOut_Payload)
        //{
        //    return new MemoryStream(); // TODO miniExcel
        //}

        public async Task<MemoryStream> RepackXlsx(string[] pathAndData, Stream zipOut_Payload)
        {
            var memIn = new MemoryFileSystem();
            for (int i = 0; i < pathAndData.Length; i+=2)
            {
                var path = ((UPath)(pathAndData[i])).ToAbsolute();
                var csv = pathAndData[i + 1];

                var pathDir = path.GetDirectory();
                if (pathDir != UPath.Root)
                {
                    memIn.CreateDirectory(pathDir);
                }
                memIn.WriteAllText(path, csv);
            }

            return await RepackXlsx(memIn, zipOut_Payload);
        }

        private async Task<MemoryStream> RepackXlsx(IFileSystem memIn, Stream zipOut_Payload)
        {
            var memOut = new MemoryFileSystem();
            if (zipOut_Payload != null && zipOut_Payload.Length > 0)
            {
                memOut.ImportZip(zipOut_Payload);
                // importPayload to MemIn ?
            }

            await Repack(memIn, memOut);
            var ms = new MemoryStream();
            memOut.ExportZip(ms);
            return ms;
        }

        //public static async Task Main(string[] args)
        //{

        //}
    }
}
#endif