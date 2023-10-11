using System.Collections;

namespace ShapeMaker;

/// <summary>
/// Special purpose append-only hash set for BitShape bytes. For smaller sizes we use a bit array, then 16m bit arrays,
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
/// list and a new byte array for each entry, but the benefits should come when dealing with hundreds of millions of
/// shapes which wouldn't really be the case until we get to around n=15, but that's when this approach should really
/// pay off and that where it is needed most.
/// </summary>
public class BitShapeHashSet : IEnumerable<byte[]> {
    private readonly BitShapeHashBucket?[] buckets;
    private readonly int bytesStored, hashIndex, bytesLength, entriesPerPage;
    private const int PAGE_SIZE = 1024;
    public const int MAX_BYTES_LENGTH = PAGE_SIZE + 3;

    // this cannot be easily changed - lots of the code depends on this specific value
    private const int NUMBER_OF_BUCKETS = 16777216;

    /// <summary>
    /// Creates a new BitShapeHashSet with the specified number of bytes per entry.
    /// </summary>
    /// <param name="bytesLength">number of bytes per entry, from 1 to PAGE_SIZE+3</param>
    public BitShapeHashSet(int bytesLength) {
        this.bytesLength = bytesLength; // number of bytes in each entry

        bytesStored = Math.Max(0, bytesLength - 3); // number of bytes actually stored in the bucket pages per entry
        hashIndex = Math.Max(0, bytesLength - 4); // start index of bytes in entry to be used as bucket index

        switch (bytesLength) {
            case < 1:
                throw new ArgumentOutOfRangeException(nameof(bytesLength), bytesLength, "range 1 to " + MAX_BYTES_LENGTH);
            case > MAX_BYTES_LENGTH:
                throw new ArgumentOutOfRangeException(nameof(bytesLength), bytesLength, "range 1 to " + MAX_BYTES_LENGTH);
            case 1:
            case 2:
            case 3:
                // Special case for small byte lengths - we use a single page with a byte array that we use as a bit array
                entriesPerPage = 1 << (8 * bytesLength);
                buckets = new[] { new BitShapeHashBucket() };
                buckets[0]!.pages = new List<byte[]>(1) { new byte[entriesPerPage / 8] }; // 32B, 8KB or 2MB
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

    /// <summary>
    /// Clears the hash set. Note that this is not perfectly thread safe.
    /// </summary>
    public void Clear() {
        switch (bytesLength) {
            case 1:
            case 2:
            case 3:
                Array.Fill<byte>(buckets[0]!.pages![0], 0);
                break;
            case 4:
                for (int bucketIndex = 0; bucketIndex < NUMBER_OF_BUCKETS; bucketIndex++) {
                    var bucket = buckets[bucketIndex];
                    if (bucket == null) continue;
                    lock (bucket) {
                        if (bucket.pages is { Count: > 0 }) Array.Fill<byte>(bucket.pages[0], 0);
                        bucket.lastPageEntryCount = 0;
                    }
                }
                break;
            default:
                for (int bucketIndex = 0; bucketIndex < NUMBER_OF_BUCKETS; bucketIndex++) {
                    var bucket = buckets[bucketIndex];
                    if (bucket == null) continue;
                    lock (bucket) {
                        bool notFirst = false;
                        bucket.pages?.RemoveAll(_ => {
                            var retVal = notFirst;
                            notFirst = true;
                            return retVal;
                        });
                        bucket.lastPageEntryCount = 0;
                    }
                }
                break;
        }
    }

    /// <summary>
    /// Adds the specified value to the hash set. Returns true if the value was added, false if it was already present.
    /// </summary>
    /// <param name="value">value to be added to hash set</param>
    /// <returns>true if value was added, false if it was already present</returns>
    /// <remarks>This method IS thread safe</remarks>
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
                var bucket = buckets[0]!;
                int byteIndex = bitIndex >> 3, shr = bitIndex & 7;
                byte mask = (byte)(128 >> shr);
                var page = bucket.pages![0];
                // check the bit, if set, we don't add
                if ((page[byteIndex] & mask) != 0) 
                    return false;
                lock (bucket) {
                    var pageByte = page[byteIndex];
                    // check the bit again
                    if ((pageByte & mask) != 0) 
                        return false;
                    // add the bit
                    page[byteIndex] = (byte)(pageByte | mask);
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
                        bucket.pages ??= new List<byte[]>(1) { new byte[256 / 8] };
                    }
                }
                var page = bucket.pages[0];
                int byteIndex = value[3] >> 3, shr = value[3] & 7, mask = 128 >> shr;
                // check the bit, if set, we don't add
                if ((page[byteIndex] & mask) != 0) return false;
                lock (bucket) {
                    var pageByte = page[byteIndex];
                    // check the bit again
                    if ((pageByte & mask) != 0) return false;
                    page[byteIndex] = (byte)(pageByte | mask);
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
                        bucket.pages ??= new List<byte[]> { new byte[PAGE_SIZE] };
                    }
                }
                if (bucket.pages.Count == 0) {
                    lock (bucket) {
                        if (bucket.pages.Count == 0) bucket.pages.Add(new byte[PAGE_SIZE]);
                    }
                }
                int pageCount, lastPageEntryCount;
                lock (bucket) {
                    pageCount = bucket.pages.Count;
                    lastPageEntryCount = bucket.lastPageEntryCount;
                }
                // scan bucket to see if value found without locking
                for (int pageIndex = 0; pageIndex < pageCount; pageIndex++) {
                    var page = bucket.pages[pageIndex];
                    int entryCount = pageIndex + 1 < pageCount ? entriesPerPage : lastPageEntryCount;
                    for (int entryIndex = 0, byteIndex = 0; entryIndex < entryCount; entryIndex++, byteIndex += bytesStored)
                        if (ByteArrayCompare(page, byteIndex, value, 0, firstValueBytes) && page[byteIndex + firstValueBytes] == lastValueByte)
                            return false;
                }
                int scannedEntryCount = (pageCount - 1) * entriesPerPage + lastPageEntryCount;
                var startEntryIndex = scannedEntryCount % entriesPerPage;
                var startByteIndex = startEntryIndex * bytesStored;

