// Uncomment one of the following hashset implementations to use
//#define USE_HASHSET
//#define USE_HASHSETARRAY
#define USE_BITSHAPEHASHSET
//#define USE_BITSHAPEHASHSET64K
//#define USE_CONCURRENTDICTIONARY

using System.Collections.Concurrent;
using System.Diagnostics;

namespace ShapeMaker;

#if USE_HASHSET
    #if USE_HASHSETARRAY || USE_BITSHAPEHASHSET || USE_BITSHAPEHASHSET64K || USE_CONCURRENTDICTIONARY
        #error You must only use one hashset implementation
    #endif
    using MyHashSet = HashSet<byte[]>;
#elif USE_HASHSETARRAY
    #if USE_HASHSET || USE_BITSHAPEHASHSET || USE_BITSHAPEHASHSET64K || USE_CONCURRENTDICTIONARY
        #error You must only use one hashset implementation
    #endif
    using MyHashSet = HashSet<byte[]>;
#elif USE_BITSHAPEHASHSET
    #if USE_HASHSET || USE_HASHSETARRAY || USE_BITSHAPEHASHSET64K || USE_CONCURRENTDICTIONARY
        #error You must only use one hashset implementation
    #endif
    using MyHashSet = BitShapeHashSet;
#elif USE_BITSHAPEHASHSET64K
#if USE_HASHSET || USE_HASHSETARRAY || USE_BITSHAPEHASHSET || USE_CONCURRENTDICTIONARY
#error You must only use one hashset implementation
#endif
    using MyHashSet = BitShapeHashSet64k;
#elif USE_CONCURRENTDICTIONARY
#if USE_HASHSET || USE_HASHSETARRAY || USE_BITSHAPEHASHSET || USE_BITSHAPEHASHSET64K
#error You must only use one hashset implementation
#endif
    using MyHashSet = ConcurrentDictionary<byte[], byte>;
#else
#error You must define which hashset implementation to use
#endif

public static class Program {
    public static string filePath = "~/Downloads/ShapeMaker";
    public const string FILE_EXT = ".bin";
    public const string FILE_COMPLETE = "_COMPLETE";

    private static bool doChiralCount = true;
    private static bool doForceRecompute = false;
    private const int MAX_COMPUTE_N = 19;

