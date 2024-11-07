namespace FileViewer.Models
{
    public class FileSearchResult
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public DateTime LastModified { get; set; }
        public string MatchedContent { get; set; } // A snippet of the matched content
        public int LineNumber { get; set; }
    }
}