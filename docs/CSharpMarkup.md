# C# Markup Guide

MewUI's C# Markup is a Fluent API that allows you to declaratively build UI with pure C# code without XAML.
It is compatible with Native AOT compilation and does not use Reflection.

## Concept

### Why C# Markup?

- **Native AOT Compatible**: Everything is determined at compile time without Reflection
- **Type Safety**: Compiler catches errors
- **IntelliSense**: IDE auto-completion support
- **Code Reuse**: Extract UI components as regular C# methods

### Basic Pattern

```csharp
new Button()
    .Content("Click Me")
    .Width(100)
    .OnClick(() => Console.WriteLine("Clicked!"))
```

All extension methods return `this` to enable method chaining.

## Naming Policy

### Property Setting
| Pattern | Description | Example |
|---------|-------------|---------|
| `PropertyName(value)` | Set property directly | `.Width(100)`, `.Text("Hello")` |
| `PropertyName()` | Set bool property to true | `.Bold()`, `.IsChecked()` |

### Event Handlers
| Pattern | Description | Example |
|---------|-------------|---------|
| `OnEventName(handler)` | Register event handler | `.OnClick(...)`, `.OnTextChanged(...)` |
| `OnCanEventName(func)` | Conditional execution (Commanding) | `.OnCanClick(() => isValid)` |

### Data Binding
| Pattern | Description | Example |
|---------|-------------|---------|
| `BindPropertyName(source)` | ObservableValue binding | `.BindText(vm.Name)` |
| `BindPropertyName(source, converter)` | Conversion binding | `.BindText(vm.Count, c => $"{c} items")` |

### Shortcut Methods
Frequently used properties have concise shortcut methods:
- `.Bold()` → `.FontWeight(FontWeight.Bold)`
- `.Horizontal()` → `.Orientation(Orientation.Horizontal)`
- `.Center()` → `.HorizontalAlignment(Center).VerticalAlignment(Center)`

---

## Common Extension Methods

### FluentExtensions (All Reference Types)

| Method | Description |
|--------|-------------|
| `Ref(out T field)` | Store reference in variable |

```csharp
new TextBox()
    .Ref(out var nameBox)  // Store reference in nameBox variable
    .Text("Hello")
```

---

## Element Extension Methods

Base class for all UI elements.

### DockPanel Attached Properties

| Method | Description |
|--------|-------------|
| `DockTo(Dock dock)` | Set Dock position |
| `DockLeft()` | Dock left |
| `DockTop()` | Dock top |
| `DockRight()` | Dock right |
| `DockBottom()` | Dock bottom |

### Grid Attached Properties

| Method | Description |
|--------|-------------|
| `Row(int row)` | Grid row position |
| `Column(int column)` | Grid column position |
| `RowSpan(int rowSpan)` | Row span |
| `ColumnSpan(int columnSpan)` | Column span |
| `GridPosition(row, column)` | Set row/column together |
| `GridPosition(row, column, rowSpan, columnSpan)` | Set full position |

### Canvas Attached Properties

| Method | Description |
|--------|-------------|
| `CanvasLeft(double left)` | Left offset |
| `CanvasTop(double top)` | Top offset |
| `CanvasRight(double right)` | Right offset |
| `CanvasBottom(double bottom)` | Bottom offset |
| `CanvasPosition(left, top)` | Set position |

---

## FrameworkElement Extension Methods

Base class for all layout-capable elements.

### Size

| Method | Description |
|--------|-------------|
| `Width(double)` | Width |
| `Height(double)` | Height |
| `Size(width, height)` | Set width/height together |
| `Size(double)` | Square size |
| `MinWidth(double)` | Minimum width |
| `MinHeight(double)` | Minimum height |
| `MaxWidth(double)` | Maximum width |
| `MaxHeight(double)` | Maximum height |

### Margin

| Method | Description |
|--------|-------------|
| `Margin(uniform)` | Uniform margin |
| `Margin(horizontal, vertical)` | Horizontal/vertical margin |
| `Margin(left, top, right, bottom)` | Individual margin |
| `Padding(uniform)` | Uniform padding |
| `Padding(horizontal, vertical)` | Horizontal/vertical padding |
| `Padding(left, top, right, bottom)` | Individual padding |

