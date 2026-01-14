# Data Binding Guide

MewUI's data binding system is designed with a delegate-based approach without Reflection to be compatible with Native AOT.

## Core Concepts

### Reflection-Free Binding

Unlike WPF/WinUI, MewUI does not use Reflection:

| WPF Approach | MewUI Approach |
|--------------|----------------|
| `{Binding PropertyName}` | `.BindText(vm.PropertyName)` |
| `INotifyPropertyChanged` | `ObservableValue<T>` |
| PropertyPath strings | Direct lambda/delegate |

Benefits of this approach:
- **Native AOT Compatible**: Safe for trimming/AOT
- **Compile-time Validation**: Prevents property name typos
- **IntelliSense Support**: Auto-completion available
- **Refactoring Safe**: Automatically reflects name changes

---

## ObservableValue\<T>

A reactive value container that automatically updates the UI when the value changes.

### Basic Usage

```csharp
// Creation
var name = new ObservableValue<string>("Default");
var count = new ObservableValue<int>(0);
var isEnabled = new ObservableValue<bool>(true);

// Read/Write values
string currentName = name.Value;
name.Value = "New Value";

// Change detection
name.Changed += () => Console.WriteLine("Name changed!");
```

### Constructor Options

```csharp
public ObservableValue(
    T initialValue = default!,      // Initial value
    Func<T, T>? coerce = null,      // Value transform/constraint function
    IEqualityComparer<T>? comparer = null  // Equality comparer
)
```

### Coerce (Value Constraints)

Automatically transforms values to a valid range when changed:

```csharp
// Constrain to 0-100
var percent = new ObservableValue<double>(50, v => Math.Clamp(v, 0, 100));

percent.Value = 150;  // Automatically converted to 100
percent.Value = -10;  // Automatically converted to 0

// Prevent negative selection index
var selectedIndex = new ObservableValue<int>(-1, v => Math.Max(-1, v));

// String trim
var text = new ObservableValue<string>("", v => v?.Trim() ?? "");
```

### Methods

| Method | Description |
|--------|-------------|
| `Value { get; set; }` | Current value |
| `Set(T value)` | Set value (returns true if changed) |
| `NotifyChanged()` | Manually raise Changed event |
| `Subscribe(Action)` | Subscribe to change event |
| `Unsubscribe(Action)` | Unsubscribe from change event |

### Events

```csharp
var counter = new ObservableValue<int>(0);

// Subscribe to event
counter.Changed += OnCounterChanged; // Not typically used directly

// Unsubscribe from event
counter.Changed -= OnCounterChanged; // Not typically used directly

void OnCounterChanged()
{
    Console.WriteLine($"Counter is now: {counter.Value}");
}
```

---

## Control Binding

### One-Way Binding (Source → UI)

When the value changes, the UI updates automatically:

```csharp
var message = new ObservableValue<string>("Hello");

new Label()
    .BindText(message)  // Label auto-updates when message changes

// Value change → UI auto-reflects
message.Value = "World";
```

### Two-Way Binding (Source ↔ UI)

UI changes reflect to the source, and source changes reflect to the UI:

```csharp
var userName = new ObservableValue<string>("");

new TextBox()
    .BindText(userName)  // Two-way binding

// Change from code → UI reflects
userName.Value = "John";

// Input from UI → Source reflects
// (userName.Value auto-updates when user types in TextBox)
```

### Conversion Binding

Displays values converted to a different format.

**Function Signature Comparison:**
```csharp
// Regular binding - same type (ObservableValue<string> → string)
Label BindText(
    this Label label,
    ObservableValue<string> source)

// Conversion binding - different type (ObservableValue<T> → string)
Label BindText<TSource>(
    this Label label,
    ObservableValue<TSource> source,    // Source type (int, decimal, User, etc.)
    Func<TSource, string> convert)       // Convert function: TSource → string
```

| Category | Regular Binding | Conversion Binding |
|----------|-----------------|-------------------|
| Source Type | `ObservableValue<string>` | `ObservableValue<T>` (any type) |
| Additional Parameter | None | `Func<T, string> convert` |
| Use Case | Display string directly | Convert numbers, objects, etc. to string |

**Usage Examples:**
```csharp
var count = new ObservableValue<int>(5);
var price = new ObservableValue<decimal>(1234.56m);

// int → string conversion
new Label()
    .BindText(count, c => $"Items: {c}")
    //        ↑       ↑
    //   source    converter: Func<int, string>

// decimal → formatting
new Label()
    .BindText(price, p => p.ToString("C"))  // $1,234.56
    //        ↑      ↑
    //   source   converter: Func<decimal, string>

// Complex conversion
var user = new ObservableValue<User?>(null);
new Label()
    .BindText(user, u => u?.FullName ?? "Guest")
    //        ↑     ↑
    //   source  converter: Func<User?, string>
```

