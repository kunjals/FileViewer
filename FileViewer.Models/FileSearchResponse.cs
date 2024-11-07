using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileViewer.Models
{
    public class FileSearchResponse
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public IEnumerable<FileSearchResult> Results { get; set; }
    }
}
