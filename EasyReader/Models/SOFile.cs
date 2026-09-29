namespace EasyReader.Models
{
    public class SOFile
    {
    #pragma warning disable CS8618

        public string Path { get; set; }
        public string Name { get; set; }
        // Service Order number the file belongs to (optional)
        public string SONum { get; set; }
        // file creation date/time
        public System.DateTime CreatedDate { get; set; }
        public bool IsSelected { get; set; }
    }
}
