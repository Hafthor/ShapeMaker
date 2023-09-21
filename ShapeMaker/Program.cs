// Uncomment one of the following hashset implementations to use
//#define USE_HASHSET               // works fast at the beginning, but slows down as it gets bigger, uses more memory
//#define USE_HASHSETARRAY          // 256 hashsets, works fast overall, but uses more memory
#define USE_BITSHAPEHASHSET       // works slower overall, but uses less memory, 16M buckets
//#define USE_BITSHAPEHASHSET64K    // works slower overall, but uses less memory - not as good for large n, 64K buckets
//#define USE_CONCURRENTDICTIONARY  // works fast overall, but uses more memory

#if USE_CONCURRENTDICTIONARY
using System.Collections.Concurrent;
#endif
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

/// <summary>
/// Main program class. Kinda has more than it ideally should, but this is mostly because we want to be able
/// to use the #define to switch between hashset implementations.
/// </summary>
public static class Program {
    public const string FILE_EXT = ".bin";
    public const string FILE_COMPLETE = "_COMPLETE";

    public static string filePath = "~/Downloads/ShapeMaker";

    private static bool doChiralCount = true;
    private static bool doForceRecompute = false;
    private static int maxComputeN = 19;

    /// <summary>
    /// Performs the computation to find all possible shapes of voxel count n, as well as all the unique chiral shapes.
    /// Takes a single un-optioned command line argument, the path to the directory where the files will be stored.
    /// There are three options that can be passed as command line arguments:
    /// --no-chiral-count to skip the chiral count operation
    /// --force-recompute to recompute all shapes, even if they have already been computed
    /// --max-compute [n] to compute up to n voxel count shapes
    /// </summary>
    static void Main(string[] args) {
        // parse command line options
        bool getMaxComputeNext = false;
        foreach (var arg in args) {
            if (getMaxComputeNext) {
                maxComputeN = int.Parse(arg);
                getMaxComputeNext = false;
            } else if (arg.StartsWith("--"))
                if (arg == "--no-chiral-count")
                    doChiralCount = false;
                else if (arg == "--force-recompute")
                    doForceRecompute = true;
                else if (arg == "--max-compute")
                    getMaxComputeNext = true;
                else
                    throw new ArgumentException("Unrecognized parameter " + arg);
            else
                filePath = arg;
        }
        if (getMaxComputeNext) 
            throw new ArgumentException("Missing parameter for --max-compute");

        if (filePath == "~")
            filePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        else if (filePath.StartsWith("~/"))
            filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), filePath.Substring("~/".Length));

        var totalAvailableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

        // compute shape for n=1 to get started
        string? completeString = FileReader.NCompleteString(1);
        if (doForceRecompute || completeString == null) {
            if (doForceRecompute) 
                FileWriter.Clear(1);
            else 
                FileWriter.ClearTmp(1);
            using (var writer = new FileWriter(1, 1, 1, 1))
                writer.Write(new BitShape("1x1x1,*").bytes);
            FileWriter.MarkNComplete(1, doChiralCount ? "n=1, shapes: 1 time: 0, chiral count: 1 time: 0" : "n=1, shapes: 1 time: 0");
        }

        for (byte n = 2; n <= maxComputeN; n++) {
            completeString = FileReader.NCompleteString(n);
            if (!doForceRecompute && completeString != null) {
                Console.WriteLine(completeString);
                continue;
            }

            if (doForceRecompute) 
                FileWriter.Clear(n);
            else 
                FileWriter.ClearTmp(n);
            completeString = "n=" + n + ", shapes: ";
            Console.Write(completeString);
            Stopwatch sw = Stopwatch.StartNew();
            TimeSpan additionalTime = TimeSpan.Zero;
            var inputFileList = new FileScanner((byte)(n - 1)).List
                .OrderByDescending(f => f.size)
                .ToList();
            var targetSizes = ShapeMakerEstimator.ShapeSizesFromExtendingShapes(inputFileList)
                .OrderByDescending(f => f.sz)
                .ToList();
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
                    var progress = "            " + (shardCount != 0 ? "/" + shardCount : "") + 
                                   "[" + shapeCount.ToString("N0") + ", " + totalSeconds.ToString("N0") + "s, " + 
                                   size.w + "x" + size.h + "x" + size.d + " " + currentSizeIndex + "/" + targetSizesCount + "]     ";
                    ConsoleWriteWithBackspace(progress);
                    if (n < maxComputeN)
                        using (var writer = new FileWriter(n, size.w, size.h, size.d))
                            shapeCount += ShapesFromExtendingShapes(inputFileList, writer, size, shardCount);
                    else
                        shapeCount += ShapesFromExtendingShapes(inputFileList, null, size, shardCount);
                }
                {
                    double totalSeconds = additionalTime.Add(sw.Elapsed).TotalSeconds;
                    var progress = "            " + (shardCount != 0 ? "/" + shardCount : "") + 
                                   "[" + shapeCount.ToString("N0") + ", " + totalSeconds.ToString("N0") + "s, " + 
                                   size.w + "x" + size.h + "x" + size.d + " " + currentSizeIndex + "/" + targetSizesCount + "]     ";
                    ConsoleWriteWithBackspace(progress);
                }
            }
            sw.Stop();
            {
                double totalSeconds = additionalTime.Add(sw.Elapsed).TotalSeconds;
                string progress = shapeCount.ToString("N0") + " time: " + totalSeconds;
                completeString += progress;
                Console.Write(progress);
                ConsoleWriteWithBackspace("      ");
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
    /// <returns>string to write to complete file with count and timing</returns>
    private static string DoChiralCount(byte n, List<(byte w, byte h, byte d, long sz)> targetSizes) {
        var sw = Stopwatch.StartNew();
        long chiralCount = 0;
        int currentSizeIndex = 0;
        int targetSizesCount = targetSizes.Count;
        foreach (var size in targetSizes) {
            string progress = "     " + ++currentSizeIndex + "/" + targetSizesCount + "=" + 
                              size.w + "x" + size.h + "x" + size.d + ", " + chiralCount.ToString("N0") + ", " + 
                              sw.Elapsed.TotalSeconds.ToString("N0") + "s   ";
            ConsoleWriteWithBackspace(progress);
            FileScanner.Results fileInfo = new FileScanner.Results() { n = n, w = size.w, h = size.h, d = size.d, ext = Program.FILE_EXT };
            int shapeSizeInBytes = new BitShape(size.w, size.h, size.d).bytes.Length;
            long sourceShapes = FileReader.FileSize(n, size.w, size.h, size.d) / shapeSizeInBytes;
            long sourceShapes100 = sourceShapes / 100;
            long sourceShapeCount = 0, nextShapeCount = sourceShapes100;
            int percent = 0;
            Parallel.ForEach(FileReader.LoadShapes(fileInfo), (shape) => {
                if (Interlocked.Increment(ref sourceShapeCount) == nextShapeCount) {
                    nextShapeCount += sourceShapes100;
                    ConsoleWriteWithBackspace(" " + ++percent + "%");
                }
                if (shape.IsMinChiralRotation())
                    Interlocked.Increment(ref chiralCount);
            });
        }
        sw.Stop();
        {
            string progress = ", chiral count: " + chiralCount.ToString("N0") + " time: " + sw.Elapsed.TotalSeconds;
            Console.Write(progress + "    ");
            return progress;
        }
    }

    /// <summary>
    /// Extend list of files of shapes to find new shapes and add them to a hashset.
    /// </summary>
    /// <param name="fileList">list of files of previous n voxel count shapes</param>
    /// <param name="writer">file writer to store new found shapes into</param>
    /// <param name="size">size of shapes to extend (width, height, depth)</param>
    /// <param name="shardCount">shardCount - 0 if no sharding, -8 to shard only on the 8 corners, negative to shard on
    /// corners and edges, positive to shard on corners, edges and faces.</param>
    /// <returns>shape count found for target size {width}, {height}, {depth}</returns>
    private static long ShapesFromExtendingShapes(IList<FileScanner.Results> fileList, FileWriter? writer, (byte w, byte h, byte d, long _) size, int shardCount) {
#if USE_BITSHAPEHASHSET || USE_BITSHAPEHASHSET64K
        var newShapes = new MyHashSet((size.w * size.h * size.d + 7) / 8);
#elif USE_HASHSET || USE_CONCURRENTDICTIONARY
        var newShapes = new MyHashSet(ByteArrayEqualityComparer.Instance);
#elif USE_HASHSETARRAY
        var newShapes = new MyHashSet[256];
        for (int i = 0; i < 256; i++) 
            newShapes[i] = new HashSet<byte[]>(ByteArrayEqualityComparer.Instance);
#endif

        if (shardCount == 0) // no sharding
            return ShapesFromExtendingShapes(fileList, writer, newShapes, size.w, size.h, size.d, -1, -1, -1);

        long shapeCount = 0;
        if (shardCount < 0)
            if (shardCount == -8) // just corner count sharding
                for (int cornerIndex = 0; cornerIndex <= 8; cornerIndex++)
                    shapeCount += ShapesFromExtendingShapes(fileList, writer, newShapes, size.w, size.h, size.d, cornerIndex, -1, -1);
            else // just corner/edge count sharding
                for (int cornerIndex = 0; cornerIndex <= 8; cornerIndex++)
                    for (int edgeIndex = cornerIndex == 0 ? 0 : 1; edgeIndex <= -shardCount - cornerIndex; edgeIndex++)
                        shapeCount += ShapesFromExtendingShapes(fileList, writer, newShapes, size.w, size.h, size.d, cornerIndex, edgeIndex, -1);
        else { // corner/edge/face count sharding
            int maxInteriorCount = Math.Max(0, size.w - 2) * Math.Max(0, size.h - 2) * Math.Max(0, size.d - 2);
            for (int cornerIndex = 0; cornerIndex <= 8; cornerIndex++)
                for (int edgeIndex = cornerIndex == 0 ? 0 : 1; edgeIndex <= shardCount - cornerIndex; edgeIndex++)
                    for (int faceIndex = edgeIndex == 0 ? 0 : 1; faceIndex <= shardCount - cornerIndex - edgeIndex; faceIndex++)
                        if (cornerIndex + edgeIndex + faceIndex >= shardCount - maxInteriorCount)
                            shapeCount += ShapesFromExtendingShapes(fileList, writer, newShapes, size.w, size.h, size.d, cornerIndex, edgeIndex, faceIndex);
        }
        return shapeCount;
    }

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
    private static long ShapesFromExtendingShapes(IEnumerable<FileScanner.Results> fileList, FileWriter? writer, MyHashSet newShapes, byte targetWidth, byte targetHeight, byte targetDepth, int targetCornerCount, int targetEdgeCount, int targetFaceCount) {
#endif
        long shapeCount = 0;
        foreach (var fileInfo in fileList)
            ShapesFromExtendingShapes(fileInfo, newShapes, targetWidth, targetHeight, targetDepth, targetCornerCount, targetEdgeCount, targetFaceCount);
#if USE_HASHSETARRAY
        if (writer != null) 
            foreach (var hs in newShapes) 
                foreach (var shape in hs) 
                    writer.Write(shape);
        foreach (var hs in newShapes) {
            shapeCount += hs.LongCount(); 
            hs.Clear();
        }
#elif USE_CONCURRENTDICTIONARY
        if (writer != null) 
            foreach (var shape in newShapes.Keys)
                writer.Write(shape);
        shapeCount += newShapes.Keys.LongCount();
        newShapes.Clear();
#else
        if (writer != null) 
            foreach (var shape in newShapes) 
                writer.Write(shape);
        shapeCount += newShapes.LongCount();
        newShapes.Clear();
#endif
        return shapeCount;
    }

    /// <summary>
    /// Extend file of shapes to find new shapes and add them to a hashset. For each shape in parallel, try to
    /// add voxel to it. First does by adding voxel to the shape in its current size, then tries padding each
    /// of the 6 faces of the shape and adding a voxel there.
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
            // target shape size is same as source shape size, so we are just adding a voxel to the shape
            StatusUpdate('*', targetCornerCount, targetEdgeCount, targetFaceCount);
            long sourceShapeCount = 0, nextShapeCount = sourceShapes100;
            int percent = 0;
            Parallel.ForEach(FileReader.LoadShapes(fileInfo), (shape) => {
                if (Interlocked.Increment(ref sourceShapeCount) == nextShapeCount) {
                    nextShapeCount += sourceShapes100;
                    ConsoleWriteWithBackspace("*" + ++percent + "%");
                }
                AddShapes(newShapes, shape, 0, w, 0, h, 0, d, targetCornerCount, targetEdgeCount, targetFaceCount);
            });
        }

        var (wMin, hMin, dMin) = ShapeMakerHelper.MinRotation((byte)(w + 1), h, d);
        if (wMin == targetWidth && hMin == targetHeight && dMin == targetDepth) {
            // target shape size is one voxel wider than source shape size, so we are adding a layer on the left and right and adding a voxel to that layer
            StatusUpdate('|', targetCornerCount, targetEdgeCount, targetFaceCount);
            long sourceShapeCount = 0, nextShapeCount = sourceShapes100;
            int percent = 0;
            Parallel.ForEach(FileReader.LoadShapes(fileInfo), (shape) => {
                if (Interlocked.Increment(ref sourceShapeCount) == nextShapeCount) {
                    nextShapeCount += sourceShapes100;
                    ConsoleWriteWithBackspace("|" + ++percent + "%");
                }
                AddShapes(newShapes, shape.PadLeft(), 0, 1, 0, h, 0, d, targetCornerCount, targetEdgeCount, targetFaceCount);
                AddShapes(newShapes, shape.PadRight(), w, w + 1, 0, h, 0, d, targetCornerCount, targetEdgeCount, targetFaceCount);
            });
        }

        (wMin, hMin, dMin) = ShapeMakerHelper.MinRotation(w, (byte)(h + 1), d);
        if (wMin == targetWidth && hMin == targetHeight && dMin == targetDepth) {
            // target shape size is one voxel taller than source shape size, so we are adding a layer on the top and bottom and adding a voxel to that layer
            StatusUpdate('-', targetCornerCount, targetEdgeCount, targetFaceCount);
            long sourceShapeCount = 0, nextShapeCount = sourceShapes100;
            int percent = 0;
            Parallel.ForEach(FileReader.LoadShapes(fileInfo), (shape) => {
                if (Interlocked.Increment(ref sourceShapeCount) == nextShapeCount) {
                    nextShapeCount += sourceShapes100;
                    ConsoleWriteWithBackspace("-" + ++percent + "%");
                }
                AddShapes(newShapes, shape.PadTop(), 0, w, 0, 1, 0, d, targetCornerCount, targetEdgeCount, targetFaceCount);
                AddShapes(newShapes, shape.PadBottom(), 0, w, h, h + 1, 0, d, targetCornerCount, targetEdgeCount, targetFaceCount);
            });
        }

        (wMin, hMin, dMin) = ShapeMakerHelper.MinRotation(w, h, (byte)(d + 1));
        if (wMin == targetWidth && hMin == targetHeight && dMin == targetDepth) {
            // target shape size is one voxel deeper than source shape size, so we are adding a layer on the front and back and adding a voxel to that layer
            StatusUpdate('/', targetCornerCount, targetEdgeCount, targetFaceCount);
            long sourceShapeCount = 0, nextShapeCount = sourceShapes100;
            int percent = 0;
            Parallel.ForEach(FileReader.LoadShapes(fileInfo), (shape) => {
                if (Interlocked.Increment(ref sourceShapeCount) == nextShapeCount) {
                    nextShapeCount += sourceShapes100;
                    ConsoleWriteWithBackspace("/" + ++percent + "%");
                }
                AddShapes(newShapes, shape.PadFront(), 0, w, 0, h, 0, 1, targetCornerCount, targetEdgeCount, targetFaceCount);
                AddShapes(newShapes, shape.PadBack(), 0, w, 0, h, d, d + 1, targetCornerCount, targetEdgeCount, targetFaceCount);
            });
        }
    }

    /// <summary>
    /// Writes to console, but returning the cursor to the previous position.
    /// </summary>
    /// <param name="text">text to write to console</param>
    private static void ConsoleWriteWithBackspace(string text) {
        lock (Console.Out) {
            Console.Write(text);
            Console.Write(new string('\b', text.Length));
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
        ConsoleWriteWithBackspace(stepChar + "    " + (targetCornerCount >= 0 ? targetCornerCount + "," + Pad2(targetEdgeCount) + "," + Pad2(targetFaceCount) : ""));
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
        if (targetCornerCount >= 0) // if sharding
            if (targetEdgeCount < 0) { // if sharding on corners only
                cornerCount = shape.CornerCount();
                if (cornerCount > targetCornerCount || (cornerCount + 1) < targetCornerCount)
                    return;
            } else if (targetFaceCount < 0) { // if sharding on corners and edges
                (cornerCount, edgeCount) = shape.CornerEdgeCount();
                if (cornerCount > targetCornerCount || (cornerCount + 1) < targetCornerCount ||
                    edgeCount > targetEdgeCount || (edgeCount + 1) < targetEdgeCount) 
                    return;
            } else { // if sharding on corners, edges and faces
                (cornerCount, edgeCount, faceCount) = shape.CornerEdgeFaceCount();
                if (cornerCount > targetCornerCount || (cornerCount + 1) < targetCornerCount ||
                    edgeCount > targetEdgeCount || (edgeCount + 1) < targetEdgeCount ||
                    faceCount > targetFaceCount || (faceCount + 1) < targetFaceCount)
                    return;
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
                    if (targetCornerCount >= 0) { // if sharding
                        bool zFace = z == 0 || z == zLimit;
                        bool isInterior = !xFace && !yFace && !zFace;
                        if (isInterior && (targetCornerCount != cornerCount || targetEdgeCount != edgeCount || targetFaceCount != faceCount)) 
                            continue;
                        bool isCorner = xFace && yFace && zFace;
                        if (isCorner && (targetCornerCount != cornerCount + 1 || targetEdgeCount != edgeCount || targetFaceCount != faceCount))
                            continue;
                        if (targetEdgeCount >= 0) { // if sharding on edges
                            bool isEdge = xFace && yFace || yFace && zFace || xFace && zFace;
                            if (isEdge && (targetCornerCount != cornerCount || targetEdgeCount != edgeCount + 1 || targetFaceCount != faceCount)) 
                                continue;
                            if (targetFaceCount >= 0) { // if sharding on faces
                                bool isFace = !isCorner && !isEdge && !isInterior;
                                if (isFace && (targetCornerCount != cornerCount || targetEdgeCount != edgeCount || targetFaceCount != faceCount + 1)) 
                                    continue;
                            }
                        }
                    }
                    if (!shape[x, y, z] && shape.HasSetNeighbor(x, y, z)) {
                        Array.Copy(shapeBytes, newShapeBytes, shapeBytesLength);
                        newShape[x, y, z] = true;
                        var bytes = newShape.MinRotation().bytes;
#if USE_HASHSET
                        lock (newShapes) 
                            newShapes.Add(bytes);
#elif USE_BITSHAPEHASHSET || USE_BITSHAPEHASHSET64K
                        newShapes.Add(bytes);
#elif USE_CONCURRENTDICTIONARY
                        newShapes.TryAdd(bytes, 0);
#elif USE_HASHSETARRAY
                        int len = bytes.Length; 
                        byte hashIndex = len < 3 ? bytes[0] : bytes[len - 2]; // use last full byte
                        lock (newShapes[hashIndex]) 
                            newShapes[hashIndex].Add(bytes);
#endif
                    }
                }
            }
        }
    }
}