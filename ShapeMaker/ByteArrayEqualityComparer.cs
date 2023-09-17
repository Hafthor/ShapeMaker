namespace ShapeMaker;

/// <summary>
/// Byte array equality comparer. Used to compare byte arrays for equality and to get a hash code for a byte array.
/// Used to allow HashSet to store byte arrays by using .Instance as the comparer.
/// </summary>
public class ByteArrayEqualityComparer : IEqualityComparer<byte[]> {
    /// <summary>
    /// Byte array equality comparer instance. Used when creating a HashSet to store byte arrays.
    /// </summary>
    public static ByteArrayEqualityComparer Instance { get; } = new();

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