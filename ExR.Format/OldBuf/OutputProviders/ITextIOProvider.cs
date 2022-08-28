using ExR.Format;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ExR.OutputProviders
{
    public interface ITextIOProvider
    {
        string Extension { get; }
        void WriteAllLines(Stream outStream, List<Line> lines);
        List<Line> ReadAllLines(Stream inStream);
    }
}