                lock (bucket) {
                    pageCount = bucket.pages.Count;
                    lastPageEntryCount = bucket.lastPageEntryCount;
                    // scan the rest of the bucket under lock
                    for (int pageIndex = scannedEntryCount / entriesPerPage; pageIndex < pageCount; pageIndex++) {
                        var page = bucket.pages[pageIndex];
                        int entryCount = pageIndex + 1 < pageCount ? entriesPerPage : lastPageEntryCount;
                        for (int entryIndex = startEntryIndex, byteIndex = startByteIndex; entryIndex < entryCount; entryIndex++, byteIndex += bytesStored)
                            if (ByteArrayCompare(page, byteIndex, value, 0, firstValueBytes) && page[byteIndex + firstValueBytes] == lastValueByte)
                                return false;
                        startEntryIndex = 0;
                        startByteIndex = 0;
                    }
                    // add new entry
                    int destByteIndex;
                    byte[] destPage;
                    if (lastPageEntryCount < entriesPerPage) {
                        destPage = bucket.pages[pageCount - 1];
                        destByteIndex = lastPageEntryCount * bytesStored;
                        bucket.lastPageEntryCount++;
                    } else {
                        destPage = new byte[PAGE_SIZE];
                        bucket.pages.Add(destPage);
                        destByteIndex = 0;
                        bucket.lastPageEntryCount = 1;
                    }
                    Array.Copy(value, 0, destPage, destByteIndex, firstValueBytes);
                    destPage[destByteIndex + firstValueBytes] = lastValueByte;
                    return true;
                }
            }
        }

        // Internal helper method to compare two byte arrays for equality
        static bool ByteArrayCompare(byte[] byteArray1, int offset1, byte[] byteArray2, int offset2, int length) {
            int endOffset1 = offset1 + length;
            while (offset1 < endOffset1)
                if (byteArray1[offset1++] != byteArray2[offset2++])
                    return false;
            return true;
        }
    }

    /// <summary>
    /// Get the contents of the hash set as a sequence of byte arrays.
    /// </summary>
    /// <returns>A sequence of the hash set contents</returns>
    /// <remarks>This method is NOT perfectly thread safe</remarks>
    public IEnumerator<byte[]> GetEnumerator() {
        switch (bytesLength) {
            case < 1:
                throw new ApplicationException("how did we get here?");
            case 1: {
                var page = buckets[0]!.pages![0];
                for (int byteIndex = 0; byteIndex < page.Length; byteIndex++) {
                    byte bits = page[byteIndex];
                    if (bits == 0) continue;
                    byte byte0 = (byte)(byteIndex << 3);
                    if ((bits & 128) != 0)
                        yield return new[] { byte0 };
                    if ((bits & 64) != 0) 
                        yield return new[] { (byte)(byte0 | 1) };
                    if ((bits & 32) != 0) 
                        yield return new[] { (byte)(byte0 | 2) };
                    if ((bits & 16) != 0) 
                        yield return new[] { (byte)(byte0 | 3) };
                    if ((bits & 8) != 0) 
                        yield return new[] { (byte)(byte0 | 4) };
                    if ((bits & 4) != 0) 
                        yield return new[] { (byte)(byte0 | 5) };
                    if ((bits & 2) != 0) 
                        yield return new[] { (byte)(byte0 | 6) };
                    if ((bits & 1) != 0)
                        yield return new[] { (byte)(byte0 | 7) };
                }
                break;
            }
            case 2: {
                var page = buckets[0]!.pages![0];
                for (int byteIndex = 0; byteIndex < page.Length; byteIndex++) {
                    byte bits = page[byteIndex];
                    if (bits == 0) continue;
                    byte byte0 = (byte)(byteIndex >> 5), byte1 = (byte)(byteIndex << 3);
                    if ((bits & 128) != 0) 
                        yield return new[] { byte0, byte1 };
                    if ((bits & 64) != 0) 
                        yield return new[] { byte0, (byte)(byte1 | 1) };
                    if ((bits & 32) != 0) 
                        yield return new[] { byte0, (byte)(byte1 | 2) };
                    if ((bits & 16) != 0) 
                        yield return new[] { byte0, (byte)(byte1 | 3) };
                    if ((bits & 8) != 0) 
                        yield return new[] { byte0, (byte)(byte1 | 4) };
                    if ((bits & 4) != 0) 
                        yield return new[] { byte0, (byte)(byte1 | 5) };
                    if ((bits & 2) != 0) 
                        yield return new[] { byte0, (byte)(byte1 | 6) };
                    if ((bits & 1) != 0)
                        yield return new[] { byte0, (byte)(byte1 | 7) };
                }
                break;
            }
            case 3: {
                var page = buckets[0]!.pages![0];
                for (int byteIndex = 0; byteIndex < page.Length; byteIndex++) {
                    byte bits = page[byteIndex];
                    if (bits == 0) continue;
                    byte byte0 = (byte)(byteIndex >> 13), byte1 = (byte)(byteIndex >> 5), byte2 = (byte)(byteIndex << 3);
                    if ((bits & 128) != 0) 
                        yield return new[] { byte0, byte1, byte2 };
                    if ((bits & 64) != 0) 
                        yield return new[] { byte0, byte1, (byte)(byte2 | 1) };
                    if ((bits & 32) != 0) 
                        yield return new[] { byte0, byte1, (byte)(byte2 | 2) };
                    if ((bits & 16) != 0) 
                        yield return new[] { byte0, byte1, (byte)(byte2 | 3) };
                    if ((bits & 8) != 0) 
                        yield return new[] { byte0, byte1, (byte)(byte2 | 4) };
                    if ((bits & 4) != 0) 
                        yield return new[] { byte0, byte1, (byte)(byte2 | 5) };
                    if ((bits & 2) != 0) 
                        yield return new[] { byte0, byte1, (byte)(byte2 | 6) };
                    if ((bits & 1) != 0) 
                        yield return new[] { byte0, byte1, (byte)(byte2 | 7) };
                }
                break;
            }
            case 4:
                for (int byte0 = 0, bucketIndex = 0; byte0 < 256; byte0++) {
                    for (int byte1 = 0; byte1 < 256; byte1++) {
                        for (int byte2 = 0; byte2 < 256; byte2++, bucketIndex++) {
                            var bucket = buckets[bucketIndex];
                            if (bucket?.pages != null) {
                                var page = bucket.pages[0];
                                byte mask = 128;
                                int byteIndex = 0;
                                for (int entryIndex = 0; entryIndex < 256; entryIndex++) {
                                    if ((page[byteIndex] & mask) != 0)
                                        yield return new[] { (byte)byte0, (byte)byte1, (byte)byte2, (byte)entryIndex };
                                    mask >>= 1;
                                    if (mask == 0) {
                                        mask = 128;
                                        byteIndex++;
                                    }
                                }
                            }
                        }
                    }
                }
                break;
            default:
                for (int byte0 = 0, bucketIndex = 0; byte0 < 256; byte0++) {
                    for (int byte1 = 0; byte1 < 256; byte1++) {
                        for (int byte2 = 0; byte2 < 256; byte2++, bucketIndex++) {
                            var bucket = buckets[bucketIndex];
                            if (bucket?.pages != null) {
                                int pageCount, lastPageEntryCount;
                                lock (bucket) {
                                    pageCount = bucket.pages.Count;
                                    lastPageEntryCount = bucket.lastPageEntryCount;
                                }
                                for (int pageIndex = 0; pageIndex < pageCount; pageIndex++) {
                                    var page = bucket.pages[pageIndex];
                                    int entryCount = pageIndex + 1 < bucket.pages.Count ? entriesPerPage : lastPageEntryCount;
                                    for (int entryIndex = 0, byteIndex = 0; entryIndex < entryCount; entryIndex++, byteIndex += bytesStored) {
                                        var returnValue = new byte[bytesLength];
                                        Array.Copy(page, byteIndex, returnValue, 0, bytesStored - 1);
                                        int destByteIndex = bytesLength;
                                        returnValue[--destByteIndex] = page[byteIndex + bytesStored - 1];
                                        returnValue[--destByteIndex] = (byte)byte2;
                                        returnValue[--destByteIndex] = (byte)byte1;
                                        returnValue[--destByteIndex] = (byte)byte0;
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
        public List<byte[]>? pages = null;
        public int lastPageEntryCount = 0;
    }
}
