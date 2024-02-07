namespace ShapeMaker;

/// <summary>
/// File scanner. Scans a directory for shape files of a given voxel count and extension.
/// </summary>
public class FileScanner {
    /// <summary>
    /// Results of a file scan.
    /// </summary>
    public class Results {
        /// <summary>
        /// width
        /// </summary>
        public byte w;

        /// <summary>
        /// height
        /// </summary>
        public byte h;

        /// <summary>
        /// depth
        /// </summary>
        public byte d;

        /// <summary>
        /// voxel count
        /// </summary>
        public byte n;

        /// <summary>
        /// file extension
        /// </summary>
        public required string ext;

        /// <summary>
        /// full file path
        /// </summary>
        public string Filepath => FileReader.FilePath(n, w, h, d, ext);

        /// <summary>
        /// file size in bytes
        /// </summary>
        public long size;
    }

    /// <summary>
    /// List of results of a file scan.
    /// </summary>
    public List<Results> List { get; } = new();

    /// <summary>
    /// Create and scan a directory for shape files of a given voxel count and extension. Read results in .List.
    /// </summary>
    /// <param name="n">voxel count</param>
    /// <param name="ext">file extension (defaults to .bin)</param>
    public FileScanner(byte n, string ext = Program.FILE_EXT) {
        var di = new DirectoryInfo(Path.Combine(Program.options.filePath, n.ToString()));

        // scan for and migrate old files
        var renameList = new List<(string, string)>();
        foreach (var file in di.GetFiles("*" + ext)) {
            if (!file.Name.EndsWith(ext)) 
                continue;
            var dim = file.Name.Substring(0, file.Name.Length - ext.Length).Split(',');
            if (dim.Length != 3) 
                continue;
            if (!byte.TryParse(dim[0], out var w) || w < 1 || w > n)
                continue;
            if (!byte.TryParse(dim[1], out var h) || h < 1 || h > n) 
                continue;
            if (!byte.TryParse(dim[2], out var d) || d < 1 || d > n) 
                continue;
            var newDim = string.Join('x', dim);
            renameList.Add((file.Name, newDim + ext));
        }
        foreach (var (oldName, newName) in renameList) {
            var oldPath = Path.Combine(Program.options.filePath, n.ToString(), oldName);
            var newPath = Path.Combine(Program.options.filePath, n.ToString(), newName);
            File.Move(oldPath, newPath);
        }

        // scan for new files
        foreach (var file in di.GetFiles("*" + ext)) {
            if (!file.Name.EndsWith(ext)) continue;
            var dim = file.Name.Substring(0, file.Name.Length - ext.Length).Split('x');
            if (dim.Length != 3)
                continue;
            if (!byte.TryParse(dim[0], out var w) || w < 1 || w > n) 
                continue;
            if (!byte.TryParse(dim[1], out var h) || h < 1 || h > n) 
                continue;
            if (!byte.TryParse(dim[2], out var d) || d < 1 || d > n) 
                continue;
            List.Add(new Results() { n = n, w = w, h = h, d = d, ext = ext, size = file.Length });
        }
    }
}