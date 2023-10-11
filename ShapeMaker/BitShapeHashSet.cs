using System.Collections;
using System.Collections.Concurrent;

namespace ShapeMaker;

/// <summary>
/// Special purpose append-only hash set for BitShape bytes. For sizes from 1 to 4 bytes we use a bit arrays.
/// Otherwise, we use the last 2 or 3 bytes (but not the very last byte) as the index to the bucket rather than a hash.
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
/// shapes which wouldn't really be the case until we get to around n=15.
/// </summary>


public interface IBitShapeHashSet : IEnumerable<byte[]> {
    /// <summary>
    ///  Clears the hash set. Not expected to be perfectly thread safe.
    /// </summary>
    void Clear();
    
    /// <summary>
    /// Adds the specified value to the hash set.
    /// </summary>
    /// <param name="value">value to be added to hash set</param>
    /// <returns>true if value was added, false if it was already present</returns>
    /// <remarks>This method MUST be thread safe</remarks>
    bool Add(byte[] value);
}

public static class BitShapeHashSetFactory {
    public static IBitShapeHashSet Create(int bytesLength, bool preferSpeedOverMemory = true) {
        return bytesLength switch {
            < 1 => throw new ArgumentOutOfRangeException(nameof(bytesLength), bytesLength, "must be greater than 0"),
            1 => new BitShapeHashSetBits1(),
            2 => new BitShapeHashSetBits2(),
            3 => new BitShapeHashSetBits3(),
            4 => new BitShapeHashSetBits4(),
            _ => preferSpeedOverMemory ? new BitShapeHashSet16M(bytesLength) : new BitShapeHashSet64K(bytesLength)
        };
    }

    public static IBitShapeHashSet CreateWithDictionary() => new BitShapeHashSetDictionary();

    public static IBitShapeHashSet CreateWithHashSet(bool use256HashSets) => use256HashSets ? new BitShapeHashSet256HashSets() : new BitShapeHashSet1HashSet();
}

internal class BitShapeHashSetBits1 : IBitShapeHashSet {
    private readonly byte[] bucket = new byte[256 / 8];
    
    /// <inheritdoc />
    public void Clear() => Array.Clear(bucket, 0, bucket.Length);

    /// <inheritdoc />
    public bool Add(byte[] value) {
        int byteIndex = value[0] >> 3;
        int bitIndex = value[0] & 7;
        int bitMask = 1 << bitIndex;
        lock (bucket) {
            bool added = (bucket[byteIndex] & bitMask) == 0;
            bucket[byteIndex] |= (byte)bitMask;
            return added;
        }
    }
    
    /// <inheritdoc />
    public IEnumerator<byte[]> GetEnumerator() {
        for (int byteIndex = 0; byteIndex < bucket.Length; byteIndex++) {
            byte bits = bucket[byteIndex];
            if (bits == 0) continue;
            byte byte0 = (byte)(byteIndex << 3);
            for (int i = 0, m = 1; i < 8; i++, m <<= 1)
                if ((bits & m) != 0)
                    yield return new[] { (byte)(byte0 | i) };
        }
    }
    
    IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
}

internal class BitShapeHashSetBits2 : IBitShapeHashSet {
    private readonly byte[] bucket = new byte[65536 / 8];
    
    /// <inheritdoc />
    public void Clear() => Array.Clear(bucket, 0, bucket.Length);

    /// <inheritdoc />
    public bool Add(byte[] value) {
        int bucketIndex = (value[0] << 5) + (value[1] >> 3);
        int bitIndex = value[1] & 7;
        int bitMask = 1 << bitIndex;
        lock (bucket) {
            bool added = (bucket[bucketIndex] & bitMask) == 0;
            bucket[bucketIndex] |= (byte)bitMask;
            return added;
        }
    }
    
    /// <inheritdoc />
    public IEnumerator<byte[]> GetEnumerator() {
        for (int byteIndex = 0; byteIndex < bucket.Length; byteIndex++) {
            byte bits = bucket[byteIndex];
            if (bits == 0) continue;
            byte byte0 = (byte)(byteIndex >> 5), byte1 = (byte)(byteIndex << 3);
            for (int i = 0, m = 1; i < 8; i++, m <<= 1)
                if ((bits & m) != 0)
                    yield return new[] { byte0, (byte)(byte1 | i) };
        }
    }
    
    IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
}

internal class BitShapeHashSetBits3 : IBitShapeHashSet {
    private readonly byte[] bucket = new byte[16777216 / 8];
    
    /// <inheritdoc />
    public void Clear() => Array.Clear(bucket, 0, bucket.Length);

    /// <inheritdoc />
    public bool Add(byte[] value) {
        int bucketIndex = (value[0] << 13) + (value[1] << 5) + (value[2] >> 3);
        int bitIndex = value[2] & 7;
        int bitMask = 1 << bitIndex;
        lock (bucket) {
            bool added = (bucket[bucketIndex] & bitMask) == 0;
            bucket[bucketIndex] |= (byte)bitMask;
            return added;
        }
    }
    
    /// <inheritdoc />
    public IEnumerator<byte[]> GetEnumerator() {
        for (int byteIndex = 0; byteIndex < bucket.Length; byteIndex++) {
            byte bits = bucket[byteIndex];
            if (bits == 0) continue;
            byte byte0 = (byte)(byteIndex >> 13), byte1 = (byte)(byteIndex >> 5), byte2 = (byte)(byteIndex << 3);
            for (int i = 0, m = 1; i < 8; i++, m <<= 1)
                if ((bits & m) != 0)
                    yield return new[] { byte0, byte1, (byte)(byte2 | i) };
        }
    }
    
    IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
}

internal class BitShapeHashSetBits4 : IBitShapeHashSet {
    private readonly byte[] bucket = new byte[4294967296 / 8];
    
    /// <inheritdoc />
    public void Clear() => Array.Clear(bucket, 0, bucket.Length);

    /// <inheritdoc />
    public bool Add(byte[] value) {
        int bucketIndex = (value[0] << 21) + (value[1] << 13) + (value[2] << 5) + (value[3] >> 3);
        int bitIndex = value[3] & 7;
        int bitMask = 1 << bitIndex;
        lock (bucket) {
            bool added = (bucket[bucketIndex] & bitMask) == 0;
            bucket[bucketIndex] |= (byte)bitMask;
            return added;
        }
    }
    
    /// <inheritdoc />
    public IEnumerator<byte[]> GetEnumerator() {
        for (int byteIndex = 0; byteIndex < bucket.Length; byteIndex++) {
            byte bits = bucket[byteIndex];
            if (bits == 0) continue;
            byte byte0 = (byte)(byteIndex >> 21), byte1 = (byte)(byteIndex >> 13), byte2 = (byte)(byteIndex >> 5), byte3 = (byte)(byteIndex << 3);
            for (int i = 0, m = 1; i < 8; i++, m <<= 1)
                if ((bits & m) != 0)
                    yield return new[] { byte0, byte1, byte2, (byte)(byte3 | i) };
        }
    }
    
    IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
}

internal class BitShapeHashSetDictionary : IBitShapeHashSet {
    private readonly ConcurrentDictionary<byte[], byte> dictionary = new(ByteArrayEqualityComparer.Instance);

    /// <inheritdoc />
    public void Clear() => dictionary.Clear();

    /// <inheritdoc />
    public bool Add(byte[] value) => dictionary.TryAdd(value, 0);
    