### Alignment

| Method | Description |
|--------|-------------|
| `HorizontalAlignment(alignment)` | Horizontal alignment |
| `VerticalAlignment(alignment)` | Vertical alignment |
| `Center()` | Center alignment (horizontal+vertical) |
| `CenterHorizontal()` | Horizontal center |
| `CenterVertical()` | Vertical center |
| `Left()` | Left alignment |
| `Right()` | Right alignment |
| `Top()` | Top alignment |
| `Bottom()` | Bottom alignment |
| `StretchHorizontal()` | Horizontal stretch |
| `StretchVertical()` | Vertical stretch |

---

## UIElement Extension Methods

Base class for all elements that handle input events.

### Binding

| Method | Description |
|--------|-------------|
| `BindIsVisible(ObservableValue<bool>)` | Visibility binding |
| `BindIsEnabled(ObservableValue<bool>)` | Enabled binding |

### Focus Events

| Method | Description |
|--------|-------------|
| `OnGotFocus(Action)` | Focus gained |
| `OnLostFocus(Action)` | Focus lost |

### Mouse Events

| Method | Description |
|--------|-------------|
| `OnMouseEnter(Action)` | Mouse enter |
| `OnMouseLeave(Action)` | Mouse leave |
| `OnMouseDown(Action<MouseEventArgs>)` | Mouse button down |
| `OnMouseUp(Action<MouseEventArgs>)` | Mouse button up |
| `OnMouseMove(Action<MouseEventArgs>)` | Mouse move |
| `OnMouseWheel(Action<MouseWheelEventArgs>)` | Mouse wheel |

### Keyboard Events

| Method | Description |
|--------|-------------|
| `OnKeyDown(Action<KeyEventArgs>)` | Key down |
| `OnKeyUp(Action<KeyEventArgs>)` | Key up |
| `OnTextInput(Action<TextInputEventArgs>)` | Text input |

---

## Control Extension Methods

Base class for all controls with visual styling.

### Colors

| Method | Description |
|--------|-------------|
| `Background(Color)` | Background color |
| `Foreground(Color)` | Foreground color (text) |
| `BorderBrush(Color)` | Border color |
| `BorderThickness(double)` | Border thickness |

### Font

| Method | Description |
|--------|-------------|
| `FontFamily(string)` | Font name |
| `FontSize(double)` | Font size |
| `FontWeight(FontWeight)` | Font weight |
| `Bold()` | Bold (shortcut) |

---

## Individual Control Extension Methods

### Window

```csharp
new Window()
    .Title("My App")
    .Resizable(800, 600)
    .Content(...)
    .OnLoaded(() => ...)
    .OnClosed(() => ...)
```

| Method | Description |
|--------|-------------|
| `Title(string)` | Window title |
| `Width(double)` | Window width |
| `Height(double)` | Window height |
| `Size(width, height)` | Window size |
| `Resizable(width, height)` | Resizable |
| `Fixed(width, height)` | Fixed size |
| `FitContentWidth(maxWidth, fixedHeight)` | Fit content (width) |
| `FitContentHeight(fixedWidth, maxHeight)` | Fit content (height) |
| `FitContentSize(maxWidth, maxHeight)` | Fit content |
| `Content(Element)` | Window content |
| `OnLoaded(Action)` | Load completed |
| `OnClosed(Action)` | Window closed |
| `OnActivated(Action)` | Window activated |
| `OnDeactivated(Action)` | Window deactivated |
| `OnSizeChanged(Action<Size>)` | Size changed |
| `OnDpiChanged(Action<uint, uint>)` | DPI changed |
| `OnThemeChanged(Action<Theme, Theme>)` | Theme changed |
| `OnFirstFrameRendered(Action)` | First frame rendered |
| `OnPreviewKeyDown(Action<KeyEventArgs>)` | Key down (preview) |
| `OnPreviewKeyUp(Action<KeyEventArgs>)` | Key up (preview) |
| `OnPreviewTextInput(Action<TextInputEventArgs>)` | Text input (preview) |

