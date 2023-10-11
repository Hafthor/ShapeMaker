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
    
    public static bool Equals(byte[] byteArray1, int offset1, byte[] byteArray2, int offset2, int length) {
        int endOffset1 = offset1 + length;
        while (offset1 < endOffset1)
            if (byteArray1[offset1++] != byteArray2[offset2++])
                return false;
        return true;
        
        //var span1 = byteArray1.AsSpan().Slice(offset1, length);
        //var span2 = byteArray2.AsSpan().Slice(offset2, length);
        //return span1.SequenceEqual(span2);
    }
}