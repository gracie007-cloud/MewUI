using Aprillz.MewUI.Core;
using Aprillz.MewUI.Binding;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.Primitives;
using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.Controls;

public class ListBox : Control, IDisposable
{
    private readonly List<string> _items = new();
    private ValueBinding<int>? _selectedIndexBinding;
    private bool _updatingFromSource;
    private bool _disposed;

    public IList<string> Items => _items;

    public int SelectedIndex
    {
        get;
        set
        {
            int clamped = value;
            if (_items.Count == 0)
                clamped = -1;
            else
                clamped = Math.Clamp(value, -1, _items.Count - 1);

            if (field == clamped)
                return;

            field = clamped;
            SelectionChanged?.Invoke(field);
            InvalidateVisual();
        }
    } = -1;

    public string? SelectedItem => SelectedIndex >= 0 && SelectedIndex < _items.Count ? _items[SelectedIndex] : null;

    public double ItemHeight
    {
        get;
        set { field = value; InvalidateMeasure(); }
    } = double.NaN;

    public Action<int>? SelectionChanged { get; set; }

    public override bool Focusable => true;

    public ListBox()
    {
        var theme = Theme.Current;
        Background = theme.ControlBackground;
        BorderBrush = theme.ControlBorder;
        BorderThickness = 1;
        Padding = new Thickness(1);
    }

    protected override void OnThemeChanged(Theme oldTheme, Theme newTheme)
    {
        if (Background == oldTheme.ControlBackground)
            Background = newTheme.ControlBackground;
        if (BorderBrush == oldTheme.ControlBorder)
            BorderBrush = newTheme.ControlBorder;

        base.OnThemeChanged(oldTheme, newTheme);
    }

    protected override Size MeasureContent(Size availableSize)
    {
        using var measure = BeginTextMeasurement();

        double maxWidth = 0;
        foreach (var item in _items)
        {
            if (string.IsNullOrEmpty(item))
                continue;
            maxWidth = Math.Max(maxWidth, measure.Context.MeasureText(item, measure.Font).Width);
        }

        double itemHeight = ResolveItemHeight();
        double height = _items.Count * itemHeight;

        return new Size(maxWidth, height).Inflate(Padding);
    }

    protected override void OnRender(IGraphicsContext context)
    {
        var theme = GetTheme();
        var bounds = Bounds;
        double radius = theme.ControlCornerRadius;
        double itemRadius = Math.Max(0, radius - 1);

        var bg = IsEnabled ? Background : theme.TextBoxDisabledBackground;
        if (radius > 0)
            context.FillRoundedRectangle(bounds, radius, radius, bg);
        else
            context.FillRectangle(bounds, bg);

        var borderColor = BorderBrush;
        if (IsEnabled)
        {
            if (IsFocused)
                borderColor = theme.Accent;
            else if (IsMouseOver)
                borderColor = BorderBrush.Lerp(theme.Accent, 0.6);
        }
        if (BorderThickness > 0)
        {
            var stroke = Math.Max(1, BorderThickness);
            if (radius > 0)
                context.DrawRoundedRectangle(bounds, radius, radius, borderColor, stroke);
            else
                context.DrawRectangle(bounds, borderColor, stroke);
        }

        if (_items.Count == 0)
            return;

        var borderInset = GetBorderVisualInset();
        var contentBounds = bounds.Deflate(Padding).Deflate(new Thickness(borderInset));
        context.Save();
        context.SetClip(contentBounds);

        var font = GetFont();
        double itemHeight = ResolveItemHeight();

        for (int i = 0; i < _items.Count; i++)
        {
            double y = contentBounds.Y + i * itemHeight;
            var itemRect = new Rect(contentBounds.X, y, contentBounds.Width, itemHeight);

            bool selected = i == SelectedIndex;
            if (selected)
            {
                var selectionBg = theme.SelectionBackground;
                if (itemRadius > 0)
                    context.FillRoundedRectangle(itemRect, itemRadius, itemRadius, selectionBg);
                else
                    context.FillRectangle(itemRect, selectionBg);
            }

            var textColor = selected ? theme.SelectionText : (IsEnabled ? Foreground : theme.DisabledText);
            context.DrawText(_items[i] ?? string.Empty, itemRect.Deflate(new Thickness(2, 0, 2, 0)), font, textColor,
                TextAlignment.Left, TextAlignment.Center, TextWrapping.NoWrap);
        }

        context.Restore();
    }

    internal override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (!IsEnabled || e.Button != MouseButton.Left)
            return;

        Focus();

        var contentBounds = Bounds.Deflate(Padding).Deflate(new Thickness(GetBorderVisualInset()));
        int index = (int)((e.Position.Y - contentBounds.Y) / ResolveItemHeight());
        if (index >= 0 && index < _items.Count)
        {
            SelectedIndex = index;
            e.Handled = true;
        }
    }

    internal override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!IsEnabled)
            return;

        if (e.Key == Native.Constants.VirtualKeys.VK_UP)
        {
            if (_items.Count > 0)
                SelectedIndex = Math.Max(0, SelectedIndex <= 0 ? 0 : SelectedIndex - 1);
            e.Handled = true;
        }
        else if (e.Key == Native.Constants.VirtualKeys.VK_DOWN)
        {
            if (_items.Count > 0)
                SelectedIndex = Math.Min(_items.Count - 1, SelectedIndex < 0 ? 0 : SelectedIndex + 1);
            e.Handled = true;
        }
    }

    public void AddItem(string item)
    {
        _items.Add(item ?? string.Empty);
        InvalidateMeasure();
        InvalidateVisual();
    }

    public void ClearItems()
    {
        _items.Clear();
        SelectedIndex = -1;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private double ResolveItemHeight()
    {
        if (!double.IsNaN(ItemHeight) && ItemHeight > 0)
            return ItemHeight;
        return Math.Max(18, FontSize + 6);
    }

    public ListBox BindSelectedIndex(ObservableValue<int> source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        _selectedIndexBinding?.Dispose();
        _selectedIndexBinding = new ValueBinding<int>(
            get: () => source.Value,
            set: v => source.Value = v,
            subscribe: h => source.Changed += h,
            unsubscribe: h => source.Changed -= h,
            onSourceChanged: () =>
            {
                _updatingFromSource = true;
                try { SelectedIndex = source.Value; }
                finally { _updatingFromSource = false; }
            });

        var existing = SelectionChanged;
        SelectionChanged = i =>
        {
            existing?.Invoke(i);

            if (_updatingFromSource)
                return;

            _selectedIndexBinding?.Set(i);
        };

        _updatingFromSource = true;
        try { SelectedIndex = source.Value; }
        finally { _updatingFromSource = false; }

        return this;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _selectedIndexBinding?.Dispose();
        _selectedIndexBinding = null;
        _disposed = true;
    }
}