### Label

```csharp
new Label()
    .Text("Hello World")
    .Bold()
    .FontSize(16)
```

| Method | Description |
|--------|-------------|
| `Text(string)` | Text content |
| `TextAlignment(TextAlignment)` | Horizontal text alignment |
| `VerticalTextAlignment(TextAlignment)` | Vertical text alignment |
| `TextWrapping(TextWrapping)` | Text wrapping |
| `BindText(ObservableValue<string>)` | Text binding |
| `BindText(source, converter)` | Conversion binding |

### Button

```csharp
new Button()
    .Content("Click Me")
    .OnCanClick(() => isFormValid)
    .OnClick(() => Submit())
```

| Method | Description |
|--------|-------------|
| `Content(string)` | Button text |
| `OnClick(Action)` | Click handler |
| `OnCanClick(Func<bool>)` | Click condition (Commanding) |
| `BindContent(ObservableValue<string>)` | Content binding |

### TextBox

```csharp
new TextBox()
    .Placeholder("Enter name...")
    .BindText(vm.Name)
```

| Method | Description |
|--------|-------------|
| `Text(string)` | Text content |
| `Placeholder(string)` | Placeholder |
| `IsReadOnly(bool)` | Read only |
| `AcceptTab(bool)` | Accept tab key |
| `OnTextChanged(Action<string>)` | Text changed handler |
| `BindText(ObservableValue<string>)` | Text binding (two-way) |

### MultiLineTextBox

```csharp
new MultiLineTextBox()
    .Placeholder("Enter notes...")
    .Wrap(true)
    .Height(100)
```

| Method | Description |
|--------|-------------|
| `Text(string)` | Text content |
| `Placeholder(string)` | Placeholder |
| `IsReadOnly(bool)` | Read only |
| `AcceptTab(bool)` | Accept tab key |
| `Wrap(bool)` | Word wrap |
| `OnWrapChanged(Action<bool>)` | Wrap changed handler |
| `OnTextChanged(Action<string>)` | Text changed handler |
| `BindText(ObservableValue<string>)` | Text binding |

### CheckBox

```csharp
new CheckBox()
    .Text("Enable feature")
    .BindIsChecked(vm.IsEnabled)
```

| Method | Description |
|--------|-------------|
| `Text(string)` | Label text |
| `IsChecked(bool)` | Checked state |
| `OnCheckedChanged(Action<bool>)` | Checked changed handler |
| `BindIsChecked(ObservableValue<bool>)` | Checked binding |
| `BindWrap(MultiLineTextBox)` | Link to Wrap property |

### RadioButton

```csharp
new RadioButton()
    .Text("Option A")
    .GroupName("options")
    .IsChecked(true)
```

| Method | Description |
|--------|-------------|
| `Text(string)` | Label text |
| `GroupName(string?)` | Group name (only one selected in same group) |
| `IsChecked(bool)` | Selected state |
| `OnCheckedChanged(Action<bool>)` | Selection changed handler |
| `BindIsChecked(ObservableValue<bool>)` | Selection binding |

### ToggleSwitch

```csharp
new ToggleSwitch()
    .Text("Dark Mode")
    .BindIsChecked(vm.IsDarkMode)
```

| Method | Description |
|--------|-------------|
| `Text(string)` | Label text |
| `IsChecked(bool)` | Toggle state |
| `OnCheckedChanged(Action<bool>)` | Toggle changed handler |
| `BindIsChecked(ObservableValue<bool>)` | Toggle binding |

### ListBox

```csharp
new ListBox()
    .Items("Apple", "Banana", "Cherry")
    .SelectedIndex(0)
    .Height(120)
```

| Method | Description |
|--------|-------------|
| `Items(params string[])` | Item list |
| `ItemHeight(double)` | Item height |
| `ItemPadding(Thickness)` | Item padding |
| `SelectedIndex(int)` | Selected index |
| `OnSelectionChanged(Action<int>)` | Selection changed handler |
| `BindSelectedIndex(ObservableValue<int>)` | Selection binding |

