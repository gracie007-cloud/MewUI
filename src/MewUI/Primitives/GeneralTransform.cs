namespace Aprillz.MewUI;

/// <summary>
/// Represents a general point/rect transform between visual coordinate spaces.
/// This is a minimal WPF-compatible shape (translation-only for now).
/// </summary>
public abstract class GeneralTransform
{
    public abstract Point Transform(Point point);

    public virtual Rect TransformBounds(Rect rect) =>
        new(Transform(rect.Position), rect.Size);

    public virtual bool TryTransform(Point inPoint, out Point result)
    {
        result = Transform(inPoint);
        return true;
    }

    public abstract GeneralTransform Inverse { get; }
}

internal sealed class IdentityGeneralTransform : GeneralTransform
{
    public static readonly IdentityGeneralTransform Instance = new();

    private IdentityGeneralTransform() { }

    public override Point Transform(Point point) => point;

    public override Rect TransformBounds(Rect rect) => rect;

    public override GeneralTransform Inverse => this;
}

internal sealed class TranslateGeneralTransform : GeneralTransform
{
    private readonly double _dx;
    private readonly double _dy;

    public TranslateGeneralTransform(double dx, double dy)
    {
        _dx = dx;
        _dy = dy;
    }

    public override Point Transform(Point point) =>
        new(point.X + _dx, point.Y + _dy);

    public override Rect TransformBounds(Rect rect) =>
        rect.Offset(_dx, _dy);

    public override GeneralTransform Inverse =>
        new TranslateGeneralTransform(-_dx, -_dy);
}

