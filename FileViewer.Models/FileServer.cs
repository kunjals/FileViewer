using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileViewer.Models
{
    public class FileServer
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string InternalUrl { get; set; }  // Internal network URL
        public bool IsHealthy { get; set; }
        public DateTime LastChecked { get; set; }
        public string ApiKey { get; set; }
    }
}
