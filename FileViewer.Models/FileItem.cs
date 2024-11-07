using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileViewer.Models
{
    public class FileItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool IsDirectory { get; set; }
        public string RootName { get; set; }
        public DateTime LastModified { get; set; }
        public long Size { get; set; } // In bytes
    }
}