### ComboBox

```csharp
new ComboBox()
    .Items("Small", "Medium", "Large")
    .Placeholder("Select size...")
    .SelectedIndex(1)
```

| Method | Description |
|--------|-------------|
| `Items(params string[])` | Item list |
| `SelectedIndex(int)` | Selected index |
| `Placeholder(string)` | Placeholder |
| `OnSelectionChanged(Action<int>)` | Selection changed handler |
| `BindSelectedIndex(ObservableValue<int>)` | Selection binding |

### Slider

```csharp
new Slider()
    .Minimum(0)
    .Maximum(100)
    .BindValue(vm.Volume)
```

| Method | Description |
|--------|-------------|
| `Minimum(double)` | Minimum value |
| `Maximum(double)` | Maximum value |
| `Value(double)` | Current value |
| `SmallChange(double)` | Small change unit |
| `OnValueChanged(Action<double>)` | Value changed handler |
| `BindValue(ObservableValue<double>)` | Value binding |

### ProgressBar

```csharp
new ProgressBar()
    .Minimum(0)
    .Maximum(100)
    .BindValue(vm.Progress)
```

| Method | Description |
|--------|-------------|
| `Minimum(double)` | Minimum value |
| `Maximum(double)` | Maximum value |
| `Value(double)` | Current value |
| `BindValue(ObservableValue<double>)` | Value binding |

### Image

```csharp
new Image()
    .SourceFile("logo.png")
    .Size(64, 64)
    .StretchMode(ImageStretch.Uniform)
```

| Method | Description |
|--------|-------------|
| `Source(ImageSource?)` | Image source |
| `SourceFile(string path)` | Load from file |
| `SourceResource(Assembly, string)` | Load from resource |
| `SourceResource<TAnchor>(string)` | Load from resource (generic) |
| `StretchMode(ImageStretch)` | Stretch mode |

### TabControl

```csharp
new TabControl()
    .TabItems(
        new TabItem().Header("Home").Content(...),
        new TabItem().Header("Settings").Content(...)
    )
```

| Method | Description |
|--------|-------------|
| `TabItems(params TabItem[])` | Tab item list |
| `Tab(header, content)` | Add tab (string header) |
| `Tab(Element header, content)` | Add tab (element header) |
| `SelectedIndex(int)` | Selected tab index |
| `OnSelectionChanged(Action<int>)` | Tab changed handler |
| `VerticalScroll(ScrollMode)` | Vertical scroll |
| `HorizontalScroll(ScrollMode)` | Horizontal scroll |
| `AutoVerticalScroll()` | Auto vertical scroll |
| `AutoHorizontalScroll()` | Auto horizontal scroll |

### TabItem

```csharp
new TabItem()
    .Header("Settings")
    .Content(new StackPanel().Children(...))
    .IsEnabled(true)
```

| Method | Description |
|--------|-------------|
| `Header(string)` | Header text |
| `Header(Element)` | Header element |
| `Content(Element)` | Tab content |
| `IsEnabled(bool)` | Enabled state |

### GroupBox (HeaderedContentControl)

```csharp
new GroupBox()
    .Header("Options")
    .Content(new StackPanel().Children(...))
```

| Method | Description |
|--------|-------------|
| `Header(string)` | Header text (Bold style) |
| `Header(Element)` | Header element |
| `HeaderSpacing(double)` | Header-content spacing |
| `Content(Element)` | Group content |

### ScrollViewer

```csharp
new ScrollViewer()
    .AutoVerticalScroll()
    .NoHorizontalScroll()
    .Content(...)
```

| Method | Description |
|--------|-------------|
| `VerticalScroll(ScrollMode)` | Vertical scroll mode |
| `HorizontalScroll(ScrollMode)` | Horizontal scroll mode |
| `Scroll(vertical, horizontal)` | Set scroll modes together |
| `NoVerticalScroll()` | Disable vertical scroll |
| `AutoVerticalScroll()` | Auto vertical scroll |
| `ShowVerticalScroll()` | Always show vertical scroll |
| `NoHorizontalScroll()` | Disable horizontal scroll |
| `AutoHorizontalScroll()` | Auto horizontal scroll |
| `ShowHorizontalScroll()` | Always show horizontal scroll |
| `Content(Element)` | Content to scroll |

