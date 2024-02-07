namespace ShapeMaker;

/// <summary>
/// Class to read shapes from a file.
/// </summary>
public class FileReader : IDisposable {
    private readonly FileStream fs;
    private readonly int length;

    /// <summary>
    /// Helper method to create a file path for a given voxel count and shape size.
    /// </summary>
    /// <param name="n">voxel count</param>
    /// <param name="w">width</param>
    /// <param name="h">height</param>
    /// <param name="d">depth</param>
    /// <param name="ext">file extension</param>
    /// <returns>file path</returns>
    public static string FilePath(int n, int w, int h, int d, string ext = Program.FILE_EXT) => Path.Combine(Program.options.filePath, n.ToString(), w + "x" + h + "x" + d + ext);

    /// <summary>
    /// Helper method to determine if a file exists for a given voxel count and shape size.
    /// </summary>
    /// <param name="n">voxel count</param>
    /// <param name="w">width</param>
    /// <param name="h">height</param>
    /// <param name="d">depth</param>
    /// <returns>true if file exists</returns>
    public static bool FileExists(int n, int w, int h, int d) => File.Exists(FilePath(n, w, h, d));

    /// <summary>
    /// Helper method to get file size for a given voxel count and shape size.
    /// </summary>
    /// <param name="n">voxel count</param>
    /// <param name="w">width</param>
    /// <param name="h">height</param>
    /// <param name="d">depth</param>
    /// <returns>file size in bytes or -1 if file does not exist</returns>
    public static long FileSize(int n, int w, int h, int d) {
        var fi = new FileInfo(FilePath(n, w, h, d));
        if (!fi.Exists) return -1;
        return fi.Length;
    }

    /// <summary>
    /// Helper method to get the difference between the last write time and the creation time for a file of given voxel
    /// count and shape size.
    /// </summary>
    /// <param name="n">voxel count</param>
    /// <param name="w">width</param>
    /// <param name="h">height</param>
    /// <param name="d">depth</param>
    /// <returns>TimeSpan of how long it took to generate this file</returns>
    public static TimeSpan FileTime(int n, int w, int h, int d) {
        var fi = new FileInfo(FilePath(n, w, h, d));
        if (!fi.Exists) return TimeSpan.Zero;
        return fi.LastWriteTimeUtc - fi.CreationTimeUtc;
    }

    /// <summary>
    /// Helper method to read the content of a file that indicates the shapes of a given voxel count have been found.
    /// </summary>
    /// <param name="n">voxel count</param>
    /// <returns>a string with the shape count and timing for a given voxel count or null if not complete yet</returns>
    public static string? NCompleteString(int n) {
        var path = Path.Combine(Program.options.filePath, n.ToString(), Program.FILE_COMPLETE);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>
    /// Helper method to load shapes from a file.
    /// </summary>
    /// <param name="fileInfo">file to load</param>
    /// <returns>a sequence of BitShapes from file</returns>
    public static IEnumerable<BitShape> LoadShapes(FileScanner.Results fileInfo) {
        byte n = fileInfo.n, w = fileInfo.w, h = fileInfo.h, d = fileInfo.d;
        using var reader = new FileReader(n, w, h, d);
        for (;;) {
            var bytes = reader.Read();
            if (bytes is null) break;
            yield return new BitShape(w, h, d, bytes);
        }
    }

    /// <summary>
    /// Creates a new file reader for a given voxel count and shape size.
    /// </summary>
    /// <param name="n">voxel count</param>
    /// <param name="w">width</param>
    /// <param name="h">height</param>
    /// <param name="d">depth</param>
    private FileReader(int n, int w, int h, int d) {
        fs = new FileStream(FilePath(n, w, h, d), FileMode.Open, FileAccess.Read, FileShare.None, 65536, FileOptions.None);
        length = new BitShape((byte)w, (byte)h, (byte)d).bytes.Length;
    }

    /// <summary>
    /// Reads a shape from the file. This is just the shape contents and does not include the size.
    /// </summary>
    /// <returns>shape contents byte array</returns>
    private byte[]? Read() {
        byte[] bytes = new byte[length];
        return fs.Read(bytes) < length ? null : bytes;
    }

    /// <summary>
    /// Closes the file reader.
    /// </summary>
    public void Dispose() {
        fs.Dispose();
    }
}