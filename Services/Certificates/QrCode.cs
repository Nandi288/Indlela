using System.Text;

namespace Indlela.Services.Certificates;

/// <summary>
/// A small, dependency-free QR code generator (byte mode, error-correction level M, versions 1–40),
/// used for the "scan to verify" code on certificates. Based on Project Nayuki's QR Code generator
/// algorithm (MIT licence). Output is an SVG string, so it stays sharp when printed.
/// </summary>
public sealed class QrCode
{
    public int Size { get; }
    private readonly bool[,] _modules;      // [y, x], true = dark
    private readonly bool[,] _isFunction;

    private QrCode(int version)
    {
        Size = version * 4 + 17;
        _modules = new bool[Size, Size];
        _isFunction = new bool[Size, Size];
    }

    public bool this[int x, int y] => _modules[y, x];

    // ---------------------------------------------------------------- public API

    public static QrCode Encode(string text)
    {
        var data = Encoding.UTF8.GetBytes(text);

        // Smallest version that fits (byte mode: 4-bit mode + 8/16-bit length + data).
        int version = 1;
        for (; ; version++)
        {
            if (version > 40) throw new ArgumentException("Text is too long for a QR code.", nameof(text));
            int capacityBits = NumDataCodewords(version) * 8;
            int usedBits = 4 + (version <= 9 ? 8 : 16) + data.Length * 8;
            if (usedBits <= capacityBits) break;
        }

        // Build the bit stream.
        var bits = new List<bool>();
        void Append(int value, int length) { for (int i = length - 1; i >= 0; i--) bits.Add(((value >> i) & 1) != 0); }
        Append(0b0100, 4);
        Append(data.Length, version <= 9 ? 8 : 16);
        foreach (var b in data) Append(b, 8);

        int capacity = NumDataCodewords(version) * 8;
        Append(0, Math.Min(4, capacity - bits.Count));          // terminator
        Append(0, (8 - bits.Count % 8) % 8);                     // byte align
        for (int pad = 0xEC; bits.Count < capacity; pad ^= 0xEC ^ 0x11) Append(pad, 8);

        var codewords = new byte[bits.Count / 8];
        for (int i = 0; i < bits.Count; i++)
            if (bits[i]) codewords[i >> 3] |= (byte)(1 << (7 - (i & 7)));

        var qr = new QrCode(version);
        qr.DrawFunctionPatterns(version);
        qr.DrawCodewords(AddEccAndInterleave(codewords, version));

        // Pick the mask with the lowest penalty.
        int bestMask = 0, bestPenalty = int.MaxValue;
        for (int mask = 0; mask < 8; mask++)
        {
            qr.ApplyMask(mask);
            qr.DrawFormatBits(mask);
            int penalty = qr.Penalty();
            if (penalty < bestPenalty) { bestPenalty = penalty; bestMask = mask; }
            qr.ApplyMask(mask); // undo (XOR)
        }
        qr.ApplyMask(bestMask);
        qr.DrawFormatBits(bestMask);
        return qr;
    }