---

## Binding Methods by Control

### Label

| Method | Signature | Direction |
|--------|-----------|-----------|
| `BindText` | `Label BindText(ObservableValue<string> source)` | One-Way |
| `BindText` | `Label BindText<T>(ObservableValue<T> source, Func<T, string> convert)` | One-Way |

```csharp
var text = new ObservableValue<string>("Hello");
var count = new ObservableValue<int>(42);

new Label().BindText(text)                      // Direct binding
new Label().BindText(count, c => $"Count: {c}") // Conversion binding
```

### TextBox / MultiLineTextBox

| Method | Signature | Direction |
|--------|-----------|-----------|
| `BindText` | `TextBox BindText(ObservableValue<string> source)` | Two-Way |

```csharp
var input = new ObservableValue<string>("");

new TextBox().BindText(input)
new MultiLineTextBox().BindText(input)
```

### Button

| Method | Signature | Direction |
|--------|-----------|-----------|
| `BindContent` | `Button BindContent(ObservableValue<string> source)` | One-Way |
| `BindContent` | `Button BindContent<T>(ObservableValue<T> source, Func<T, string> convert)` | One-Way |

```csharp
var buttonText = new ObservableValue<string>("Click Me");

new Button().BindContent(buttonText)
new Button().BindContent(buttonText, t => $"[{t}]")     // Conversion binding
```

### CheckBox / RadioButton / ToggleSwitch

| Method | Signature | Direction |
|--------|-----------|-----------|
| `BindIsChecked` | `T BindIsChecked<T>(ObservableValue<bool> source)` | Two-Way |

```csharp
var isChecked = new ObservableValue<bool>(false);

new CheckBox().BindIsChecked(isChecked)
new RadioButton().BindIsChecked(isChecked)
new ToggleSwitch().BindIsChecked(isChecked)
```

### ListBox / ComboBox

| Method | Signature | Direction |
|--------|-----------|-----------|
| `BindSelectedIndex` | `T BindSelectedIndex<T>(ObservableValue<int> source)` | Two-Way |

```csharp
var selectedIndex = new ObservableValue<int>(0);

new ListBox()
    .Items("A", "B", "C")
    .BindSelectedIndex(selectedIndex)

new ComboBox()
    .Items("Small", "Medium", "Large")
    .BindSelectedIndex(selectedIndex)
```

### Slider

| Method | Signature | Direction |
|--------|-----------|-----------|
| `BindValue` | `Slider BindValue(ObservableValue<double> source)` | Two-Way |

```csharp
var volume = new ObservableValue<double>(50);

new Slider()
    .Minimum(0)
    .Maximum(100)
    .BindValue(volume)
```

### ProgressBar

| Method | Signature | Direction |
|--------|-----------|-----------|
| `BindValue` | `ProgressBar BindValue(ObservableValue<double> source)` | One-Way |

```csharp
var progress = new ObservableValue<double>(0);

new ProgressBar()
    .Minimum(0)
    .Maximum(100)
    .BindValue(progress)
```

### UIElement (Common)

| Method | Signature | Direction |
|--------|-----------|-----------|
| `BindIsVisible` | `T BindIsVisible<T>(ObservableValue<bool> source) where T : UIElement` | One-Way |
| `BindIsEnabled` | `T BindIsEnabled<T>(ObservableValue<bool> source) where T : UIElement` | One-Way |

```csharp
var isVisible = new ObservableValue<bool>(true);
var isEnabled = new ObservableValue<bool>(true);

new Button()
    .BindIsVisible(isVisible)
    .BindIsEnabled(isEnabled)
```

---

## ViewModel Pattern

### Basic ViewModel

```csharp
class LoginViewModel
{
    public ObservableValue<string> Username { get; } = new("");
    public ObservableValue<string> Password { get; } = new("");
    public ObservableValue<bool> RememberMe { get; } = new(false);
    public ObservableValue<string> ErrorMessage { get; } = new("");
    public ObservableValue<bool> IsLoading { get; } = new(false);

    public void Login()
    {
        if (string.IsNullOrEmpty(Username.Value))
        {
            ErrorMessage.Value = "Username is required";
            return;
        }

        IsLoading.Value = true;
        // ... login logic
    }
}
```

### UI Binding

```csharp
var vm = new LoginViewModel();

new StackPanel()
    .Vertical()
    .Spacing(8)
    .Children(
        new TextBox()
            .Placeholder("Username")
            .BindText(vm.Username),

        new TextBox()
            .Placeholder("Password")
            .BindText(vm.Password),

        new CheckBox()
            .Text("Remember me")
            .BindIsChecked(vm.RememberMe),

        new Label()
            .Foreground(Colors.Red)
            .BindText(vm.ErrorMessage),

        new Button()
            .Content("Login")
            .OnCanClick(() => !vm.IsLoading.Value)
            .OnClick(() => vm.Login())
    )
```

