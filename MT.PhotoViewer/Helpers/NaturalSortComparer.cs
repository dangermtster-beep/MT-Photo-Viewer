using System.IO;

namespace MT.PhotoViewer.Helpers;

/// <summary>
/// Doğal dosya adı sıralaması (Guide §14): 1.jpg, 2.jpg, 3.jpg, 10.jpg.
/// Sayı blokları sayısal, diğer bölümler kültüre duyarsız metin olarak karşılaştırılır.
/// </summary>
public sealed class NaturalSortComparer : IComparer<string>
{
    public static readonly NaturalSortComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        int ix = 0, iy = 0;

        while (ix < x.Length && iy < y.Length)
        {
            char cx = x[ix];
            char cy = y[iy];

            if (char.IsDigit(cx) && char.IsDigit(cy))
            {
                int sx = ix, sy = iy;
                while (ix < x.Length && char.IsDigit(x[ix])) ix++;
                while (iy < y.Length && char.IsDigit(y[iy])) iy++;

                ReadOnlySpan<char> nx = x.AsSpan(sx, ix - sx).TrimStart('0');
                ReadOnlySpan<char> ny = y.AsSpan(sy, iy - sy).TrimStart('0');

                if (nx.Length != ny.Length)
                    return nx.Length - ny.Length;

                int cmp = nx.SequenceCompareTo(ny);
                if (cmp != 0) return cmp;

                // Sayısal olarak eşit: "007" ile "7" arasında sıfır dolgusuna göre karar ver.
                int padCmp = (ix - sx) - (iy - sy);
                if (padCmp != 0) return padCmp;
            }
            else
            {
                int cmp = char.ToUpperInvariant(cx).CompareTo(char.ToUpperInvariant(cy));
                if (cmp != 0) return cmp;
                ix++;
                iy++;
            }
        }

        return (x.Length - ix) - (y.Length - iy);
    }
}

/// <summary>Tam yol listelerini dosya adına göre doğal sıralar.</summary>
public sealed class NaturalFileNameComparer : IComparer<string>
{
    public static readonly NaturalFileNameComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        string nx = x is null ? string.Empty : Path.GetFileName(x);
        string ny = y is null ? string.Empty : Path.GetFileName(y);

        int cmp = NaturalSortComparer.Instance.Compare(nx, ny);
        return cmp != 0
            ? cmp
            : string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
    }
}