    /// <inheritdoc />
    public IEnumerator<byte[]> GetEnumerator() => dictionary.Keys.GetEnumerator();
    
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal class BitShapeHashSet1HashSet : IBitShapeHashSet {
    private readonly HashSet<byte[]> hashSet = new(ByteArrayEqualityComparer.Instance);

    /// <inheritdoc />
    public void Clear() => hashSet.Clear();

    /// <inheritdoc />
    public bool Add(byte[] value) {
        lock (hashSet)
            return hashSet.Add(value);
    }
    
    /// <inheritdoc />
    public IEnumerator<byte[]> GetEnumerator() => hashSet.GetEnumerator();
    
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal class BitShapeHashSet256HashSets : IBitShapeHashSet {
    private readonly HashSet<byte[]>[] hashSets = new HashSet<byte[]>[256];

    public BitShapeHashSet256HashSets() {
        for (int i = 0; i < hashSets.Length; i++)
            hashSets[i] = new HashSet<byte[]>(ByteArrayEqualityComparer.Instance);
    }

    /// <inheritdoc />
    public void Clear() {
        lock (hashSets)
            foreach (var hashSet in hashSets)
                hashSet.Clear();
    }

    /// <inheritdoc />
    public bool Add(byte[] value) {
        int len = value.Length;
        byte hashIndex = len < 3 ? value[0] : value[len - 2]; // use last full byte
        var hashSet = hashSets[hashIndex];
        lock (hashSet)
            return hashSet.Add(value);
    }
    
    /// <inheritdoc />
    public IEnumerator<byte[]> GetEnumerator() {
        foreach (var hashSet in hashSets)
            lock (hashSet)
                foreach (var value in hashSet)
                    yield return value;
    }
    
    IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
}

internal class BitShapeHashSet64K : IBitShapeHashSet {
    // this cannot be easily changed - lots of the code depends on this specific value
    private const int NUMBER_OF_BUCKETS = 65536;
    private const int ENTRIES_PER_PAGE = 256;
    
    private readonly BitShapeHashBucket[] buckets = new BitShapeHashBucket[NUMBER_OF_BUCKETS];
    private readonly int bytesStored, hashIndex, bytesLength, pageSize;

    /// <summary>
    /// Creates a new BitShapeHashSet with the specified number of bytes per entry.
    /// </summary>
    /// <param name="bytesLength">number of bytes per entry, from 1 to PAGE_SIZE+2</param>
    public BitShapeHashSet64K(int bytesLength) {
        this.bytesLength = bytesLength; // number of bytes in each entry

        bytesStored = bytesLength - 2; // number of bytes actually stored in the bucket pages per entry
        hashIndex = bytesLength - 3; // start index of bytes in entry to be used as bucket index

        buckets = new BitShapeHashBucket[NUMBER_OF_BUCKETS];
        for (int i = 0; i < NUMBER_OF_BUCKETS; i++)
            buckets[i] = new BitShapeHashBucket();
        
        pageSize = ENTRIES_PER_PAGE * bytesStored;
    }
    
    /// <inheritdoc />
    public void Clear() {
        for (int bucketIndex = 0; bucketIndex < NUMBER_OF_BUCKETS; bucketIndex++) {
            var bucket = buckets[bucketIndex];
            if (bucket == null) 
                continue;
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
    }
    
    /// <inheritdoc />
    public bool Add(byte[] value) {
        byte lastValueByte = value[bytesLength - 1];
        int firstValueBytes = bytesStored - 1;
        int bucketIndex = value[hashIndex + 0] * 256 + value[hashIndex + 1];
        var bucket = buckets[bucketIndex]!;
        if (bucket.pages == null) { // bucket hasn't been initialized yet
            lock (bucket) {
                bucket.pages ??= new List<byte[]> { new byte[pageSize] };
            }
        }
        if (bucket.pages.Count == 0) {
            lock (bucket) {
                if (bucket.pages.Count == 0) bucket.pages.Add(new byte[pageSize]);
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
            int entryCount = pageIndex + 1 < pageCount ? ENTRIES_PER_PAGE : lastPageEntryCount;
            for (int entryIndex = 0, byteIndex = 0; entryIndex < entryCount; entryIndex++, byteIndex += bytesStored)
                if (ByteArrayEqualityComparer.Equals(page, byteIndex, value, 0, firstValueBytes) && page[byteIndex + firstValueBytes] == lastValueByte)
                    return false;
        }
        int scannedEntryCount = (pageCount - 1) * ENTRIES_PER_PAGE + lastPageEntryCount;
        var startEntryIndex = scannedEntryCount % ENTRIES_PER_PAGE;
        var startByteIndex = startEntryIndex * bytesStored;

        lock (bucket) {
            pageCount = bucket.pages.Count;
            lastPageEntryCount = bucket.lastPageEntryCount;
            // scan the rest of the bucket under lock
            for (int pageIndex = scannedEntryCount / ENTRIES_PER_PAGE; pageIndex < pageCount; pageIndex++) {
                var page = bucket.pages[pageIndex];
                int entryCount = pageIndex + 1 < pageCount ? ENTRIES_PER_PAGE : lastPageEntryCount;
                for (int entryIndex = startEntryIndex, byteIndex = startByteIndex; entryIndex < entryCount; entryIndex++, byteIndex += bytesStored)
                    if (ByteArrayEqualityComparer.Equals(page, byteIndex, value, 0, firstValueBytes) && page[byteIndex + firstValueBytes] == lastValueByte)
                        return false;
                startEntryIndex = 0;
                startByteIndex = 0;
            }
            // add new entry
            int destByteIndex;
            byte[] destPage;
            if (lastPageEntryCount < ENTRIES_PER_PAGE) {
                destPage = bucket.pages[pageCount - 1];
                destByteIndex = lastPageEntryCount * bytesStored;
                bucket.lastPageEntryCount++;
            } else {
                destPage = new byte[pageSize];
                bucket.pages.Add(destPage);
                destByteIndex = 0;
                bucket.lastPageEntryCount = 1;
            }
            Array.Copy(value, 0, destPage, destByteIndex, firstValueBytes);
            destPage[destByteIndex + firstValueBytes] = lastValueByte;
            return true;
        }
    }
    
    /// <inheritdoc />
    public IEnumerator<byte[]> GetEnumerator() {
        for (int byte0 = 0, bucketIndex = 0; byte0 < 256; byte0++) {
            for (int byte1 = 0; byte1 < 256; byte1++, bucketIndex++) {
                var bucket = buckets[bucketIndex];
                if (bucket?.pages != null) {
                    int pageCount, lastPageEntryCount;
                    lock (bucket) {
                        pageCount = bucket.pages.Count;
                        lastPageEntryCount = bucket.lastPageEntryCount;
                    }
                    for (int pageIndex = 0; pageIndex < pageCount; pageIndex++) {
                        var page = bucket.pages[pageIndex];
                        int entryCount = pageIndex + 1 < bucket.pages.Count ? ENTRIES_PER_PAGE : lastPageEntryCount;
                        for (int entryIndex = 0, byteIndex = 0; entryIndex < entryCount; entryIndex++, byteIndex += bytesStored) {
                            var returnValue = new byte[bytesLength];
                            Array.Copy(page, byteIndex, returnValue, 0, bytesStored - 1);
                            int destByteIndex = bytesLength;
                            returnValue[--destByteIndex] = page[byteIndex + bytesStored - 1];
                            returnValue[--destByteIndex] = (byte)byte1;
                            returnValue[--destByteIndex] = (byte)byte0;
                            yield return returnValue;
                        }
                    }
                }
            }
        }
    }
    
    IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
}

internal class BitShapeHashSet16M : IBitShapeHashSet {
    // this cannot be easily changed - lots of the code depends on this specific value
    private const int NUMBER_OF_BUCKETS = 256 * 256 * 256;
    private const int ENTRIES_PER_PAGE = 256;

    private readonly BitShapeHashBucket[] buckets = new BitShapeHashBucket[NUMBER_OF_BUCKETS];
    private readonly int bytesStored, hashIndex, bytesLength, pageSize;

    /// <summary>
    /// Creates a new BitShapeHashSet with the specified number of bytes per entry.
    /// </summary>
    /// <param name="bytesLength">number of bytes per entry, from 1 to PAGE_SIZE+3</param>
    public BitShapeHashSet16M(int bytesLength) {
        this.bytesLength = bytesLength; // number of bytes in each entry

        bytesStored = bytesLength - 3; // number of bytes actually stored in the bucket pages per entry
        hashIndex = bytesLength - 4; // start index of bytes in entry to be used as bucket index

        //for (int i = 0; i < NUMBER_OF_BUCKETS; i++) buckets[i] = new BitShapeHashBucket();
        pageSize = ENTRIES_PER_PAGE * bytesStored;
    }
    
    /// <inheritdoc />
    public void Clear() {
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
    }

    /// <inheritdoc />
    public bool Add(byte[] value) {
        // determine bucket
        // scan bucket, if found return false
        // lock(bucket) scan any new items in bucket, if found return false, else add and return true
        if (value.Length != bytesLength)
            throw new ArgumentOutOfRangeException(nameof(value), "expected value.Length to equal bytesLength provider to constructor");
        
        byte lastValueByte = value[bytesLength - 1];
        int firstValueBytes = bytesStored - 1;
        int bucketIndex = value[hashIndex + 0] * 65536 + value[hashIndex + 1] * 256 + value[hashIndex + 2];
        var bucket = buckets[bucketIndex];
        if (bucket == null) {
            lock (this) {
                bucket = buckets[bucketIndex];
                if (bucket == null) {
                    buckets[bucketIndex] = bucket = new BitShapeHashBucket() { pages = new List<byte[]>() { new byte[pageSize] } };
                }
            }
        }
        if (bucket.pages == null) { // bucket hasn't been initialized yet
            lock (bucket) {
                bucket.pages ??= new List<byte[]> { new byte[pageSize] };
            }
        }
        if (bucket.pages.Count == 0) {
            lock (bucket) {
                if (bucket.pages.Count == 0) bucket.pages.Add(new byte[pageSize]);
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
            int entryCount = pageIndex + 1 < pageCount ? ENTRIES_PER_PAGE : lastPageEntryCount;
            for (int entryIndex = 0, byteIndex = 0; entryIndex < entryCount; entryIndex++, byteIndex += bytesStored)
                if (ByteArrayEqualityComparer.Equals(page, byteIndex, value, 0, firstValueBytes) && page[byteIndex + firstValueBytes] == lastValueByte)
                    return false;
        }
        int scannedEntryCount = (pageCount - 1) * ENTRIES_PER_PAGE + lastPageEntryCount;
        var startEntryIndex = scannedEntryCount % ENTRIES_PER_PAGE;
        var startByteIndex = startEntryIndex * bytesStored;

        lock (bucket) {
            pageCount = bucket.pages.Count;
            lastPageEntryCount = bucket.lastPageEntryCount;
            // scan the rest of the bucket under lock
            for (int pageIndex = scannedEntryCount / ENTRIES_PER_PAGE; pageIndex < pageCount; pageIndex++) {
                var page = bucket.pages[pageIndex];
                int entryCount = pageIndex + 1 < pageCount ? ENTRIES_PER_PAGE : lastPageEntryCount;
                for (int entryIndex = startEntryIndex, byteIndex = startByteIndex; entryIndex < entryCount; entryIndex++, byteIndex += bytesStored)
                    if (ByteArrayEqualityComparer.Equals(page, byteIndex, value, 0, firstValueBytes) && page[byteIndex + firstValueBytes] == lastValueByte)
                        return false;
                startEntryIndex = 0;
                startByteIndex = 0;
            }
            // add new entry
            int destByteIndex;
            byte[] destPage;
            if (lastPageEntryCount < ENTRIES_PER_PAGE) {
                destPage = bucket.pages[pageCount - 1];
                destByteIndex = lastPageEntryCount * bytesStored;
                bucket.lastPageEntryCount++;
            } else {
                destPage = new byte[pageSize];
                bucket.pages.Add(destPage);
                destByteIndex = 0;
                bucket.lastPageEntryCount = 1;
            }
            Array.Copy(value, 0, destPage, destByteIndex, firstValueBytes);
            destPage[destByteIndex + firstValueBytes] = lastValueByte;
            return true;
        }
    }

    /// <inheritdoc />
    public IEnumerator<byte[]> GetEnumerator() {
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
                            int entryCount = pageIndex + 1 < bucket.pages.Count ? ENTRIES_PER_PAGE : lastPageEntryCount;
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
    }
    
    IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
}

internal record BitShapeHashBucket {
    public List<byte[]>? pages = null;
    public int lastPageEntryCount = 0;
}