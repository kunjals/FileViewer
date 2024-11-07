using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileViewer.Models
{
    public class FileSearchRequest
    {
        public string ServerId { get; set; }
        public string RootName { get; set; }
        public string Path { get; set; }
        public string SearchTerm { get; set; }
        public SearchType SearchType { get; set; }
    }
}