    /// <summary>An SVG drawing of the code with a 4-module quiet zone. Scales to any size via CSS.</summary>
    public string ToSvg(string dark = "#0A2A20", string light = "#FFFFFF", string? title = null)
    {
        const int border = 4;
        int dim = Size + border * 2;
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {dim} {dim}\" shape-rendering=\"crispEdges\" role=\"img\">");
        if (title is not null) sb.Append("<title>").Append(System.Net.WebUtility.HtmlEncode(title)).Append("</title>");
        sb.Append($"<rect width=\"{dim}\" height=\"{dim}\" fill=\"{light}\"/><path fill=\"{dark}\" d=\"");
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                if (_modules[y, x]) sb.Append($"M{x + border},{y + border}h1v1h-1z");
        sb.Append("\"/></svg>");
        return sb.ToString();
    }

    // ---------------------------------------------------------------- tables (level M)

    private static readonly int[] EccPerBlock =
        { -1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28 };
    private static readonly int[] NumBlocks =
        { -1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16, 17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49 };
    private const int FormatEclBits = 0; // M

    private static int NumRawDataModules(int ver)
    {
        int result = (16 * ver + 128) * ver + 64;
        if (ver >= 2)
        {
            int numAlign = ver / 7 + 2;
            result -= (25 * numAlign - 10) * numAlign - 55;
            if (ver >= 7) result -= 36;
        }
        return result;
    }

    private static int NumDataCodewords(int ver) => NumRawDataModules(ver) / 8 - EccPerBlock[ver] * NumBlocks[ver];

    // ---------------------------------------------------------------- error correction

    private static byte[] AddEccAndInterleave(byte[] data, int ver)
    {
        int numBlocks = NumBlocks[ver], eccLen = EccPerBlock[ver];
        int rawCodewords = NumRawDataModules(ver) / 8;
        int numShortBlocks = numBlocks - rawCodewords % numBlocks;
        int shortBlockLen = rawCodewords / numBlocks;

        var divisor = RsDivisor(eccLen);
        var blocks = new List<byte[]>();
        for (int i = 0, k = 0; i < numBlocks; i++)
        {
            int datLen = shortBlockLen - eccLen + (i < numShortBlocks ? 0 : 1);
            var dat = data.AsSpan(k, datLen).ToArray();
            k += datLen;
            var ecc = RsRemainder(dat, divisor);
            var block = new byte[shortBlockLen + 1];
            Array.Copy(dat, block, datLen);                          // short blocks get a 0 placeholder after their data
            Array.Copy(ecc, 0, block, shortBlockLen + 1 - eccLen, eccLen);
            blocks.Add(block);
        }

        var result = new List<byte>(rawCodewords);
        for (int i = 0; i < shortBlockLen + 1; i++)
            for (int j = 0; j < blocks.Count; j++)
                if (i != shortBlockLen - eccLen || j >= numShortBlocks)
                    result.Add(blocks[j][i]);
        return result.ToArray();
    }

    private static byte[] RsDivisor(int degree)
    {
        var result = new byte[degree];
        result[degree - 1] = 1;
        int root = 1;
        for (int i = 0; i < degree; i++)
        {
            for (int j = 0; j < result.Length; j++)
            {
                result[j] = GfMul(result[j], root);
                if (j + 1 < result.Length) result[j] ^= result[j + 1];
            }
            root = GfMul(root, 0x02);
        }
        return result;
    }

    private static byte[] RsRemainder(byte[] data, byte[] divisor)
    {
        var result = new byte[divisor.Length];
        foreach (var b in data)
        {
            int factor = b ^ result[0];
            Array.Copy(result, 1, result, 0, result.Length - 1);
            result[^1] = 0;
            for (int i = 0; i < result.Length; i++) result[i] ^= GfMul(divisor[i], factor);
        }
        return result;
    }

    private static byte GfMul(int x, int y)
    {
        int z = 0;
        for (int i = 7; i >= 0; i--)
        {
            z = (z << 1) ^ ((z >> 7) * 0x11D);
            z ^= ((y >> i) & 1) * x;
        }
        return (byte)z;
    }

    // ---------------------------------------------------------------- drawing

    private void SetFunction(int x, int y, bool dark) { _modules[y, x] = dark; _isFunction[y, x] = true; }

    private void DrawFunctionPatterns(int ver)
    {
        for (int i = 0; i < Size; i++) { SetFunction(6, i, i % 2 == 0); SetFunction(i, 6, i % 2 == 0); }

        DrawFinder(3, 3); DrawFinder(Size - 4, 3); DrawFinder(3, Size - 4);

        var align = AlignmentPositions(ver);
        int n = align.Length;
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                if (!((i == 0 && j == 0) || (i == 0 && j == n - 1) || (i == n - 1 && j == 0)))
                    DrawAlignment(align[i], align[j]);

        DrawFormatBits(0); // reserve; redrawn after masking
        DrawVersion(ver);
    }

    private void DrawFinder(int x, int y)
    {
        for (int dy = -4; dy <= 4; dy++)
            for (int dx = -4; dx <= 4; dx++)
            {
                int dist = Math.Max(Math.Abs(dx), Math.Abs(dy)), xx = x + dx, yy = y + dy;
                if (xx >= 0 && xx < Size && yy >= 0 && yy < Size) SetFunction(xx, yy, dist != 2 && dist != 4);
            }
    }

    private void DrawAlignment(int x, int y)
    {
        for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
                SetFunction(x + dx, y + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
    }

    private int[] AlignmentPositions(int ver)
    {
        if (ver == 1) return [];
        int numAlign = ver / 7 + 2;
        int step = (ver * 8 + numAlign * 3 + 5) / (numAlign * 4 - 4) * 2;
        var result = new List<int> { 6 };
        for (int pos = Size - 7; result.Count < numAlign; pos -= step) result.Insert(1, pos);
        return result.ToArray();
    }

    private void DrawFormatBits(int mask)
    {
        int data = FormatEclBits << 3 | mask;
        int rem = data;
        for (int i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
        int bits = (data << 10 | rem) ^ 0x5412;
        bool Bit(int i) => ((bits >> i) & 1) != 0;

        for (int i = 0; i <= 5; i++) SetFunction(8, i, Bit(i));
        SetFunction(8, 7, Bit(6)); SetFunction(8, 8, Bit(7)); SetFunction(7, 8, Bit(8));
        for (int i = 9; i < 15; i++) SetFunction(14 - i, 8, Bit(i));

        for (int i = 0; i < 8; i++) SetFunction(Size - 1 - i, 8, Bit(i));
        for (int i = 8; i < 15; i++) SetFunction(8, Size - 15 + i, Bit(i));
        SetFunction(8, Size - 8, true); // the always-dark module
    }

    private void DrawVersion(int ver)
    {
        if (ver < 7) return;
        int rem = ver;
        for (int i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
        int bits = ver << 12 | rem;
        for (int i = 0; i < 18; i++)
        {
            bool bit = ((bits >> i) & 1) != 0;
            int a = Size - 11 + i % 3, b = i / 3;
            SetFunction(a, b, bit); SetFunction(b, a, bit);
        }
    }

    private void DrawCodewords(byte[] data)
    {
        int i = 0;
        for (int right = Size - 1; right >= 1; right -= 2)
        {
            if (right == 6) right = 5;
            for (int vert = 0; vert < Size; vert++)
                for (int j = 0; j < 2; j++)
                {
                    int x = right - j;
                    bool upward = ((right + 1) & 2) == 0;
                    int y = upward ? Size - 1 - vert : vert;
                    if (!_isFunction[y, x] && i < data.Length * 8)
                    {
                        _modules[y, x] = ((data[i >> 3] >> (7 - (i & 7))) & 1) != 0;
                        i++;
                    }
                }
        }
    }

    private void ApplyMask(int mask)
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                bool invert = mask switch
                {
                    0 => (x + y) % 2 == 0,
                    1 => y % 2 == 0,
                    2 => x % 3 == 0,
                    3 => (x + y) % 3 == 0,
                    4 => (x / 3 + y / 2) % 2 == 0,
                    5 => x * y % 2 + x * y % 3 == 0,
                    6 => (x * y % 2 + x * y % 3) % 2 == 0,
                    _ => ((x + y) % 2 + x * y % 3) % 2 == 0
                };
                if (!_isFunction[y, x] && invert) _modules[y, x] = !_modules[y, x];
            }
    }

    /// <summary>Simplified mask penalty (runs, 2×2 blocks, dark/light balance) — any mask scans; this just picks a clean one.</summary>
    private int Penalty()
    {
        int penalty = 0;
        for (int pass = 0; pass < 2; pass++)
            for (int a = 0; a < Size; a++)
            {
                int run = 1;
                for (int b = 1; b < Size; b++)
                {
                    bool cur = pass == 0 ? _modules[a, b] : _modules[b, a];
                    bool prev = pass == 0 ? _modules[a, b - 1] : _modules[b - 1, a];
                    if (cur == prev) { run++; if (run == 5) penalty += 3; else if (run > 5) penalty++; }
                    else run = 1;
                }
            }
        int dark = 0;
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                if (_modules[y, x]) dark++;
                if (x < Size - 1 && y < Size - 1)
                {
                    bool c = _modules[y, x];
                    if (c == _modules[y, x + 1] && c == _modules[y + 1, x] && c == _modules[y + 1, x + 1]) penalty += 3;
                }
            }
        int total = Size * Size;
        int k = (Math.Abs(dark * 20 - total * 10) + total - 1) / total - 1;
        return penalty + Math.Max(0, k) * 10;
    }
}
