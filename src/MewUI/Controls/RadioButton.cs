using Aprillz.MewUI.Core;
using Aprillz.MewUI.Binding;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.Primitives;
using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.Controls;

public class RadioButton : Control, IDisposable
{
    private bool _isPressed;
    private bool _isChecked;
    private ValueBinding<bool>? _checkedBinding;
    private bool _updatingFromSource;
    private bool _disposed;

    public string Text
    {
        get;
        set { field = value ?? string.Empty; InvalidateMeasure(); }
    } = string.Empty;

    public string? GroupName
    {
        get;
        set { field = value; InvalidateVisual(); }
    }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value)
                return;

            SetChecked(value, notifyGroup: value);
        }
    }

    public Action<bool>? CheckedChanged { get; set; }

    public override bool Focusable => true;

    public RadioButton()
    {
        var theme = Theme.Current;
        Background = Color.Transparent;
        BorderBrush = theme.ControlBorder;
        BorderThickness = 1;
        Padding = new Thickness(2);
    }

    protected override void OnThemeChanged(Theme oldTheme, Theme newTheme)
    {
        if (BorderBrush == oldTheme.ControlBorder)
            BorderBrush = newTheme.ControlBorder;
        base.OnThemeChanged(oldTheme, newTheme);
    }

    private void SetChecked(bool value, bool notifyGroup)
    {
        _isChecked = value;
        CheckedChanged?.Invoke(value);
        InvalidateVisual();

        if (value && notifyGroup)
            UncheckOtherRadiosInGroup();
    }

    private void UncheckOtherRadiosInGroup()
    {
        var root = FindVisualRoot();
        if (root is not Window window || window.Content == null)
            return;

        string? group = string.IsNullOrWhiteSpace(GroupName) ? null : GroupName;
        var parent = Parent;

        Elements.VisualTree.Visit(window.Content, element =>
        {
            if (ReferenceEquals(element, this))
                return;

            if (element is not RadioButton rb)
                return;

            if (!rb._isChecked)
                return;

            if (group != null)
            {
                if (string.Equals(rb.GroupName, group, StringComparison.Ordinal))
                    rb.SetChecked(false, notifyGroup: false);
            }
            else
            {
                if (ReferenceEquals(rb.Parent, parent) && string.IsNullOrWhiteSpace(rb.GroupName))
                    rb.SetChecked(false, notifyGroup: false);
            }
        });
    }

    protected override Size MeasureContent(Size availableSize)
    {
        const double boxSize = 14;
        const double spacing = 6;

        double width = boxSize + spacing;
        double height = boxSize;

        if (!string.IsNullOrEmpty(Text))
        {
            using var measure = BeginTextMeasurement();
            var textSize = measure.Context.MeasureText(Text, measure.Font);
            width += textSize.Width;
            height = Math.Max(height, textSize.Height);
        }

        return new Size(width, height).Inflate(Padding);
    }

    protected override void OnRender(IGraphicsContext context)
    {
        var theme = GetTheme();
        var bounds = Bounds;
        var contentBounds = bounds.Deflate(Padding);

        const double boxSize = 14;
        const double spacing = 6;

        double boxY = contentBounds.Y + (contentBounds.Height - boxSize) / 2;
        var circleRect = new Rect(contentBounds.X, boxY, boxSize, boxSize);

        var fill = IsEnabled ? theme.ControlBackground : theme.TextBoxDisabledBackground;
        context.FillEllipse(circleRect, fill);

        var borderColor = BorderBrush;
        if (IsEnabled)
        {
            if (IsFocused || _isPressed)
                borderColor = theme.Accent;
            else if (IsMouseOver)
                borderColor = BorderBrush.Lerp(theme.Accent, 0.6);
        }
        context.DrawEllipse(circleRect, borderColor, Math.Max(1, BorderThickness));

        if (IsChecked)
        {
            var inner = circleRect.Inflate(-4, -4);
            context.FillEllipse(inner, theme.Accent);
        }

        if (!string.IsNullOrEmpty(Text))
        {
            var font = GetFont();
            var textColor = IsEnabled ? Foreground : theme.DisabledText;
            var textBounds = new Rect(contentBounds.X + boxSize + spacing, contentBounds.Y, contentBounds.Width - boxSize - spacing, contentBounds.Height);
            context.DrawText(Text, textBounds, font, textColor, TextAlignment.Left, TextAlignment.Center, TextWrapping.NoWrap);
        }
    }

    internal override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (!IsEnabled || e.Button != MouseButton.Left)
            return;

        _isPressed = true;
        Focus();

        var root = FindVisualRoot();
        if (root is Window window)
            window.CaptureMouse(this);

        InvalidateVisual();
        e.Handled = true;
    }

    internal override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button != MouseButton.Left || !_isPressed)
            return;

        _isPressed = false;

        var root = FindVisualRoot();
        if (root is Window window)
            window.ReleaseMouseCapture();

        if (IsEnabled && Bounds.Contains(e.Position))
            IsChecked = true;

        InvalidateVisual();
        e.Handled = true;
    }

    internal override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);

        if (!IsEnabled)
            return;

        if (e.Key == Input.Key.Space)
        {
            IsChecked = true;
            e.Handled = true;
        }
    }

    public RadioButton BindIsChecked(ObservableValue<bool> source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        _checkedBinding?.Dispose();
        _checkedBinding = new ValueBinding<bool>(
            get: () => source.Value,
            set: v => source.Value = v,
            subscribe: h => source.Changed += h,
            unsubscribe: h => source.Changed -= h,
            onSourceChanged: () =>
            {
                _updatingFromSource = true;
                try { IsChecked = source.Value; }
                finally { _updatingFromSource = false; }
            });

        var existing = CheckedChanged;
        CheckedChanged = v =>
        {
            existing?.Invoke(v);

            if (_updatingFromSource)
                return;

            _checkedBinding?.Set(v);
        };

        _updatingFromSource = true;
        try { IsChecked = source.Value; }
        finally { _updatingFromSource = false; }

        return this;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _checkedBinding?.Dispose();
        _checkedBinding = null;
        _disposed = true;
    }
}
