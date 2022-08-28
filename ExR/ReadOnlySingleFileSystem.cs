using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zio;
using Zio.FileSystems;

namespace ExR
{
    class ReadOnlySingleFileSystem : PhysicalFileSystem
    {
        UPath _currentFile;

        public ReadOnlySingleFileSystem(UPath pathToFile)
        {
            _currentFile = ConvertPathFromInternal(pathToFile.FullName);
        }

        public UPath GetDirectory()
        {
            return _currentFile.GetDirectory();
        }

        protected override IEnumerable<UPath> EnumeratePathsImpl(UPath path, string searchPattern, SearchOption searchOption, SearchTarget searchTarget)
        {
            var search = SearchPattern.Parse(ref path, ref searchPattern);

            var isEntryMatching = search.Match(_currentFile);
            if (isEntryMatching)
            {
                yield return _currentFile;
            }
        }

        protected override bool FileExistsImpl(UPath path)
        {
            return path.Equals(_currentFile) ? true : false;
            //return File.Exists(ConvertPathToInternal(path));
        }


        protected override Stream OpenFileImpl(UPath path, FileMode mode, FileAccess access, FileShare share)
        {
            if (FileExistsImpl(path))
                return File.Open(ConvertPathToInternal(path), mode, access, share);

            throw new FileNotFoundException();
        }
    }
}
