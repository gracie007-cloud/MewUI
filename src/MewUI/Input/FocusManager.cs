using Aprillz.MewUI.Elements;

namespace Aprillz.MewUI.Input;

/// <summary>
/// Manages keyboard focus within a window.
/// </summary>
public sealed class FocusManager
{
    private readonly Controls.Window _window;

    internal FocusManager(Controls.Window window)
    {
        _window = window;
    }

    /// <summary>
    /// Gets the currently focused element.
    /// </summary>
    public UIElement? FocusedElement { get; private set; }

    /// <summary>
    /// Sets focus to the specified element.
    /// </summary>
    public bool SetFocus(UIElement? element)
    {
        if (FocusedElement == element)
        {
            return true;
        }

        if (element != null && (!element.Focusable || !element.IsEffectivelyEnabled || !element.IsVisible))
        {
            return false;
        }

        var oldElement = FocusedElement;
        FocusedElement = element;

        oldElement?.SetFocused(false);
        element?.SetFocused(true);

        _window.RequerySuggested();

        return true;
    }

    /// <summary>
    /// Clears focus from the current element.
    /// </summary>
    public void ClearFocus() => SetFocus(null);

    /// <summary>
    /// Moves focus to the next focusable element.
    /// </summary>
    public bool MoveFocusNext()
    {
        var focusable = CollectFocusableElements(_window.Content);
        if (focusable.Count == 0)
        {
            return false;
        }

        int currentIndex = FocusedElement != null ? focusable.IndexOf(FocusedElement) : -1;
        int nextIndex = (currentIndex + 1) % focusable.Count;

        return SetFocus(focusable[nextIndex]);
    }

    /// <summary>
    /// Moves focus to the previous focusable element.
    /// </summary>
    public bool MoveFocusPrevious()
    {
        var focusable = CollectFocusableElements(_window.Content);
        if (focusable.Count == 0)
        {
            return false;
        }

        int currentIndex = FocusedElement != null ? focusable.IndexOf(FocusedElement) : focusable.Count;
        int prevIndex = (currentIndex - 1 + focusable.Count) % focusable.Count;

        return SetFocus(focusable[prevIndex]);
    }

    private List<UIElement> CollectFocusableElements(Element? root)
    {
        var result = new List<UIElement>();
        CollectFocusableElementsCore(root, result);
        return result;
    }

    private void CollectFocusableElementsCore(Element? element, List<UIElement> result)
    {
        if (element is UIElement uiElement)
        {
            if (uiElement.Focusable && uiElement.IsEffectivelyEnabled && uiElement.IsVisible)
            {
                result.Add(uiElement);
            }
        }

        if (element is Panels.Panel panel)
        {
            foreach (var child in panel.Children)
            {
                CollectFocusableElementsCore(child, result);
            }
        }
        else if (element is Controls.ContentControl contentControl && contentControl.Content != null)
        {
            CollectFocusableElementsCore(contentControl.Content, result);
        }
    }
}