    /// <summary>
    /// Performs the computation to find all possible shapes of voxel count n, as well as all the unique chiral shapes.
    /// Takes a single command line argument, the path to the directory where the files will be stored.
    /// There are two options that can be passed as command line arguments:
    /// --no-chiral-count to skip the chiral count operation
    /// --force-recompute to recompute all shapes, even if they have already been computed
    /// </summary>
    static void Main(string[] args) {
        foreach (var arg in args) {
            if (arg.StartsWith("--"))
                if (arg == "--no-chiral-count")
                    doChiralCount = false;
                else if (arg == "--force-recompute")
                    doForceRecompute = true;
                else
                    throw new ArgumentException("Unrecognized parameter " + arg);
            else
                filePath = arg;
        }

        if (filePath == "~")
            filePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        else if (filePath.StartsWith("~/"))
            filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), filePath.Substring("~/".Length));

        var totalAvailableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

        bool recompute = doForceRecompute;

        string completeString = FileReader.NCompleteString(1);
        if (recompute || completeString == null) {
            if (recompute) FileWriter.Clear(1); else FileWriter.ClearTmp(1);
            using (var writer = new FileWriter(1, 1, 1, 1))
                writer.Write(new BitShape("1x1x1,*").bytes);
            FileWriter.MarkNComplete(1, doChiralCount ? "n=1, shapes: 1 time: 0, chiral count: 1 time: 0" : "n=1, shapes: 1 time: 0");
        }

        for (byte n = 2; n <= MAX_COMPUTE_N; n++) {
            completeString = FileReader.NCompleteString(n);
            if (!recompute && completeString != null) {
                Console.WriteLine(completeString);
                continue;
            }

            if (recompute) FileWriter.Clear(n); else FileWriter.ClearTmp(n);
            completeString = "n=" + n + ", shapes: ";
            Console.Write(completeString);
            Stopwatch sw = Stopwatch.StartNew();
            TimeSpan additionalTime = TimeSpan.Zero;
            var inputFileList = new FileScanner((byte)(n - 1)).List
                .OrderByDescending(f => f.size)
                .ToList();
            var targetSizes = ShapeMakerEstimator.ShapeSizesFromExtendingShapes(inputFileList).OrderByDescending(f => f.sz).ToList();
            long shapeCount = 0;
            int currentSizeIndex = 0, targetSizesCount = targetSizes.Count;
            foreach (var size in targetSizes) {
                int shardCount = 0; // don't shard
                // if the combined input size is 1GB, for example, the output is likely to be ~8GB, and ~24GB in memory
                if (n >= 14 && size is { w: > 1, h: > 1, d: > 1 }) {
#if USE_BITSHAPEHASHSET || USE_BITSHAPEHASHSET64K
                    long inMemSize = size.sz * 8; // should be *8 for BitShapeHashSet
#else
                    long inMemSize = size.sz * 8 * 3; // should be *8*3 normally
#endif
                    if (inMemSize > totalAvailableMemory) {
                        shardCount = -8; // just shard on corner count
                        if (inMemSize / 3 > totalAvailableMemory) { // unless that's not enough
                            shardCount = -n; // shard on corner/edge counts
                            if (inMemSize / 9 > totalAvailableMemory) // unless that's not enough
                                shardCount = n; // shard on corner/edge/face counts
                        }
                    }
                }
                currentSizeIndex++;
                if (FileReader.FileExists(n, size.w, size.h, size.d)) {
                    int bytesPerShape = new BitShape(size.w, size.h, size.d).bytes.Length;
                    shapeCount += FileReader.FileSize(n, size.w, size.h, size.d) / bytesPerShape;
                    TimeSpan timeTaken = FileReader.FileTime(n, size.w, size.h, size.d);
                    additionalTime += timeTaken;
                } else {
                    double totalSeconds = additionalTime.Add(sw.Elapsed).TotalSeconds;
                    var progress = "            " + (shardCount != 0 ? "/" + shardCount : "") + "[" + shapeCount.ToString("N0") + ", " + totalSeconds.ToString("N0") + "s, " + size.w + "x" + size.h + "x" + size.d + " " + currentSizeIndex + "/" + targetSizesCount + "]     ";
                    Console.Write(progress + new string('\b', progress.Length));
                    if (n < MAX_COMPUTE_N)
                        using (var writer = new FileWriter(n, size.w, size.h, size.d))
                            shapeCount += ShapeMaker.ShapesFromExtendingShapes(inputFileList, writer, size.w, size.h, size.d, shardCount);
                    else
                        shapeCount += ShapeMaker.ShapesFromExtendingShapes(inputFileList, null, size.w, size.h, size.d, shardCount);
                }
                {
                    double totalSeconds = additionalTime.Add(sw.Elapsed).TotalSeconds;
                    var progress = "            " + (shardCount != 0 ? "/" + shardCount : "") + "[" + shapeCount.ToString("N0") + ", " + totalSeconds.ToString("N0") + "s, " + size.w + "x" + size.h + "x" + size.d + " " + currentSizeIndex + "/" + targetSizesCount + "]     ";
                    Console.Write(progress + new string('\b', progress.Length));
                }
            }
            sw.Stop();
            {
                double totalSeconds = additionalTime.Add(sw.Elapsed).TotalSeconds;
                string progress = shapeCount.ToString("N0") + " time: " + totalSeconds + "      \b\b\b\b\b\b";
                completeString += progress;
                Console.Write(progress);
            }

            if (doChiralCount)
                completeString += DoChiralCount(n, targetSizes);

            Console.WriteLine();
            FileWriter.MarkNComplete(n, completeString);
        }
    }

    /// <summary>
    /// Perform a chiral count on the shapes we've found so far. Reads shapes of voxel count n from disk and checks
    /// each to see if it is the minimal chiral rotation of the shape, and if so counts that shape. This is done in
    /// parallel. Roughly half of the shapes will be chiral.
    /// </summary>
    /// <param name="n">voxel count</param>
    /// <param name="targetSizes">list of shape sizes of voxel count</param>
    /// <returns>string to print to console with count and timing</returns>
    private static string DoChiralCount(byte n, List<(byte w, byte h, byte d, long sz)> targetSizes) {
        var sw = Stopwatch.StartNew();
        long chiralCount = 0;
        int currentSizeIndex = 0;
        int targetSizesCount = targetSizes.Count;
        foreach (var size in targetSizes) {
            string progress = "     " + ++currentSizeIndex + "/" + targetSizesCount + "=" + size.w + "x" + size.h + "x" + size.d + ", " + chiralCount.ToString("N0") + ", " + sw.Elapsed.TotalSeconds.ToString("N0") + "s   ";
            Console.Write(progress + new string('\b', progress.Length));
            FileScanner.Results fileInfo = new FileScanner.Results() { n = n, w = size.w, h = size.h, d = size.d, ext = Program.FILE_EXT };
            int shapeSizeInBytes = new BitShape(size.w, size.h, size.d).bytes.Length;
            long sourceShapes = FileReader.FileSize(n, size.w, size.h, size.d) / shapeSizeInBytes;
            long sourceShapes100 = sourceShapes / 100;
            long sourceShapeCount = 0, nextShapeCount = sourceShapes100;
            int percent = 0;
            Parallel.ForEach(FileReader.LoadShapes(fileInfo), (shape) => {
                long newShapeCount = Interlocked.Increment(ref sourceShapeCount);
                if (newShapeCount == nextShapeCount) {
                    percent++;
                    nextShapeCount += sourceShapes100;
                    var progress2 = " " + percent + "%";
                    lock (fileInfo) Console.Write(progress2 + new string('\b', progress2.Length));
                }
                if (shape.IsMinChiralRotation()) Interlocked.Increment(ref chiralCount);
            });
        }
        sw.Stop();
        {
            string progress = ", chiral count: " + chiralCount.ToString("N0") + " time: " + sw.Elapsed.TotalSeconds + "    ";
            Console.Write(progress);
            return progress;
        }
    }
}

