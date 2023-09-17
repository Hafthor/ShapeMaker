namespace ShapeMaker;

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
        if (n < w * h * d) 
            yield return (w, h, d, fileInfo.size);
        var (w1, h1, d1) = ShapeMakerHelper.MinRotation((byte)(w + 1), h, d);
        yield return (w1, h1, d1, fileInfo.size);
        var (w2, h2, d2) = ShapeMakerHelper.MinRotation(w, (byte)(h + 1), d);
        yield return (w2, h2, d2, fileInfo.size);
        var (w3, h3, d3) = ShapeMakerHelper.MinRotation(w, h, (byte)(d + 1));
        yield return (w3, h3, d3, fileInfo.size);
    }
}