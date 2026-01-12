namespace Aprillz.MewUI.Elements;

internal static class VisualTree
{
    public static void Visit(Element? element, Action<Element> visitor)
    {
        if (element == null)
        {
            return;
        }

        visitor(element);

        if (element is Panels.Panel panel)
        {
            foreach (var child in panel.Children)
            {
                Visit(child, visitor);
            }

            return;
        }

        if (element is Controls.ContentControl contentControl && contentControl.Content != null)
        {
            Visit(contentControl.Content, visitor);
        }
    }
}
