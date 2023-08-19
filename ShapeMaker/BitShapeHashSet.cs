using System.Collections;

namespace ShapeMaker;

/// <summary>
/// Special purpose append-only hash set for bitshape bytes. For smaller sizes we use a bit array, then 16m bit arrays,
/// then 16m pages.
/// We use the first 3 of the last 4 bytes as the index to the bucket rather than a hash.
/// We avoid the last byte because it is often a bit partial, so may not have many unique values.
/// We avoid the early part of the value since we order BitShapes by their minimal rotation which means the early bits
/// will typically be zeroes.
/// 
/// Here's an example showing a shape 3x3x5 = 45bits or 5.625 bytes:
/// 
///                         [......store.......][........bucket index........][.store..]
/// BtiShape bytes example: [ byte 1 ][ byte 2 ][ byte 3 ][ byte 4 ][ byte 5 ][ byte 6 ]
///                         [76543210][76543210][76543210][76543210][76543210][76543...]
///                              |         |         |         |         |         |
/// these would often be zero ---/---------/         |         |         |         |
///                                                  |         |         |         |
/// we use these as a the bucket index --------------/---------/---------/         |
///                                                                                |
/// this would be the remainder bits. might not be an bit 8 byte ------------------/
///
/// May be a little slower and memory hungry early on since we are very likely to have to create a new bucket, a new
/// list and a new byte array for each entry, but the benefits should come when dealing with hudreds of millions of
/// shapes which wouldn't really be the case until we get to around n=15, but that's when this approach should really
/// pay off and that where it is needed most.
/// </summary>
public class BitShapeHashSet : IEnumerable<byte[]> {
    private readonly BitShapeHashBucket[] buckets;
    private readonly int bytesStored, hashIndex, bytesLength, entriesPerPage;
    private const int PAGE_SIZE = 1024;

    // this cannot be easily changed - lots of the code depends on this specific value
    // might be able to change it to 65536 without too much effort.
    private const int NUMBER_OF_BUCKETS = 16777216;

    public BitShapeHashSet(int bytesLength) {
        this.bytesLength = bytesLength; // number of bytes in each entry

        bytesStored = Math.Max(0, bytesLength - 3); // number of bytes actually stored in the bucket pages per entry
        hashIndex = Math.Max(0, bytesLength - 4); // start index of bytes in entry to be used as bucket index

        switch (bytesLength) {
            case < 1:
                throw new ArgumentOutOfRangeException(nameof(bytesLength), bytesLength, "range 1 to " + PAGE_SIZE);
            case > PAGE_SIZE:
                throw new ArgumentOutOfRangeException(nameof(bytesLength), bytesLength, "range 1 to " + PAGE_SIZE);
            case 1:
            case 2:
            case 3:
                // Special case for small byte lengths - we use a single page with a byte array that we use as a bit array
                entriesPerPage = 1 << (8 * bytesLength);
                buckets = new BitShapeHashBucket[1] { new BitShapeHashBucket() };
                buckets[0].pages = new List<byte[]>(1) { new byte[entriesPerPage / 8] }; // 32B, 8KB or 2MB
                break;
            case 4:
                // Special case for 4 byte length - we use a small (32) byte array in each bucket as a bit array
                buckets = new BitShapeHashBucket[NUMBER_OF_BUCKETS];
                //for (int i = 0; i < NUMBER_OF_BUCKETS; i++) buckets[i] = new BitShapeHashBucket();
                entriesPerPage = 256;
                break;
            default:
                buckets = new BitShapeHashBucket[NUMBER_OF_BUCKETS];
                //for (int i = 0; i < NUMBER_OF_BUCKETS; i++) buckets[i] = new BitShapeHashBucket();
                entriesPerPage = PAGE_SIZE / bytesStored;
                break;
        }
    }

