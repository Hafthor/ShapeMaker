namespace ShapeMaker;

public class BitShapeSorter { // not presently used
    public static void SortFile(string filePath, int recordSize, bool removeDuplicates) {
        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
            long totalSize = fs.Length, totalRecordCount = totalSize / recordSize;

            byte[] buffer = new byte[int.MaxValue];
            int recordCount = buffer.Length / recordSize;
            SortFile(fs, recordSize, 0, totalRecordCount, buffer);

            if (removeDuplicates) {
                long readPos = 0, writePos = 0;
                byte[] prevShape = null;
                int prevShapePos = 0;
                while (readPos < totalSize) {
                    int readBytes = recordCount * recordSize;
                    if (totalSize - readPos < readBytes) {
                        readBytes = (int)(totalSize - readPos);
                    }
                    Read(fs, readPos, buffer, readBytes);
                    readPos += readBytes;
                    int di = 0;
                    for (int si = 0; si < readBytes; si += recordSize) {
                        if (prevShape == null || Compare(prevShape, prevShapePos, buffer, si, recordSize) != 0) {
                            if (si != di) {
                                Array.Copy(buffer, si, buffer, di, recordSize);
                            }
                            prevShape = buffer;
                            prevShapePos = di;
                            di += recordSize;
                        }
                    }
                    fs.Position = writePos;
                    fs.Write(buffer, 0, di);
                    writePos += di;
                }
                fs.SetLength(writePos);
            }
        }
    }

    private static void SortFile(FileStream fs, int recordSize, long start, long count, byte[] buffer) {
        for (;;) {
            if (count * recordSize <= buffer.Length) {
                int readBytes = (int)(count * recordSize);
                Read(fs, start * recordSize, buffer, readBytes);
                SortBuffer(buffer, recordSize, readBytes);
                fs.Position = start * recordSize;
                fs.Write(buffer, 0, readBytes);
                break;
            }
            
            long mid = Partition(fs, recordSize, start, count), midCount = mid - start;
            if (midCount < count - midCount) {
                SortFile(fs, recordSize, start, midCount, buffer);
                start = mid;
                count -= midCount;
            } else {
                SortFile(fs, recordSize, mid, count - midCount, buffer);
                count = midCount;
            }
        }
    }

    private static void SortBuffer(byte[] buffer, int recordSize, int bufLen) {
        for (int left = 0, right = (bufLen - 1) / recordSize, pivot = right / 2 * recordSize; left <= right;) {
            while (Compare(buffer, left * recordSize, buffer, pivot, recordSize) < 0) left++;
            while (Compare(buffer, right * recordSize, buffer, pivot, recordSize) > 0) right--;
            if (left <= right) MemSwap(buffer, left++, right--, recordSize);
        }
    }

    private static void MemSwap(byte[] buffer, int left, int right, int len) {
        for (int l = left * len, r = right * len, ll = l + len; l < ll;) {
            (buffer[l], buffer[r]) = (buffer[r++], buffer[l++]);
        }
    }

    private static long Partition(FileStream fs, int recordSize, long start, long count) {
        byte[] pivot = new byte[recordSize], lBuf = new byte[recordSize], rBuf = new byte[recordSize];
        Read(fs, start * recordSize, lBuf);
        Read(fs, (start + count / 2) * recordSize, pivot);
        Read(fs, (start + count - 1) * recordSize, rBuf);
        // median of three
        if (Compare(pivot, 0, lBuf, 0, recordSize) < 0) { // pivot < lBuf
            (pivot, lBuf) = (lBuf, pivot);
        }
        if (Compare(rBuf, 0, pivot, 0, recordSize) < 0) { // rBuf < pivot
            (pivot, rBuf) = (rBuf, pivot);
            if (Compare(pivot, 0, lBuf, 0, recordSize) < 0) { // pivot < lBuf
                (pivot, lBuf) = (lBuf, pivot);
            }
        }

        long left = start, right = start + count - 1;
        // TODO: make this way more efficient
        while (left <= right) {
            for (; left <= right && Compare(pivot, 0, lBuf, 0, recordSize) > 0; left++) {
                Read(fs, left * recordSize, lBuf);
            }
            for (; left <= right && Compare(pivot, 0, rBuf, 0, recordSize) <= 0; right--) {
                Read(fs, right * recordSize, rBuf);
            }
            if (left < right) {
                fs.Position = left++ * recordSize;
                fs.Write(rBuf, 0, recordSize);
                fs.Position = right-- * recordSize;
                fs.Write(lBuf, 0, recordSize);
            }
        }
        return left;
    }

    private static void Read(FileStream fs, long startPos, byte[] buf) => Read(fs, startPos, buf, buf.Length);
    private static void Read(FileStream fs, long startPos, byte[] buf, int readBytes) {
        fs.Position = startPos;
        if (fs.Read(buf, 0, readBytes) != readBytes) {
            throw new Exception("Failed to read shape from file");
        }
    }

    private static int Compare(byte[] a, int aOffset, byte[] b, int bOffset, int len) {
        for (int i = aOffset, j = bOffset, il = aOffset + len; i < il; i++, j++) {
            if (a[i] != b[j]) {
                return a[i] - b[j];
            }
        }
        return 0;
    }
}