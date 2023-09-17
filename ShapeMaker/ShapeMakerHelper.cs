namespace ShapeMaker;

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
        if (w <= h && w <= d)
            return (w, d, h); // 1,3,2 - x
        if (d < h && h < w)
            return (d, h, w); // 3,2,1 - y
        if (d < h && d < w)
            return (d, w, h); // 3,1,2 - xy
        if (w <= d)
            return (h, w, d); // 2,1,3 - z
        return (h, d, w); // 2,3,1 - yx
    }
}