/// <summary>
/// Helps estimate the size of the shapes we will find when extending shapes. This is used so we can process shapes of
/// a target size by all the input shapes that could make that target size. Also used to determine if sharding is
/// required. Also used to help determine the order in which to process the shapes.
/// </summary> 
public static class ShapeMakerEstimator {
    public static IEnumerable<(byte w, byte h, byte d, long sz)> ShapeSizesFromExtendingShapes(IEnumerable<FileScanner.Results> inputFileList) {
        var totalFileSizeForDimensions = new Dictionary<(byte w, byte h, byte d), long>();
        foreach (var fileInfo in inputFileList)
            foreach (var size in ShapeSizesFromExtendingShapes(fileInfo))
                if (!totalFileSizeForDimensions.TryAdd((size.w, size.h, size.d), size.sz))
                    totalFileSizeForDimensions[(size.w, size.h, size.d)] += size.sz;
        return totalFileSizeForDimensions.ToList()
            .OrderBy(i => i.Key.w * 65536 + i.Key.h * 256 + i.Key.d)
            .Select(i => (i.Key.w, i.Key.h, i.Key.d, i.Value));
    }

    /// <summary>
    /// Gets the target shape sizes for a given shape size. This includes the original size, and the size of the shape
    /// if we add a cube to each of the 6 faces. This is used to determine the order in which to process the shapes.
    /// </summary>
    /// <param name="fileInfo">file info for the shape size</param>
    /// <returns>a short list (3 or 4) possible target shape sizes</returns>
    private static IEnumerable<(byte w, byte h, byte d, long sz)> ShapeSizesFromExtendingShapes(FileScanner.Results fileInfo) {
        byte n = fileInfo.n, w = fileInfo.w, h = fileInfo.h, d = fileInfo.d;
        if (n < w * h * d) yield return (w, h, d, fileInfo.size);
        var (w1, h1, d1) = ShapeMakerHelper.MinRotation((byte)(w + 1), h, d);
        yield return (w1, h1, d1, fileInfo.size);
        var (w2, h2, d2) = ShapeMakerHelper.MinRotation(w, (byte)(h + 1), d);
        yield return (w2, h2, d2, fileInfo.size);
        var (w3, h3, d3) = ShapeMakerHelper.MinRotation(w, h, (byte)(d + 1));
        yield return (w3, h3, d3, fileInfo.size);
    }
}

/// <summary>
/// Helper class to determine minimum rotation.
/// </summary>
public static class ShapeMakerHelper {
    /// <summary>
    /// Returns minimum rotation such that w is less than or equal to h and h is less than or equal to d.
    /// </summary>
    /// <param name="w">width</param>
    /// <param name="h">height</param>
    /// <param name="d">depth</param>
    /// <returns>minimum rotation</returns>
    public static (byte w, byte h, byte d) MinRotation(byte w, byte h, byte d) {
        if (w <= h && h <= d)
            return (w, h, d); // 1,2,3 - no rotation
        else if (w <= h && w <= d)
            return (w, d, h); // 1,3,2 - x
        else if (d < h && h < w)
            return (d, h, w); // 3,2,1 - y
        else if (d < h && d < w)
            return (d, w, h); // 3,1,2 - xy
        else if (w <= d)
            return (h, w, d); // 2,1,3 - z
        else
            return (h, d, w); // 2,3,1 - yx
    }
}