    public void Clear() {
        switch (bytesLength) {
            case 1:
            case 2:
            case 3:
                Array.Fill<byte>(buckets[0].pages[0], 0);
                break;
            case 4:
                for (int i = 0; i < NUMBER_OF_BUCKETS; i++) {
                    var b = buckets[i];
                    if (b == null) continue;
                    if (b.pages != null && b.pages.Count > 0) Array.Fill<byte>(b.pages[0], 0);
                    b.lastPageEntryCount = 0;
                }
                break;
            default:
                for (int i = 0; i < NUMBER_OF_BUCKETS; i++) {
                    var b = buckets[i];
                    if (b == null) continue;
                    bool notFirst = false;
                    b.pages?.RemoveAll(_ => {
                        var retVal = notFirst;
                        notFirst = true;
                        return retVal;
                    });
                    b.lastPageEntryCount = 0;
                }
                break;
        }
    }

    public bool Add(byte[] value) {
        // determine bucket
        // scan bucket, if found return false
        // lock(bucket) scan any new items in bucket, if found return false, else add and return true
        if (value.Length != bytesLength)
            throw new ArgumentOutOfRangeException(nameof(value), "expected value.Length to equal bytesLength provider to constructor");
        switch (bytesLength) {
            case < 1:
                throw new ApplicationException("how did we get here?");
            case 1:
            case 2:
            case 3: {
                    int bitIndex = bytesLength == 3 ? value[0] * 65536 + value[1] * 256 + value[2] : bytesLength == 2 ? value[0] * 256 + value[1] : value[0];
                    var bucket = buckets[0];
                    int si = bitIndex >> 3, shr = bitIndex & 7;
                    byte mask = (byte)(128 >> shr);
                    var page = buckets[0].pages[0];
                    // check the bit, if set, we don't add
                    if ((page[si] & mask) != 0) return false;
                    lock (bucket) {
                        var psi = page[si];
                        // check the bit again
                        if ((psi & mask) != 0) return false;
                        // add the bit
                        page[si] = (byte)(psi | mask);
                        return true;
                    }
                }
            case 4: {
                    int bucketIndex = value[0] * 65536 + value[1] * 256 + value[2];
                    var bucket = buckets[bucketIndex];
                    if (bucket == null) {
                        lock (this) {
                            bucket = buckets[bucketIndex];
                            if (bucket == null) {
                                buckets[bucketIndex] = bucket = new BitShapeHashBucket() { pages = new List<byte[]>(1) { new byte[256 / 8] } };
                            }
                        }
                    }
                    if (bucket.pages == null) {
                        lock (bucket) {
                            if (bucket.pages == null) bucket.pages = new List<byte[]>(1) { new byte[256 / 8] };
                        }
                    }
                    var page = bucket.pages[0];
                    int si = value[3] >> 3, shr = value[3] & 7, mask = 128 >> shr;
                    // check the bit, if set, we don't add
                    if ((page[si] & mask) != 0) return false;
                    lock (bucket) {
                        var psi = page[si];
                        // check the bit again
                        if ((psi & mask) != 0) return false;
                        page[si] = (byte)(psi | mask);
                        return true;
                    }
                }
            default: {
                    byte lastValueByte = value[bytesLength - 1];
                    int firstValueBytes = bytesStored - 1;
                    int bucketIndex = value[hashIndex + 0] * 65536 + value[hashIndex + 1] * 256 + value[hashIndex + 2];
                    var bucket = buckets[bucketIndex];
                    if (bucket == null) {
                        lock (this) {
                            bucket = buckets[bucketIndex];
                            if (bucket == null) {
                                buckets[bucketIndex] = bucket = new BitShapeHashBucket() { pages = new List<byte[]>() { new byte[PAGE_SIZE] } };
                            }
                        }
                    }
                    if (bucket.pages == null) { // bucket hasn't been initialized yet
                        lock (bucket) {
                            if (bucket.pages == null) bucket.pages = new List<byte[]> { new byte[PAGE_SIZE] };
                        }
                    }
                    if (bucket.pages.Count == 0) {
                        lock (bucket) {
                            if (bucket.pages.Count == 0) bucket.pages.Add(new byte[PAGE_SIZE]);
                        }
                    }
                    int pageCount, lpec;
                    lock (bucket) { pageCount = bucket.pages.Count; lpec = bucket.lastPageEntryCount; }
                    // scan bucket to see if value found without locking
                    for (int pageIndex = 0; pageIndex < pageCount; pageIndex++) {
                        var page = bucket.pages[pageIndex];
                        int entryCount = pageIndex + 1 < pageCount ? entriesPerPage : lpec;
                        for (int entryIndex = 0, si = 0; entryIndex < entryCount; entryIndex++, si += bytesStored)
                            if (memcmp(page, si, value, 0, firstValueBytes) && page[si + firstValueBytes] == lastValueByte)
                                return false;
                    }
                    int scannedEntryCount = (pageCount - 1) * entriesPerPage + lpec;
                    var startEntryIndex = scannedEntryCount % entriesPerPage;
                    var startSi = startEntryIndex * bytesStored;

                    lock (bucket) {
                        pageCount = bucket.pages.Count;
                        lpec = bucket.lastPageEntryCount;
                        // scan the rest of the bucket under lock
                        for (int pageIndex = scannedEntryCount / entriesPerPage; pageIndex < pageCount; pageIndex++) {
                            var page = bucket.pages[pageIndex];
                            int entryCount = pageIndex + 1 < pageCount ? entriesPerPage : lpec;
                            for (int entryIndex = startEntryIndex, si = startSi; entryIndex < entryCount; entryIndex++, si += bytesStored)
                                if (memcmp(page, si, value, 0, firstValueBytes) && page[si + firstValueBytes] == lastValueByte)
                                    return false;
                            startEntryIndex = 0;
                            startSi = 0;
                        }
                        // add new entry
                        int di;
                        byte[] p;
                        if (lpec < entriesPerPage) {
                            p = bucket.pages[pageCount - 1];
                            di = lpec * bytesStored;
                            bucket.lastPageEntryCount++;
                        } else {
                            p = new byte[PAGE_SIZE];
                            bucket.pages.Add(p);
                            di = 0;
                            bucket.lastPageEntryCount = 1;
                        }
                        Array.Copy(value, 0, p, di, firstValueBytes);
                        p[di + firstValueBytes] = lastValueByte;
                        return true;
                    }
                }
        }

        static bool memcmp(byte[] a1, int offset1, byte[] a2, int offset2, int length) {
            int endOffset1 = offset1 + length;
            while (offset1 < endOffset1)
                if (a1[offset1++] != a2[offset2++])
                    return false;
            return true;
        }
    }