---

## Panel Extension Methods

### Panel (Common)

| Method | Description |
|--------|-------------|
| `Children(params Element[])` | Add child elements |

### StackPanel

```csharp
new StackPanel()
    .Vertical()
    .Spacing(8)
    .Children(
        new Label().Text("First"),
        new Label().Text("Second")
    )
```

| Method | Description |
|--------|-------------|
| `Orientation(Orientation)` | Direction |
| `Horizontal()` | Horizontal direction (shortcut) |
| `Vertical()` | Vertical direction (shortcut) |
| `Spacing(double)` | Spacing between elements |

### Grid

```csharp
new Grid()
    .Rows("Auto,*,Auto")
    .Columns("100,*")
    .Spacing(8)
    .AutoIndexing()
    .Children(
        new Label().Text("Name:"),
        new TextBox()
    )
```

| Method | Description |
|--------|-------------|
| `Rows(params GridLength[])` | Row definitions |
| `Rows(string)` | Row definitions (string: "Auto,*,2*,100") |
| `Columns(string)` | Column definitions (string) |
| `Spacing(double)` | Cell spacing |
| `AutoIndexing(bool)` | Auto indexing (Row/Column auto-increment) |

**GridLength String Syntax:**
- `Auto` - Fit to content
- `*` - 1 star ratio
- `2*` - 2 star ratio
- `100` - 100 pixels

### UniformGrid

```csharp
new UniformGrid()
    .Columns(3)
    .Spacing(8)
    .Children(
        new Button().Content("1"),
        new Button().Content("2"),
        new Button().Content("3")
    )
```

| Method | Description |
|--------|-------------|
| `Rows(int)` | Row count |
| `Columns(int)` | Column count |
| `Spacing(double)` | Cell spacing |

### WrapPanel

```csharp
new WrapPanel()
    .Orientation(Orientation.Horizontal)
    .Spacing(8)
    .ItemWidth(100)
    .ItemHeight(100)
    .Children(...)
```

| Method | Description |
|--------|-------------|
| `Orientation(Orientation)` | Direction |
| `Spacing(double)` | Spacing between elements |
| `ItemWidth(double)` | Item width |
| `ItemHeight(double)` | Item height |

### DockPanel

```csharp
new DockPanel()
    .LastChildFill()
    .Spacing(8)
    .Children(
        new Label().Text("Header").DockTop(),
        new Label().Text("Footer").DockBottom(),
        new Label().Text("Content")  // Fills remaining space
    )
```

| Method | Description |
|--------|-------------|
| `LastChildFill(bool)` | Last child fills remaining space |
| `Spacing(double)` | Spacing between elements |

---

## Commanding (CanExecute Pattern)

You can implement a pattern similar to WPF ICommand using Button's `OnCanClick`.

```csharp
var text = new ObservableValue<string>("");

new TextBox()
    .BindText(text)
    .OnTextChanged(_ => window.RequerySuggested()),

new Button()
    .Content("Submit")
    .OnCanClick(() => !string.IsNullOrWhiteSpace(text.Value))
    .OnClick(() => Submit(text.Value))
```

### Automatic Re-evaluation Timing

`CanClick` is automatically re-evaluated at these times:
- **Focus change** - When focus moves
- **MouseUp** - When mouse button is released
- **KeyUp** - When key is released

### Manual Re-evaluation

When manual re-evaluation is needed after state changes:

```csharp
// After state change in event handler
counter.Value++;
window.RequerySuggested();  // Trigger CanClick re-evaluation
```

---

## Apply Pattern

Use the `Apply` pattern for complex initialization or unsupported property settings:

```csharp
public static T Apply<T>(this T obj, Action<T> action)
{
    action(obj);
    return obj;
}

// Usage example
new TextBox()
    .OnTextChanged(text => Console.WriteLine(text))
    .Apply(tb => tb.MaxLength = 100)
```
