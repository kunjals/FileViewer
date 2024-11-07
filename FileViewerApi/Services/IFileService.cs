
using FileViewer.Models;

namespace FileViewerApi.Services
{
    public interface IFileService
    {
        IEnumerable<RootDirectory> GetRootDirectories();
        IEnumerable<FileItem> GetDirectoryContents(string rootName, string path);
        FileReadResult ReadFileContents(string rootName, string path);
    }
}
