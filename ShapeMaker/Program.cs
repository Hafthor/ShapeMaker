using System.Diagnostics;

namespace ShapeMaker;

/// <summary>
/// Main program class.
/// </summary>
public static class Program {
    public const string FILE_EXT = ".bin";
    public const string FILE_COMPLETE = "_COMPLETE";

    public static ShapeMakerOptions options = new();

    /// <summary>
    /// Performs the computation to find all possible shapes of voxel count n, as well as all the mirror unique shapes.
    /// </summary>
    static int Main(string[] args) {
        int exitCode = ShapeMakerOptions.ParseCommandLineOptions(args, ref options);
        if (exitCode >= 0) return exitCode;

        var totalAvailableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

        // compute shape for n=1 to get started
        string? completeString = FileReader.NCompleteString(1);
        if (options.doForceRecompute || completeString is null) {
            if (options.doForceRecompute)
                FileWriter.Clear(1);
            else
                FileWriter.ClearTmp(1);
            using (var writer = new FileWriter(1, 1, 1, 1))
                writer.Write(new BitShape("1x1x1,*").bytes);
            FileWriter.MarkNComplete(1, options.doMirrorCount ? "n=1, shapes: 1 time: 0, mirror count: 1 time: 0" : "n=1, shapes: 1 time: 0");
        }

        for (byte n = 2; n <= options.maxComputeN; n++) {
            completeString = FileReader.NCompleteString(n);
            if (!options.doForceRecompute && completeString is not null) {
                Console.WriteLine(completeString);
                continue;
            }

            if (options.doForceRecompute)
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
            long shapeCount = 0, mirrorCount = 0;
            int currentSizeIndex = 0, targetSizesCount = targetSizes.Count;
            foreach (var size in targetSizes) {
                int shardCount = 0; // don't shard
                // if the combined input size is 1mil, for example, the output is likely to be ~8mil
                if (n >= 14 && size is { w: > 1, h: > 1, d: > 1 }) {
                    long inMemSize = size.sz * 8; // should be *8 for next size
                    if (options.hashSetAlgorithm is HashSetAlgorithm.Dictionary or HashSetAlgorithm.HashSet or HashSetAlgorithm.HashSet256)
                        inMemSize *= 2; // these implementations use ~2x memory
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
                    if (n < options.maxComputeN)
                        using (var writer = new FileWriter(n, size.w, size.h, size.d)) {
                            var result = ShapesFromExtendingShapes(inputFileList, writer, size, shardCount);
                            shapeCount += result.Item1;
                            mirrorCount += result.Item2;
                        }
                    else {
                        var result = ShapesFromExtendingShapes(inputFileList, null, size, shardCount);
                        shapeCount += result.Item1;
                        mirrorCount += result.Item2;
                    }
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
                string progress = shapeCount.ToString("N0");
                if (options.doMirrorCount)
                    progress += ", mirror count: " + mirrorCount.ToString("N0");
                progress += " time: " + totalSeconds;
                completeString += progress;
                Console.Write(progress);
                ConsoleWriteWithBackspace("      ");
            }

            Console.WriteLine();
            FileWriter.MarkNComplete(n, completeString);
        }
        return 0;
    }

    /// <summary>
    /// Extend list of files of shapes to find new shapes and add them to a hashset.
    /// </summary>
    /// <param name="fileList">list of files of previous n voxel count shapes</param>
    /// <param name="writer">file writer to store new found shapes into</param>
    /// <param name="size">size of shapes to extend (width, height, depth)</param>
    /// <param name="shardCount">shardCount - 0 if no sharding, -8 to shard only on the 8 corners, negative to shard on
    /// corners and edges, positive to shard on corners, edges and faces.</param>
    /// <returns>(shape count found for target size {width} {height} {depth}, mirror unique shape count for size)</returns>
    private static (long shapeCount, long mirrorCount) ShapesFromExtendingShapes(IList<FileScanner.Results> fileList, FileWriter? writer, (byte w, byte h, byte d, long _) size, int shardCount) {
        int bytesLength = (size.w * size.h * size.d + 7) / 8;
        IBitShapeHashSet newShapes = options.hashSetAlgorithm switch {
            HashSetAlgorithm.HashSet => BitShapeHashSetFactory.CreateWithHashSet(use256HashSets: false),
            HashSetAlgorithm.HashSet256 => BitShapeHashSetFactory.CreateWithHashSet(use256HashSets: true),
            HashSetAlgorithm.Dictionary => BitShapeHashSetFactory.CreateWithDictionary(),
            HashSetAlgorithm.HashSet64K => BitShapeHashSetFactory.Create(bytesLength, preferSpeedOverMemory: false),
            HashSetAlgorithm.HashSet16M => BitShapeHashSetFactory.Create(bytesLength, preferSpeedOverMemory: true),
            _ => throw new ArgumentException("Unrecognized hash set algorithm " + options.hashSetAlgorithm),
        };

        if (shardCount == 0) // no sharding
            return ShapesFromExtendingShapes(fileList, writer, newShapes, size.w, size.h, size.d, -1, -1, -1);

        long shapeCount = 0, mirrorCount = 0;
        if (shardCount < 0)
            if (shardCount == -8) // just corner count sharding
                for (int cornerIndex = 0; cornerIndex <= 8; cornerIndex++) {
                    var result = ShapesFromExtendingShapes(fileList, writer, newShapes, size.w, size.h, size.d, cornerIndex, -1, -1);
                    shapeCount += result.Item1;
                    mirrorCount += result.Item2;
                }
            else // just corner/edge count sharding
                for (int cornerIndex = 0; cornerIndex <= 8; cornerIndex++)
                    for (int edgeIndex = cornerIndex == 0 ? 0 : 1; edgeIndex <= -shardCount - cornerIndex; edgeIndex++) {
                        var result = ShapesFromExtendingShapes(fileList, writer, newShapes, size.w, size.h, size.d, cornerIndex, edgeIndex, -1);
                        shapeCount += result.Item1;
                        mirrorCount += result.Item2;
                    }
        else { // corner/edge/face count sharding
            int maxInteriorCount = Math.Max(0, size.w - 2) * Math.Max(0, size.h - 2) * Math.Max(0, size.d - 2);
            for (int cornerIndex = 0; cornerIndex <= 8; cornerIndex++)
                for (int edgeIndex = cornerIndex == 0 ? 0 : 1; edgeIndex <= shardCount - cornerIndex; edgeIndex++)
                    for (int faceIndex = edgeIndex == 0 ? 0 : 1; faceIndex <= shardCount - cornerIndex - edgeIndex; faceIndex++)
                        if (cornerIndex + edgeIndex + faceIndex >= shardCount - maxInteriorCount) {
                            var result = ShapesFromExtendingShapes(fileList, writer, newShapes, size.w, size.h, size.d, cornerIndex, edgeIndex, faceIndex);
                            shapeCount += result.Item1;
                            mirrorCount += result.Item2;
                        }
        }
        return (shapeCount, mirrorCount);
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
    /// <returns>(shape count, mirror shape count)</returns>
    private static (long shapeCount, long mirrorCount) ShapesFromExtendingShapes(IEnumerable<FileScanner.Results> fileList, FileWriter? writer, IBitShapeHashSet newShapes, byte targetWidth, byte targetHeight, byte targetDepth, int targetCornerCount, int targetEdgeCount, int targetFaceCount) {
        long shapeCount = 0, mirrorCount = 0;
        foreach (var fileInfo in fileList)
            mirrorCount += ShapesFromExtendingShapes(fileInfo, newShapes, targetWidth, targetHeight, targetDepth, targetCornerCount, targetEdgeCount, targetFaceCount);

        if (writer is not null)
            foreach (var shape in newShapes) {
                writer.Write(shape);
                shapeCount++;
            }
        else
            shapeCount = newShapes.Count();
        newShapes.Clear();
        return (shapeCount, mirrorCount);
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
    /// <returns>oddly, returns the number of mirror unique shapes found</returns>
    private static long ShapesFromExtendingShapes(FileScanner.Results fileInfo, IBitShapeHashSet newShapes, byte targetWidth, byte targetHeight, byte targetDepth, int targetCornerCount, int targetEdgeCount, int targetFaceCount) {
        byte w = fileInfo.w, h = fileInfo.h, d = fileInfo.d;
        int shapeSizeInBytes = new BitShape(w, h, d).bytes.Length;
        long sourceShapes = FileReader.FileSize(fileInfo.n, w, h, d) / shapeSizeInBytes;
        long sourceShapes100 = sourceShapes / 100;
        long mirrorCount = 0;

        if (w == targetWidth && h == targetHeight && d == targetDepth) {
            // target shape size is same as source shape size, so we are just adding a voxel to the shape
            StatusUpdate('*', targetCornerCount, targetEdgeCount, targetFaceCount);
            long sourceShapeCount = 0, nextShapeCount = sourceShapes100;
            int percent = 0;
            Parallel.ForEach(FileReader.LoadShapes(fileInfo), shape => {
                if (Interlocked.Increment(ref sourceShapeCount) == nextShapeCount) {
                    nextShapeCount += sourceShapes100;
                    ConsoleWriteWithBackspace("*" + ++percent + "%");
                }
                long mc = AddShapes(newShapes, shape, 0, w, 0, h, 0, d, targetCornerCount, targetEdgeCount, targetFaceCount);
                Interlocked.Add(ref mirrorCount, mc);
            });
        }

        var (wMin, hMin, dMin) = ShapeMakerHelper.MinRotation((byte)(w + 1), h, d);
        if (wMin == targetWidth && hMin == targetHeight && dMin == targetDepth) {
            // target shape size is one voxel wider than source shape size, so we are adding a layer on the left and right and adding a voxel to that layer
            StatusUpdate('|', targetCornerCount, targetEdgeCount, targetFaceCount);
            long sourceShapeCount = 0, nextShapeCount = sourceShapes100;
            int percent = 0;
            Parallel.ForEach(FileReader.LoadShapes(fileInfo), shape => {
                if (Interlocked.Increment(ref sourceShapeCount) == nextShapeCount) {
                    nextShapeCount += sourceShapes100;
                    ConsoleWriteWithBackspace("|" + ++percent + "%");
                }
                long mc1 = AddShapes(newShapes, shape.PadLeft(), 0, 1, 0, h, 0, d, targetCornerCount, targetEdgeCount, targetFaceCount);
                long mc2 = AddShapes(newShapes, shape.PadRight(), w, w + 1, 0, h, 0, d, targetCornerCount, targetEdgeCount, targetFaceCount);
                Interlocked.Add(ref mirrorCount, mc1 + mc2);
            });
        }

        (wMin, hMin, dMin) = ShapeMakerHelper.MinRotation(w, (byte)(h + 1), d);
        if (wMin == targetWidth && hMin == targetHeight && dMin == targetDepth) {
            // target shape size is one voxel taller than source shape size, so we are adding a layer on the top and bottom and adding a voxel to that layer
            StatusUpdate('-', targetCornerCount, targetEdgeCount, targetFaceCount);
            long sourceShapeCount = 0, nextShapeCount = sourceShapes100;
            int percent = 0;
            Parallel.ForEach(FileReader.LoadShapes(fileInfo), shape => {
                if (Interlocked.Increment(ref sourceShapeCount) == nextShapeCount) {
                    nextShapeCount += sourceShapes100;
                    ConsoleWriteWithBackspace("-" + ++percent + "%");
                }
                long mc1 = AddShapes(newShapes, shape.PadTop(), 0, w, 0, 1, 0, d, targetCornerCount, targetEdgeCount, targetFaceCount);
                long mc2 = AddShapes(newShapes, shape.PadBottom(), 0, w, h, h + 1, 0, d, targetCornerCount, targetEdgeCount, targetFaceCount);
                Interlocked.Add(ref mirrorCount, mc1 + mc2);
            });
        }

        (wMin, hMin, dMin) = ShapeMakerHelper.MinRotation(w, h, (byte)(d + 1));
        if (wMin == targetWidth && hMin == targetHeight && dMin == targetDepth) {
            // target shape size is one voxel deeper than source shape size, so we are adding a layer on the front and back and adding a voxel to that layer
            StatusUpdate('/', targetCornerCount, targetEdgeCount, targetFaceCount);
            long sourceShapeCount = 0, nextShapeCount = sourceShapes100;
            int percent = 0;
            Parallel.ForEach(FileReader.LoadShapes(fileInfo), shape => {
                if (Interlocked.Increment(ref sourceShapeCount) == nextShapeCount) {
                    nextShapeCount += sourceShapes100;
                    ConsoleWriteWithBackspace("/" + ++percent + "%");
                }
                long mc1 = AddShapes(newShapes, shape.PadFront(), 0, w, 0, h, 0, 1, targetCornerCount, targetEdgeCount, targetFaceCount);
                long mc2 = AddShapes(newShapes, shape.PadBack(), 0, w, 0, h, d, d + 1, targetCornerCount, targetEdgeCount, targetFaceCount);
                Interlocked.Add(ref mirrorCount, mc1 + mc2);
            });
        }
        return mirrorCount;
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
    /// <returns>oddly, this returns the mirror count shapes found</returns>
    private static long AddShapes(IBitShapeHashSet newShapes, BitShape shape, int xStart, int w, int yStart, int h, int zStart, int d, int targetCornerCount, int targetEdgeCount, int targetFaceCount) {
        long mirrorCount = 0;
        int cornerCount = targetCornerCount, edgeCount = targetEdgeCount, faceCount = targetFaceCount;
        if (targetCornerCount >= 0) // if sharding
            if (targetEdgeCount < 0) { // if sharding on corners only
                cornerCount = shape.CornerCount();
                if (cornerCount > targetCornerCount || (cornerCount + 1) < targetCornerCount)
                    return mirrorCount;
            } else if (targetFaceCount < 0) { // if sharding on corners and edges
                (cornerCount, edgeCount) = shape.CornerEdgeCount();
                if (cornerCount > targetCornerCount || (cornerCount + 1) < targetCornerCount ||
                    edgeCount > targetEdgeCount || (edgeCount + 1) < targetEdgeCount)
                    return mirrorCount;
            } else { // if sharding on corners, edges and faces
                (cornerCount, edgeCount, faceCount) = shape.CornerEdgeFaceCount();
                if (cornerCount > targetCornerCount || (cornerCount + 1) < targetCornerCount ||
                    edgeCount > targetEdgeCount || (edgeCount + 1) < targetEdgeCount ||
                    faceCount > targetFaceCount || (faceCount + 1) < targetFaceCount)
                    return mirrorCount;
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
                        if (targetEdgeCount >= 0) { // if sharding on corners and edges
                            bool isEdge = xFace && yFace || yFace && zFace || xFace && zFace;
                            if (isEdge && (targetCornerCount != cornerCount || targetEdgeCount != edgeCount + 1 || targetFaceCount != faceCount))
                                continue;
                            if (targetFaceCount >= 0) { // if sharding on corners, edges and faces
                                bool isFace = !isCorner && !isEdge && !isInterior;
                                if (isFace && (targetCornerCount != cornerCount || targetEdgeCount != edgeCount || targetFaceCount != faceCount + 1))
                                    continue;
                            }
                        }
                    }
                    if (!shape[x, y, z] && shape.HasSetNeighbor(x, y, z)) {
                        Array.Copy(shapeBytes, newShapeBytes, shapeBytesLength);
                        newShape[x, y, z] = true;
                        var minRotation = newShape.MinRotation();
                        bool added = newShapes.Add(minRotation.bytes);
                        if (options.doMirrorCount && added && minRotation.IsMinMirrorRotation())
                            Interlocked.Increment(ref mirrorCount);
                    }
                }
            }
        }
        return mirrorCount;
    }
}