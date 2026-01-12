namespace Aprillz.MewUI.Resources;

/// <summary>
/// Represents a multi-size icon. Picks the nearest bitmap for the requested size.
/// </summary>
public sealed class IconSource
{
    private readonly List<Entry> _entries = new();

    private sealed record Entry(int SizePx, ImageSource Source);

    public IconSource Add(int sizePx, ImageSource source)
    {
        if (sizePx <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizePx));
        }

        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        _entries.Add(new Entry(sizePx, source));
        return this;
    }

    public ImageSource? Pick(int desiredSizePx)
    {
        if (_entries.Count == 0)
        {
            return null;
        }

        if (desiredSizePx <= 0)
        {
            desiredSizePx = 1;
        }

        Entry best = _entries[0];
        int bestDelta = Math.Abs(best.SizePx - desiredSizePx);

        for (int i = 1; i < _entries.Count; i++)
        {
            var e = _entries[i];
            int delta = Math.Abs(e.SizePx - desiredSizePx);
            if (delta < bestDelta)
            {
                best = e;
                bestDelta = delta;
            }
        }

        return best.Source;
    }
}

