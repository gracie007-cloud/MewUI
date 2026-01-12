using System.Runtime.CompilerServices;

using Aprillz.MewUI.Elements;
using Aprillz.MewUI.Primitives;

namespace Aprillz.MewUI.Panels;

public enum Dock
{
    Left,
    Top,
    Right,
    Bottom
}

public sealed class DockPanel : Panel
{
    private sealed class DockData
    {
        public Dock Dock;
    }

    private static readonly ConditionalWeakTable<Element, DockData> DockMap = new();

    public static void SetDock(Element element, Dock dock)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        DockMap.GetOrCreateValue(element).Dock = dock;
        element.InvalidateArrange();
    }

    public static Dock GetDock(Element element)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return DockMap.TryGetValue(element, out var data) ? data.Dock : Dock.Left;
    }

    public bool LastChildFill
    {
        get;
        set { field = value; InvalidateMeasure(); }
    } = true;

    protected override Size MeasureContent(Size availableSize)
    {
        if (Count == 0)
        {
            return Size.Empty;
        }

        var inner = availableSize.Deflate(Padding);

        double usedW = 0;
        double usedH = 0;
        double desiredW = 0;
        double desiredH = 0;

        int last = Count - 1;
        for (int i = 0; i < Count; i++)
        {
            var child = this[i];
            if (child is not UIElement ui || !ui.IsVisible)
            {
                continue;
            }

            bool isLastFill = LastChildFill && i == last;
            var remaining = new Size(Math.Max(0, inner.Width - usedW), Math.Max(0, inner.Height - usedH));
            var dock = GetDock(child);

            if (!isLastFill && (dock is Dock.Left or Dock.Right or Dock.Top or Dock.Bottom))
            {
                // Two-pass measure (WPF-style): first get the natural size in the docking direction,
                // then re-measure using the final (clamped) size so wrapping/layout can respond to constraints.
                if (dock is Dock.Left or Dock.Right)
                {
                    child.Measure(new Size(double.PositiveInfinity, remaining.Height));
                    var w = Math.Min(child.DesiredSize.Width, remaining.Width);
                    child.Measure(new Size(w, remaining.Height));
                }
                else
                {
                    child.Measure(new Size(remaining.Width, double.PositiveInfinity));
                    var h = Math.Min(child.DesiredSize.Height, remaining.Height);
                    child.Measure(new Size(remaining.Width, h));
                }
            }
            else
            {
                child.Measure(remaining);
            }

            var desired = child.DesiredSize;

            if (isLastFill)
            {
                desiredW = Math.Max(desiredW, usedW + desired.Width);
                desiredH = Math.Max(desiredH, usedH + desired.Height);
                continue;
            }

            switch (dock)
            {
                case Dock.Left:
                case Dock.Right:
                    usedW += desired.Width;
                    desiredW = Math.Max(desiredW, usedW);
                    desiredH = Math.Max(desiredH, usedH + desired.Height);
                    break;
                case Dock.Top:
                case Dock.Bottom:
                    usedH += desired.Height;
                    desiredW = Math.Max(desiredW, usedW + desired.Width);
                    desiredH = Math.Max(desiredH, usedH);
                    break;
            }
        }

        return new Size(desiredW, desiredH).Inflate(Padding);
    }

    protected override void ArrangeContent(Rect bounds)
    {
        if (Count == 0)
        {
            return;
        }

        var inner = bounds.Deflate(Padding);

        double left = inner.X;
        double top = inner.Y;
        double right = inner.Right;
        double bottom = inner.Bottom;

        int last = Count - 1;
        for (int i = 0; i < Count; i++)
        {
            var child = this[i];
            if (child is not UIElement ui || !ui.IsVisible)
            {
                continue;
            }

            bool isLastFill = LastChildFill && i == last;
            var dock = GetDock(child);
            var desired = child.DesiredSize;

            if (isLastFill)
            {
                child.Arrange(new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top)));
                continue;
            }

            switch (dock)
            {
                case Dock.Left:
                {
                    var w = Math.Min(desired.Width, Math.Max(0, right - left));
                    child.Arrange(new Rect(left, top, w, Math.Max(0, bottom - top)));
                    left += w;
                    break;
                }
                case Dock.Right:
                {
                    var w = Math.Min(desired.Width, Math.Max(0, right - left));
                    child.Arrange(new Rect(Math.Max(left, right - w), top, w, Math.Max(0, bottom - top)));
                    right -= w;
                    break;
                }
                case Dock.Top:
                {
                    var h = Math.Min(desired.Height, Math.Max(0, bottom - top));
                    child.Arrange(new Rect(left, top, Math.Max(0, right - left), h));
                    top += h;
                    break;
                }
                case Dock.Bottom:
                {
                    var h = Math.Min(desired.Height, Math.Max(0, bottom - top));
                    child.Arrange(new Rect(left, Math.Max(top, bottom - h), Math.Max(0, right - left), h));
                    bottom -= h;
                    break;
                }
            }
        }
    }
}