/// <summary>
/// Helper class to take previously found shapes and extend them to find new shapes and add them to a hashset.
/// </summary>
public static class ShapeMaker {
    /// <summary>
    /// Extend list of files of shapes to find new shapes and add them to a hashset.
    /// </summary>
    /// <param name="fileList">list of files of previous n voxel count shapes</param>
    /// <param name="writer">file writer to store new found shapes into</param>
    /// <param name="w">width</param>
    /// <param name="h">height</param>
    /// <param name="d">depth</param>
    /// <param name="shardCount">shardCount - 0 if no sharding, -8 to shard only on the 8 corners, negative to shard on
    /// corners and edges, positive to shard on corners, edges and faces.</param>
    /// <returns>shape count found for target size {width}, {height}, {depth}</returns>
    public static long ShapesFromExtendingShapes(IEnumerable<FileScanner.Results> fileList, FileWriter writer, byte w, byte h, byte d, int shardCount) {
#if USE_BITSHAPEHASHSET || USE_BITSHAPEHASHSET64K
        var newShapes = new MyHashSet((w * h * d + 7) / 8);
#elif USE_HASHSET || USE_CONCURRENTDICTIONARY
        var newShapes = new MyHashSet(ByteArrayEqualityComparer.Instance);
#elif USE_HASHSETARRAY
        var newShapes = new MyHashSet[256];
        for (int i = 0; i < 256; i++) newShapes[i] = new HashSet<byte[]>(ByteArrayEqualityComparer.Instance);
#endif

        if (shardCount == 0)
            return ShapesFromExtendingShapes(fileList, writer, newShapes, w, h, d, -1, -1, -1);

        long shapeCount = 0;
        if (shardCount < 0)
            if (shardCount == -8) // just corner count sharding
                for (int cornerIndex = 0; cornerIndex <= 8; cornerIndex++)
                    shapeCount += ShapesFromExtendingShapes(fileList, writer, newShapes, w, h, d, cornerIndex, -1, -1);
            else // just corner/edge count sharding
                for (int cornerIndex = 0; cornerIndex <= 8; cornerIndex++)
                    for (int edgeIndex = cornerIndex == 0 ? 0 : 1; edgeIndex <= -shardCount - cornerIndex; edgeIndex++)
                        shapeCount += ShapesFromExtendingShapes(fileList, writer, newShapes, w, h, d, cornerIndex, edgeIndex, -1);
        else {
            int maxInteriorCount = Math.Max(0, w - 2) * Math.Max(0, h - 2) * Math.Max(0, d - 2);
            for (int cornerIndex = 0; cornerIndex <= 8; cornerIndex++)
                for (int edgeIndex = cornerIndex == 0 ? 0 : 1; edgeIndex <= shardCount - cornerIndex; edgeIndex++)
                    for (int faceIndex = edgeIndex == 0 ? 0 : 1; faceIndex <= shardCount - cornerIndex - edgeIndex; faceIndex++)
                        if (cornerIndex + edgeIndex + faceIndex >= shardCount - maxInteriorCount)
                            shapeCount += ShapesFromExtendingShapes(fileList, writer, newShapes, w, h, d, cornerIndex, edgeIndex, faceIndex);
        }
        return shapeCount;
    }

    // for each shape in parallel, try to add cube to it
    // first does by adding cube to the shape in its current size
    // then tries padding each of the 6 faces of the shape and adding a cube there