    public IEnumerator<byte[]> GetEnumerator() {
        switch (bytesLength) {
            case < 1:
                throw new ApplicationException("how did we get here?");
            case 1: {
                    var page = buckets[0].pages[0];
                    for (int si = 0; si < page.Length; si++) {
                        byte b = page[si];
                        if (b == 0) continue;
                        byte ai = (byte)(si << 3);
                        if ((b & 128) != 0) yield return new byte[] { ai };
                        if ((b & 64) != 0) yield return new byte[] { (byte)(ai | 1) };
                        if ((b & 32) != 0) yield return new byte[] { (byte)(ai | 2) };
                        if ((b & 16) != 0) yield return new byte[] { (byte)(ai | 3) };
                        if ((b & 8) != 0) yield return new byte[] { (byte)(ai | 4) };
                        if ((b & 4) != 0) yield return new byte[] { (byte)(ai | 5) };
                        if ((b & 2) != 0) yield return new byte[] { (byte)(ai | 6) };
                        if ((b & 1) != 0) yield return new byte[] { (byte)(ai | 7) };
                    }
                    break;
                }
            case 2: {
                    var page = buckets[0].pages[0];
                    for (int si = 0; si < page.Length; si++) {
                        byte b = page[si];
                        if (b == 0) continue;
                        byte ai = (byte)(si >> 5), bi = (byte)(si << 3);
                        if ((b & 128) != 0) yield return new byte[] { ai, bi };
                        if ((b & 64) != 0) yield return new byte[] { ai, (byte)(bi | 1) };
                        if ((b & 32) != 0) yield return new byte[] { ai, (byte)(bi | 2) };
                        if ((b & 16) != 0) yield return new byte[] { ai, (byte)(bi | 3) };
                        if ((b & 8) != 0) yield return new byte[] { ai, (byte)(bi | 4) };
                        if ((b & 4) != 0) yield return new byte[] { ai, (byte)(bi | 5) };
                        if ((b & 2) != 0) yield return new byte[] { ai, (byte)(bi | 6) };
                        if ((b & 1) != 0) yield return new byte[] { ai, (byte)(bi | 7) };
                    }
                    break;
                }
            case 3: {
                    var page = buckets[0].pages[0];
                    for (int si = 0; si < page.Length; si++) {
                        byte b = page[si];
                        if (b == 0) continue;
                        byte ai = (byte)(si >> 13), bi = (byte)(si >> 5), ci = (byte)(si << 3);
                        if ((b & 128) != 0) yield return new byte[] { ai, bi, ci };
                        if ((b & 64) != 0) yield return new byte[] { ai, bi, (byte)(ci | 1) };
                        if ((b & 32) != 0) yield return new byte[] { ai, bi, (byte)(ci | 2) };
                        if ((b & 16) != 0) yield return new byte[] { ai, bi, (byte)(ci | 3) };
                        if ((b & 8) != 0) yield return new byte[] { ai, bi, (byte)(ci | 4) };
                        if ((b & 4) != 0) yield return new byte[] { ai, bi, (byte)(ci | 5) };
                        if ((b & 2) != 0) yield return new byte[] { ai, bi, (byte)(ci | 6) };
                        if ((b & 1) != 0) yield return new byte[] { ai, bi, (byte)(ci | 7) };
                    }
                    break;
                }
            case 4:
                for (int ai = 0, i = 0; ai < 256; ai++) {
                    for (int bi = 0; bi < 256; bi++) {
                        for (int ci = 0; ci < 256; ci++, i++) {
                            var bucket = buckets[i];
                            if (bucket?.pages != null) {
                                var page = bucket.pages[0];
                                byte mask = 128;
                                int si = 0;
                                for (int entryIndex = 0; entryIndex < 256; entryIndex++) {
                                    if ((page[si] & mask) != 0)
                                        yield return new byte[] { (byte)ai, (byte)bi, (byte)ci, (byte)entryIndex };
                                    mask >>= 1; if (mask == 0) { mask = 128; si++; }
                                }
                            }
                        }
                    }
                }
                break;
            default:
                for (int ai = 0, i = 0; ai < 256; ai++) {
                    for (int bi = 0; bi < 256; bi++) {
                        for (int ci = 0; ci < 256; ci++, i++) {
                            var bucket = buckets[i];
                            if (bucket?.pages != null) {
                                int pageCount, lpec;
                                lock (bucket) { pageCount = bucket.pages.Count; lpec = bucket.lastPageEntryCount; }
                                for (int pageIndex = 0; pageIndex < pageCount; pageIndex++) {
                                    var page = bucket.pages[pageIndex];
                                    int entryCount = pageIndex + 1 < bucket.pages.Count ? entriesPerPage : lpec;
                                    for (int entryIndex = 0, si = 0; entryIndex < entryCount; entryIndex++, si += bytesStored) {
                                        var returnValue = new byte[bytesLength];
                                        Array.Copy(page, si, returnValue, 0, bytesStored - 1);
                                        int di = bytesLength;
                                        returnValue[--di] = page[si + bytesStored - 1];
                                        returnValue[--di] = (byte)ci;
                                        returnValue[--di] = (byte)bi;
                                        returnValue[--di] = (byte)ai;
                                        yield return returnValue;
                                    }
                                }
                            }
                        }
                    }
                }
                break;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() {
        throw new NotImplementedException();
    }

    private record BitShapeHashBucket {
        public List<byte[]> pages = null;
        public int lastPageEntryCount = 0;
    }
}
