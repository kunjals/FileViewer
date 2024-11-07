using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileViewer.Models
{
    public class FileReadResult
    {
        public bool Success { get; set; }
        public string Contents { get; set; }
        public string ErrorMessage { get; set; }
        public string Encoding { get; set; }
        public long FileSizeBytes { get; set; }
    }
}