---

## Computed Values

You can combine multiple ObservableValues to create derived values:

```csharp
var firstName = new ObservableValue<string>("");
var lastName = new ObservableValue<string>("");

// Label for derived value
new Label()
    .Apply(label =>
    {
        void UpdateFullName()
        {
            label.Text = $"{firstName.Value} {lastName.Value}".Trim();
        }

        firstName.Changed += UpdateFullName;
        lastName.Changed += UpdateFullName;
        UpdateFullName();
    })
```

### Helper Method Pattern

```csharp
// Reusable derived binding
public static Label BindFullName(this Label label,
    ObservableValue<string> firstName,
    ObservableValue<string> lastName)
{
    void Update() => label.Text = $"{firstName.Value} {lastName.Value}".Trim();

    firstName.Changed += Update;
    lastName.Changed += Update;
    Update();

    return label;
}

// Usage
new Label().BindFullName(vm.FirstName, vm.LastName)
```

---

## Collection Binding

Currently, MewUI does not directly support collection binding.
Instead, manually update the Items of ListBox/ComboBox:

```csharp
var items = new List<string> { "A", "B", "C" };
ListBox listBox = null!;

new ListBox()
    .Ref(out listBox)
    .Items(items.ToArray());

// Add item
items.Add("D");
listBox.AddItem("D");
listBox.InvalidateMeasure();

// Full refresh
listBox.ClearItems();
foreach (var item in items)
{
    listBox.AddItem(item);
}
listBox.InvalidateMeasure();
```

---

## ValueBinding\<T> (Advanced)

A low-level binding class used internally by controls.
Generally not needed directly, but useful for custom control development.

```csharp
public sealed class ValueBinding<T> : IDisposable
{
    public ValueBinding(
        Func<T> get,                    // Read value
        Action<T>? set,                 // Write value (null for one-way)
        Action<Action>? subscribe,      // Subscribe to changes
        Action<Action>? unsubscribe,    // Unsubscribe from changes
        Action onSourceChanged          // Callback when source changes
    );

    public T Get();
    public void Set(T value);
    public void Dispose();
}
```

### Implementing Binding in Custom Controls

```csharp
public class MyControl : Control
{
    private ValueBinding<string>? _textBinding;

    public void SetTextBinding(
        Func<string> get,
        Action<string>? set = null,
        Action<Action>? subscribe = null,
        Action<Action>? unsubscribe = null)
    {
        _textBinding?.Dispose();
        _textBinding = new ValueBinding<string>(
            get,
            set,
            subscribe,
            unsubscribe,
            onSourceChanged: () => Text = get());

        Text = get();
    }

    protected override void OnDispose()
    {
        _textBinding?.Dispose();
        _textBinding = null;
    }
}
```

---

## Memory Management

### Automatic Cleanup

Bindings are automatically cleaned up when controls are disposed:

```csharp
var vm = new ViewModel();
var textBox = new TextBox().BindText(vm.Name);

// Binding automatically unsubscribed when Window closes
// (Unsubscribes from Changed event)
```

### Manual Cleanup

You can manually unsubscribe when needed:

```csharp
var counter = new ObservableValue<int>(0);

void OnChanged() => Console.WriteLine(counter.Value);

counter.Subscribe(OnChanged);

// Unsubscribe later
counter.Unsubscribe(OnChanged);
```

---

## Best Practices

### 1. Use ObservableValue in ViewModel

```csharp
// Good
class ViewModel
{
    public ObservableValue<string> Name { get; } = new("");
}

// Bad - regular properties cannot be bound
class ViewModel
{
    public string Name { get; set; }
}
```

### 2. Ensure Validity with Coerce

```csharp
// Good - always valid value
var age = new ObservableValue<int>(0, v => Math.Clamp(v, 0, 150));

// Bad - invalid values can be set
var age = new ObservableValue<int>(0);
```

### 3. Separate Display Logic with Conversion Binding

```csharp
// Good - display logic in UI layer
new Label().BindText(vm.Price, p => $"${p:N0}")

// Bad - display logic mixed in ViewModel
class ViewModel
{
    public ObservableValue<string> FormattedPrice { get; }
}
```

### 4. Distinguish One-Way vs Two-Way Binding

```csharp
// One-way: Label, ProgressBar (display only)
new Label().BindText(vm.Status)
new ProgressBar().BindValue(vm.Progress)

// Two-way: TextBox, CheckBox, Slider (input)
new TextBox().BindText(vm.Name)
new CheckBox().BindIsChecked(vm.IsEnabled)
new Slider().BindValue(vm.Volume)
```