    /// <summary>
    /// Extend list of files of shapes to find new shapes and add them to a hashset.
    /// </summary>
    /// <param name="fileList">list of files of previous n voxel count shapes</param>
    /// <param name="writer">file writer to store new found shapes into</param>
    /// <param name="newShapes">hash set to add to</param>
    /// <param name="targetWidth">target width</param>
    /// <param name="targetHeight">target height</param>
    /// <param name="targetDepth">target depth</param>
    /// <param name="targetCornerCount">target corner count for sharding (-1 if not sharding)</param>
    /// <param name="targetEdgeCount">target edge count for sharding (-1 if not sharding on edges, faces)</param>
    /// <param name="targetFaceCount">target face count for sharding (-1 if not sharding on faces)</param>
    /// <returns>shape count</returns>
#if USE_HASHSETARRAY
    private static long ShapesFromExtendingShapes(IEnumerable<FileScanner.Results> fileList, FileWriter writer, MyHashSet[] newShapes, byte targetWidth, byte targetHeight, byte targetDepth, int targetCornerCount, int targetEdgeCount, int targetFaceCount) {
#else
    private static long ShapesFromExtendingShapes(IEnumerable<FileScanner.Results> fileList, FileWriter writer, MyHashSet newShapes, byte targetWidth, byte targetHeight, byte targetDepth, int targetCornerCount, int targetEdgeCount, int targetFaceCount) {
#endif
        long shapeCount = 0;
        foreach (var fileInfo in fileList)
            ShapesFromExtendingShapes(fileInfo, newShapes, targetWidth, targetHeight, targetDepth, targetCornerCount, targetEdgeCount, targetFaceCount);
#if USE_HASHSETARRAY
        if (writer != null) foreach (var hs in newShapes) foreach (var shape in hs) writer.Write(shape);
        foreach (var hs in newShapes) { shapeCount += hs.LongCount(); hs.Clear(); }
#elif USE_CONCURRENTDICTIONARY
        if (writer != null) foreach (var shape in newShapes.Keys) writer.Write(shape);
        shapeCount += newShapes.Keys.LongCount();
        newShapes.Clear();
#else
        if (writer != null) foreach (var shape in newShapes) writer.Write(shape);
        shapeCount += newShapes.LongCount();
        newShapes.Clear();
#endif
        return shapeCount;
    }

    /// <summary>
    /// Extend file of shapes to find new shapes and add them to a hashset. Does this work in parallel.
    /// </summary>
    /// <param name="fileInfo">file of previous n voxel count shapes</param>
    /// <param name="newShapes">hash set to add to</param>
    /// <param name="targetWidth">target width</param>
    /// <param name="targetHeight">target height</param>
    /// <param name="targetDepth">target depth</param>
    /// <param name="targetCornerCount">target corner count for sharding (-1 if not sharding)</param>
    /// <param name="targetEdgeCount">target edge count for sharding (-1 if not sharding on edges, faces)</param>
    /// <param name="targetFaceCount">target face count for sharding (-1 if not sharding on faces)</param>
#if USE_HASHSETARRAY
    private static void ShapesFromExtendingShapes(FileScanner.Results fileInfo, MyHashSet[] newShapes, byte targetWidth, byte targetHeight, byte targetDepth, int targetCornerCount, int targetEdgeCount, int targetFaceCount) {
#else
    private static void ShapesFromExtendingShapes(FileScanner.Results fileInfo, MyHashSet newShapes, byte targetWidth, byte targetHeight, byte targetDepth, int targetCornerCount, int targetEdgeCount, int targetFaceCount) {
#endif
        byte w = fileInfo.w, h = fileInfo.h, d = fileInfo.d;
        int shapeSizeInBytes = new BitShape(w, h, d).bytes.Length;
        long sourceShapes = FileReader.FileSize(fileInfo.n, w, h, d) / shapeSizeInBytes;
        long sourceShapes100 = sourceShapes / 100;

        if (w == targetWidth && h == targetHeight && d == targetDepth) {
            StatusUpdate('*', targetCornerCount, targetEdgeCount, targetFaceCount);
            long sourceShapeCount = 0, nextShapeCount = sourceShapes100;
            int percent = 0;
            Parallel.ForEach(FileReader.LoadShapes(fileInfo), (shape) => {
                long newShapeCount = Interlocked.Increment(ref sourceShapeCount);
                if (newShapeCount == nextShapeCount) {
                    percent++;
                    nextShapeCount += sourceShapes100;
                    var progress = "*" + percent + "%";
                    lock (fileInfo) Console.Write(progress + new string('\b', progress.Length));
                }
                AddShapes(newShapes, shape, 0, w, 0, h, 0, d, targetCornerCount, targetEdgeCount, targetFaceCount); // unpadded
            });
        }

        var (wMin, hMin, dMin) = ShapeMakerHelper.MinRotation((byte)(w + 1), h, d);
        if (wMin == targetWidth && hMin == targetHeight && dMin == targetDepth) {
            StatusUpdate('|', targetCornerCount, targetEdgeCount, targetFaceCount);
            long sourceShapeCount = 0, nextShapeCount = sourceShapes100;
            int percent = 0;
            Parallel.ForEach(FileReader.LoadShapes(fileInfo), (shape) => {
                long newShapeCount = Interlocked.Increment(ref sourceShapeCount);
                if (newShapeCount == nextShapeCount) {
                    percent++;
                    nextShapeCount += sourceShapes100;
                    var progress = "|" + percent + "%";
                    lock (fileInfo) Console.Write(progress + new string('\b', progress.Length));
                }
                AddShapes(newShapes, shape.PadLeft(), 0, 1, 0, h, 0, d, targetCornerCount, targetEdgeCount, targetFaceCount);
                AddShapes(newShapes, shape.PadRight(), w, w + 1, 0, h, 0, d, targetCornerCount, targetEdgeCount, targetFaceCount);
            });
        }

        (wMin, hMin, dMin) = ShapeMakerHelper.MinRotation(w, (byte)(h + 1), d);
        if (wMin == targetWidth && hMin == targetHeight && dMin == targetDepth) {
            StatusUpdate('-', targetCornerCount, targetEdgeCount, targetFaceCount);
            long sourceShapeCount = 0, nextShapeCount = sourceShapes100;
            int percent = 0;
            Parallel.ForEach(FileReader.LoadShapes(fileInfo), (shape) => {
                long newShapeCount = Interlocked.Increment(ref sourceShapeCount);
                if (newShapeCount == nextShapeCount) {
                    percent++;
                    nextShapeCount += sourceShapes100;
                    var progress = "-" + percent + "%";
                    lock (fileInfo) Console.Write(progress + new string('\b', progress.Length));
                }
                AddShapes(newShapes, shape.PadTop(), 0, w, 0, 1, 0, d, targetCornerCount, targetEdgeCount, targetFaceCount);
                AddShapes(newShapes, shape.PadBottom(), 0, w, h, h + 1, 0, d, targetCornerCount, targetEdgeCount, targetFaceCount);
            });
        }

        (wMin, hMin, dMin) = ShapeMakerHelper.MinRotation(w, h, (byte)(d + 1));
        if (wMin == targetWidth && hMin == targetHeight && dMin == targetDepth) {
            StatusUpdate('/', targetCornerCount, targetEdgeCount, targetFaceCount);
            long sourceShapeCount = 0, nextShapeCount = sourceShapes100;
            int percent = 0;
            Parallel.ForEach(FileReader.LoadShapes(fileInfo), (shape) => {
                long newShapeCount = Interlocked.Increment(ref sourceShapeCount);
                if (newShapeCount == nextShapeCount) {
                    percent++;
                    nextShapeCount += sourceShapes100;
                    var progress = "/" + percent + "%";
                    lock (fileInfo) Console.Write(progress + new string('\b', progress.Length));
                }
                AddShapes(newShapes, shape.PadFront(), 0, w, 0, h, 0, 1, targetCornerCount, targetEdgeCount, targetFaceCount);
                AddShapes(newShapes, shape.PadBack(), 0, w, 0, h, d, d + 1, targetCornerCount, targetEdgeCount, targetFaceCount);
            });
        }
    }

    /// <summary>
    /// Writes shape size status to console.
    /// </summary>
    /// <param name="stepChar">step character, * for interior, - for top/bottom extension, | for left/right extension,
    /// / for front/back extension</param>
    /// <param name="targetCornerCount">target corner count for sharding (-1 if not sharding)</param>
    /// <param name="targetEdgeCount">target edge count for sharding (-1 if not sharding on edges, faces)</param>
    /// <param name="targetFaceCount">target face count for sharding (-1 if not sharding on faces)</param>
    private static void StatusUpdate(char stepChar, int targetCornerCount, int targetEdgeCount, int targetFaceCount) {
        Console.Write(stepChar + "    " + (targetCornerCount >= 0 ? targetCornerCount + "," + Pad2(targetEdgeCount) + "," + Pad2(targetFaceCount) + "\b\b\b\b\b\b\b\b\b\b\b\b" : "\b\b\b\b\b"));
    }

    /// <summary>
    /// Pads a number with a leading 0 if it is less than 10.
    /// </summary>
    /// <param name="value">number to pad</param>
    /// <returns>string of number zero-padded to be at least two characters</returns>
    private static string Pad2(int value) => value is >= 0 and <= 9 ? "0" + value.ToString() : value.ToString();

    /// <summary>
    /// Adds new possible shapes to hash set. For each blank voxel in range, if it has an adjacent neighbor, add that
    /// voxel, find the minimum rotation and add to the hash set. This method is expected to be called in parallel,
    /// so any operations done in here must be thread-safe, especially adding to the hash set.
    /// </summary>
    /// <param name="newShapes">hash set to add to</param>
    /// <param name="shape">starting shape</param>
    /// <param name="xStart">starting x coordinate to try extending</param>
    /// <param name="w">width on x coordinate to try extending</param>
    /// <param name="yStart">starting y coordinate to try extending</param>
    /// <param name="h">height on y coordinate to try extending</param>
    /// <param name="zStart">starting z coordinate to try extending</param>
    /// <param name="d">depth of z coordinate to try extending</param>
    /// <param name="targetCornerCount">target corner count for sharding (-1 if not sharding)</param>
    /// <param name="targetEdgeCount">target edge count for sharding (-1 if not sharding on edges, faces)</param>
    /// <param name="targetFaceCount">target face count for sharding (-1 if not sharding on faces)</param>
#if USE_HASHSETARRAY
    private static void AddShapes(MyHashSet[] newShapes, BitShape shape, int xStart, int w, int yStart, int h, int zStart, int d, int targetCornerCount, int targetEdgeCount, int targetFaceCount) {
#else
    private static void AddShapes(MyHashSet newShapes, BitShape shape, int xStart, int w, int yStart, int h, int zStart, int d, int targetCornerCount, int targetEdgeCount, int targetFaceCount) {
#endif
        int cornerCount = targetCornerCount, edgeCount = targetEdgeCount, faceCount = targetFaceCount;
        if (targetCornerCount >= 0)
            if (targetEdgeCount < 0) {
                cornerCount = shape.CornerCount();
                if (cornerCount > targetCornerCount || (cornerCount + 1) < targetCornerCount) return;
            } else if (targetFaceCount < 0) {
                (cornerCount, edgeCount) = shape.CornerEdgeCount();
                if (cornerCount > targetCornerCount || (cornerCount + 1) < targetCornerCount) return;
                if (edgeCount > targetEdgeCount || (edgeCount + 1) < targetEdgeCount) return;
            } else {
                (cornerCount, edgeCount, faceCount) = shape.CornerEdgeFaceCount();
                if (cornerCount > targetCornerCount || (cornerCount + 1) < targetCornerCount) return;
                if (edgeCount > targetEdgeCount || (edgeCount + 1) < targetEdgeCount) return;
                if (faceCount > targetFaceCount || (faceCount + 1) < targetFaceCount) return;
            }
        var newShape = new BitShape(shape);
        var newShapeBytes = newShape.bytes;
        var shapeBytes = shape.bytes;
        var shapeBytesLength = shapeBytes.Length;
        int xLimit = shape.w - 1, yLimit = shape.h - 1, zLimit = shape.d - 1;
        for (var x = xStart; x < w; x++) {
            bool xFace = x == 0 || x == xLimit;
            for (var y = yStart; y < h; y++) {
                bool yFace = y == 0 || y == yLimit;
                for (var z = zStart; z < d; z++) {
                    if (targetCornerCount >= 0) {
                        bool zFace = z == 0 || z == zLimit;
                        bool isInterior = !xFace && !yFace && !zFace;
                        if (isInterior && (targetCornerCount != cornerCount || targetEdgeCount != edgeCount || targetFaceCount != faceCount)) continue;
                        bool isCorner = xFace && yFace && zFace;
                        if (isCorner && targetCornerCount != cornerCount + 1) continue;
                        if (targetEdgeCount >= 0) {
                            bool isEdge = xFace && yFace || yFace && zFace || xFace && zFace;
                            if (isEdge && targetEdgeCount != edgeCount + 1) continue;
                            if (targetFaceCount >= 0) {
                                bool isFace = !isCorner && !isEdge && !isInterior;
                                if (isFace && targetFaceCount != faceCount + 1) continue;
                            }
                        }
                    }
                    if (!shape[x, y, z])
                        if (shape.HasSetNeighbor(x, y, z)) {
                            Array.Copy(shapeBytes, newShapeBytes, shapeBytesLength);
                            newShape[x, y, z] = true;
                            var bytes = newShape.MinRotation().bytes;
#if USE_HASHSET
                            lock (newShapes) newShapes.Add(bytes);
#elif USE_BITSHAPEHASHSET || USE_BITSHAPEHASHSET64K
                            newShapes.Add(bytes);
#elif USE_CONCURRENTDICTIONARY
                            newShapes.TryAdd(bytes, 0);
#elif USE_HASHSETARRAY
                            int len = bytes.Length; byte hashIndex = len < 3 ? bytes[0] : bytes[len - 2]; // use last full byte
                            lock (newShapes[hashIndex]) newShapes[hashIndex].Add(bytes);
#endif
                        }
                }
            }
        }
    }
}

/// <summary>
/// Byte array equality comparer. Used to compare byte arrays for equality and to get a hash code for a byte array.
/// Used to allow HashSet to store byte arrays by using .Instance as the comparer.
/// </summary>
public class ByteArrayEqualityComparer : IEqualityComparer<byte[]> {
    /// <summary>
    /// Byte array equality comparer instance. Used when creating a HashSet to store byte arrays.
    /// </summary>
    public static readonly ByteArrayEqualityComparer Instance = new ByteArrayEqualityComparer();

    bool IEqualityComparer<byte[]>.Equals(byte[]? x, byte[]? y) {
        return x is not null && y is not null && x.SequenceEqual(y);
    }

    int IEqualityComparer<byte[]>.GetHashCode(byte[] obj) {
        unchecked { // Modified FNV Hash
            const int p = 16777619;
            int hash = (int)2166136261;

            for (int i = 0, l = obj.Length; i < l; i++)
                hash = (hash ^ obj[i]) * p;

            return hash;
        }
    }
}

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
        public string filepath => FileReader.FilePath(n, w, h, d, ext);

        /// <summary>
        /// file size in bytes
        /// </summary>
        public long size;
    }

    /// <summary>
    /// List of results of a file scan.
    /// </summary>
    public readonly List<Results> List = new();

    /// <summary>
    /// Create and scan a directory for shape files of a given voxel count and extension. Read results in .List.
    /// </summary>
    /// <param name="n">voxel count</param>
    /// <param name="ext">file extension (defaults to .bin)</param>
    public FileScanner(byte n, string ext = Program.FILE_EXT) {
        var di = new DirectoryInfo(Path.Combine(Program.filePath, n.ToString()));

        // scan for and migrate old files
        var renameList = new List<(string, string)>();
        foreach (var file in di.GetFiles("*" + ext)) {
            if (!file.Name.EndsWith(ext)) continue;
            var dim = file.Name.Substring(0, file.Name.Length - ext.Length).Split(',');
            if (dim.Length != 3) continue;
            if (!byte.TryParse(dim[0], out var w) || w < 1 || w > n) continue;
            if (!byte.TryParse(dim[1], out var h) || h < 1 || h > n) continue;
            if (!byte.TryParse(dim[2], out var d) || d < 1 || d > n) continue;
            var newDim = string.Join('x', dim);
            renameList.Add((file.Name, newDim + ext));
        }
        foreach (var (oldname, newname) in renameList) {
            var oldpath = Path.Combine(Program.filePath, n.ToString(), oldname);
            var newpath = Path.Combine(Program.filePath, n.ToString(), newname);
            File.Move(oldpath, newpath);
        }

        // scan for new files
        foreach (var file in di.GetFiles("*" + ext)) {
            if (!file.Name.EndsWith(ext)) continue;
            var dim = file.Name.Substring(0, file.Name.Length - ext.Length).Split('x');
            if (dim.Length != 3) continue;
            if (!byte.TryParse(dim[0], out var w) || w < 1 || w > n) continue;
            if (!byte.TryParse(dim[1], out var h) || h < 1 || h > n) continue;
            if (!byte.TryParse(dim[2], out var d) || d < 1 || d > n) continue;
            List.Add(new Results() { n = n, w = w, h = h, d = d, ext = ext, size = file.Length });
        }
    }
}

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
        var di = new DirectoryInfo(Path.Combine(Program.filePath, n.ToString()));
        if (!di.Exists)
            di.Create();
        else {
            var list = new FileScanner(n).List;
            foreach (var f in list)
                File.Delete(f.filepath);
            var tmpList = new FileScanner(n, ".tmp").List;
            foreach (var f in tmpList)
                File.Delete(f.filepath);
            var completePath = Path.Combine(Program.filePath, n.ToString(), Program.FILE_COMPLETE);
            if (File.Exists(completePath))
                File.Delete(completePath);
        }
    }

    /// <summary>
    /// Clears all temporary shape files for a given voxel count.
    /// </summary>
    /// <param name="n">voxel count</param>
    public static void ClearTmp(byte n) {
        var di = new DirectoryInfo(Path.Combine(Program.filePath, n.ToString()));
        if (!di.Exists)
            di.Create();
        else
            foreach (var f in new FileScanner(n, ".tmp").List)
                File.Delete(f.filepath);
    }

    /// <summary>
    /// Stores a file that indicates that all shapes of a given voxel count have been found. File contains a string
    /// that indicates the count and timing for the operation.
    /// </summary>
    /// <param name="n">voxel count</param>
    /// <param name="s">string containing the count and timing for the operation</param>
    public static void MarkNComplete(int n, string s) {
        var path = Path.Combine(Program.filePath, n.ToString(), Program.FILE_COMPLETE);
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
        if (shape.Length != length) throw new ArgumentOutOfRangeException(nameof(shape), shape.Length, "unexpected shape length - should be " + length);
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
    public static string FilePath(int n, int w, int h, int d, string ext = Program.FILE_EXT) => Path.Combine(Program.filePath, n.ToString(), w + "x" + h + "x" + d + ext);

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
    public static string NCompleteString(int n) {
        var path = Path.Combine(Program.filePath, n.ToString(), Program.FILE_COMPLETE);
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
        for (; ; ) {
            var bytes = reader.Read();
            if (bytes == null) break;
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
    public byte[] Read() {
        byte[] bytes = new byte[length];
        if (fs.Read(bytes) < length) return null;
        return bytes;
    }

    /// <summary>
    /// Closes the file reader.
    /// </summary>
    public void Dispose() {
        fs.Dispose();
    }
}
