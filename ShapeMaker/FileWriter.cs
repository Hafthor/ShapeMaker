namespace ShapeMaker;

/// <summary>
/// File writer. Writes shapes to a file.
/// </summary>
public class FileWriter : IDisposable {
    private FileStream? fs = null;
    private readonly int length;
    private readonly string path;
    private readonly DateTime createTime;

    /// <summary>
    /// Clears all shape files for a given voxel count.
    /// </summary>
    /// <param name="n">voxel count</param>
    public static void Clear(byte n) {
        var di = new DirectoryInfo(Path.Combine(Program.options.filePath, n.ToString()));
        if (!di.Exists)
            di.Create();
        else {
            var list = new FileScanner(n).List;
            foreach (var f in list)
                File.Delete(f.Filepath);
            var tmpList = new FileScanner(n, ".tmp").List;
            foreach (var f in tmpList)
                File.Delete(f.Filepath);
            var completePath = Path.Combine(Program.options.filePath, n.ToString(), Program.FILE_COMPLETE);
            if (File.Exists(completePath))
                File.Delete(completePath);
        }
    }

    /// <summary>
    /// Clears all temporary shape files for a given voxel count.
    /// </summary>
    /// <param name="n">voxel count</param>
    public static void ClearTmp(byte n) {
        var di = new DirectoryInfo(Path.Combine(Program.options.filePath, n.ToString()));
        if (!di.Exists)
            di.Create();
        else
            foreach (var f in new FileScanner(n, ".tmp").List)
                File.Delete(f.Filepath);
    }

    /// <summary>
    /// Stores a file that indicates that all shapes of a given voxel count have been found. File contains a string
    /// that indicates the count and timing for the operation.
    /// </summary>
    /// <param name="n">voxel count</param>
    /// <param name="s">string containing the count and timing for the operation</param>
    public static void MarkNComplete(int n, string s) {
        var path = Path.Combine(Program.options.filePath, n.ToString(), Program.FILE_COMPLETE);
        File.WriteAllText(path, s);
    }

    /// <summary>
    /// Creates a new file writer for a given voxel count and shape size.
    /// </summary>
    /// <param name="n">voxel count</param>
    /// <param name="w">width</param>
    /// <param name="h">height</param>
    /// <param name="d">depth</param>
    public FileWriter(int n, int w, int h, int d) {
        path = FileReader.FilePath(n, w, h, d, "");
        length = new BitShape((byte)w, (byte)h, (byte)d).bytes.Length;
        createTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Writes a shape to the file.
    /// </summary>
    /// <param name="shape">bytes representing the internal contents of the shape</param>
    public void Write(byte[] shape) {
        if (shape.Length != length) 
            throw new ArgumentOutOfRangeException(nameof(shape), shape.Length, "unexpected shape length - should be " + length);
        fs ??= new FileStream(path + ".tmp", FileMode.Append, FileAccess.Write, FileShare.None, 65536, FileOptions.None);
        fs.Write(shape);
    }

    /// <summary>
    /// Closes the file writer and renames the temporary file to the final file name. Also sets the creation and
    /// last write times to the time to indicate when it finished and when it was created, which will indicate how
    /// long the operation took.
    /// </summary>
    public void Dispose() {
        fs?.Dispose();
        if (File.Exists(path + ".tmp")) {
            var updateTime = DateTime.UtcNow;
            File.SetCreationTimeUtc(path + ".tmp", createTime);
            File.SetLastWriteTimeUtc(path + ".tmp", updateTime);
            File.Move(path + ".tmp", path + Program.FILE_EXT);
        }
    }
}