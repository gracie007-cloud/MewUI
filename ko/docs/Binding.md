# Data Binding Guide

MewUI의 데이터 바인딩 시스템은 Native AOT와 호환되도록 Reflection 없이 델리게이트 기반으로 설계되었습니다.

## 핵심 개념

### Reflection-Free 바인딩

WPF/WinUI와 달리 MewUI는 Reflection을 사용하지 않습니다:

| WPF 방식 | MewUI 방식 |
|----------|------------|
| `{Binding PropertyName}` | `.BindText(vm.PropertyName)` |
| `INotifyPropertyChanged` | `ObservableValue<T>` |
| PropertyPath 문자열 | 직접 람다/델리게이트 |

이 접근 방식의 장점:
- **Native AOT 호환**: 트리밍/AOT에서 안전
- **컴파일 타임 검증**: 속성 이름 오타 방지
- **IntelliSense 지원**: 자동완성 가능
- **리팩토링 안전**: 이름 변경 시 자동 반영

---

## ObservableValue\<T>

값 변경 시 자동으로 UI를 업데이트하는 반응형 값 컨테이너입니다.

### 기본 사용법

```csharp
// 생성
var name = new ObservableValue<string>("Default");
var count = new ObservableValue<int>(0);
var isEnabled = new ObservableValue<bool>(true);

// 값 읽기/쓰기
string currentName = name.Value;
name.Value = "New Value";

// 변경 감지
name.Changed += () => Console.WriteLine("Name changed!");
```

### 생성자 옵션

```csharp
public ObservableValue(
    T initialValue = default!,      // 초기값
    Func<T, T>? coerce = null,      // 값 변환/제한 함수
    IEqualityComparer<T>? comparer = null  // 동등성 비교자
)
```

### Coerce (값 제한)

값 변경 시 자동으로 유효한 범위로 변환합니다:

```csharp
// 0-100 사이로 제한
var percent = new ObservableValue<double>(50, v => Math.Clamp(v, 0, 100));

percent.Value = 150;  // 자동으로 100으로 변환
percent.Value = -10;  // 자동으로 0으로 변환

// 선택 인덱스 음수 방지
var selectedIndex = new ObservableValue<int>(-1, v => Math.Max(-1, v));

// 문자열 트림
var text = new ObservableValue<string>("", v => v?.Trim() ?? "");
```

### 메서드

| 메서드 | 설명 |
|--------|------|
| `Value { get; set; }` | 현재 값 |
| `Set(T value)` | 값 설정 (변경 시 true 반환) |
| `NotifyChanged()` | 수동으로 Changed 이벤트 발생 |
| `Subscribe(Action)` | 변경 이벤트 구독 |
| `Unsubscribe(Action)` | 변경 이벤트 구독 해제 |

### 이벤트

```csharp
var counter = new ObservableValue<int>(0);

// 이벤트 구독
counter.Changed += OnCounterChanged; // 일반적으로 직접 사용하지 않음

// 이벤트 해제
counter.Changed -= OnCounterChanged; // 일반적으로 직접 사용하지 않음

void OnCounterChanged()
{
    Console.WriteLine($"Counter is now: {counter.Value}");
}
```

---

## 컨트롤 바인딩

### 단방향 바인딩 (Source → UI)

값이 변경되면 UI가 자동으로 업데이트됩니다:

```csharp
var message = new ObservableValue<string>("Hello");

new Label()
    .BindText(message)  // message 변경 시 Label 자동 업데이트

// 값 변경 → UI 자동 반영
message.Value = "World";
```

### 양방향 바인딩 (Source ↔ UI)

UI 변경이 소스에 반영되고, 소스 변경이 UI에 반영됩니다:

```csharp
var userName = new ObservableValue<string>("");

new TextBox()
    .BindText(userName)  // 양방향 바인딩

// 코드에서 변경 → UI 반영
userName.Value = "John";

// UI에서 입력 → 소스 반영
// (사용자가 TextBox에 입력하면 userName.Value 자동 업데이트)
```

### 변환 바인딩

값을 다른 형식으로 변환하여 표시합니다.

**함수 시그니처 비교:**
```csharp
// 일반 바인딩 - 동일 타입 (ObservableValue<string> → string)
Label BindText(
    this Label label,
    ObservableValue<string> source)

// 변환 바인딩 - 다른 타입 (ObservableValue<T> → string)
Label BindText<TSource>(
    this Label label,
    ObservableValue<TSource> source,    // 소스 타입 (int, decimal, User 등)
    Func<TSource, string> convert)       // 변환 함수: TSource → string
```

| 구분 | 일반 바인딩 | 변환 바인딩 |
|------|-------------|-------------|
| 소스 타입 | `ObservableValue<string>` | `ObservableValue<T>` (모든 타입) |
| 추가 매개변수 | 없음 | `Func<T, string> convert` |
| 용도 | 문자열 직접 표시 | 숫자, 객체 등을 문자열로 변환 |

**사용 예:**
```csharp
var count = new ObservableValue<int>(5);
var price = new ObservableValue<decimal>(1234.56m);

// int → string 변환
new Label()
    .BindText(count, c => $"Items: {c}")
    //        ↑       ↑
    //   source    converter: Func<int, string>

// decimal → 포맷팅
new Label()
    .BindText(price, p => p.ToString("C"))  // ₩1,234.56
    //        ↑      ↑
    //   source   converter: Func<decimal, string>

// 복잡한 변환
var user = new ObservableValue<User?>(null);
new Label()
    .BindText(user, u => u?.FullName ?? "Guest")
    //        ↑     ↑
    //   source  converter: Func<User?, string>
```

---

## 컨트롤별 바인딩 메서드

### Label

| 메서드 | 시그니처 | 방향 |
|--------|----------|------|
| `BindText` | `Label BindText(ObservableValue<string> source)` | 단방향 |
| `BindText` | `Label BindText<T>(ObservableValue<T> source, Func<T, string> convert)` | 단방향 |

```csharp
var text = new ObservableValue<string>("Hello");
var count = new ObservableValue<int>(42);

new Label().BindText(text)                      // 직접 바인딩
new Label().BindText(count, c => $"Count: {c}") // 변환 바인딩
```

### TextBox / MultiLineTextBox

| 메서드 | 시그니처 | 방향 |
|--------|----------|------|
| `BindText` | `TextBox BindText(ObservableValue<string> source)` | 양방향 |

```csharp
var input = new ObservableValue<string>("");

new TextBox().BindText(input)
new MultiLineTextBox().BindText(input)
```

### Button

| 메서드 | 시그니처 | 방향 |
|--------|----------|------|
| `BindContent` | `Button BindContent(ObservableValue<string> source)` | 단방향 |
| `BindContent` | `Button BindContent<T>(ObservableValue<T> source, Func<T, string> convert)` | 단방향 |

```csharp
var buttonText = new ObservableValue<string>("Click Me");

new Button().BindContent(buttonText)
new Button().BindContent(buttonText, t => $"[{t}]")     // 변환 바인딩
```

### CheckBox / RadioButton / ToggleSwitch

| 메서드 | 시그니처 | 방향 |
|--------|----------|------|
| `BindIsChecked` | `T BindIsChecked<T>(ObservableValue<bool> source)` | 양방향 |

```csharp
var isChecked = new ObservableValue<bool>(false);

new CheckBox().BindIsChecked(isChecked)
new RadioButton().BindIsChecked(isChecked)
new ToggleSwitch().BindIsChecked(isChecked)
```

### ListBox / ComboBox

| 메서드 | 시그니처 | 방향 |
|--------|----------|------|
| `BindSelectedIndex` | `T BindSelectedIndex<T>(ObservableValue<int> source)` | 양방향 |

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

| 메서드 | 시그니처 | 방향 |
|--------|----------|------|
| `BindValue` | `Slider BindValue(ObservableValue<double> source)` | 양방향 |

```csharp
var volume = new ObservableValue<double>(50);

new Slider()
    .Minimum(0)
    .Maximum(100)
    .BindValue(volume)
```

### ProgressBar

| 메서드 | 시그니처 | 방향 |
|--------|----------|------|
| `BindValue` | `ProgressBar BindValue(ObservableValue<double> source)` | 단방향 |

```csharp
var progress = new ObservableValue<double>(0);

new ProgressBar()
    .Minimum(0)
    .Maximum(100)
    .BindValue(progress)
```

### UIElement (공통)

| 메서드 | 시그니처 | 방향 |
|--------|----------|------|
| `BindIsVisible` | `T BindIsVisible<T>(ObservableValue<bool> source) where T : UIElement` | 단방향 |
| `BindIsEnabled` | `T BindIsEnabled<T>(ObservableValue<bool> source) where T : UIElement` | 단방향 |

```csharp
var isVisible = new ObservableValue<bool>(true);
var isEnabled = new ObservableValue<bool>(true);

new Button()
    .BindIsVisible(isVisible)
    .BindIsEnabled(isEnabled)
```

---

## ViewModel 패턴

### 기본 ViewModel

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
        // ... 로그인 로직
    }
}
```

### UI 바인딩

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

## 파생 값 (Computed Values)

여러 ObservableValue를 조합하여 파생 값을 만들 수 있습니다:

```csharp
var firstName = new ObservableValue<string>("");
var lastName = new ObservableValue<string>("");

// 파생 값을 위한 Label
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

### 헬퍼 메서드 패턴

```csharp
// 재사용 가능한 파생 바인딩
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

// 사용
new Label().BindFullName(vm.FirstName, vm.LastName)
```

---

## 컬렉션 바인딩

현재 MewUI는 컬렉션 바인딩을 직접 지원하지 않습니다.
대신 수동으로 ListBox/ComboBox의 Items를 업데이트합니다:

```csharp
var items = new List<string> { "A", "B", "C" };
ListBox listBox = null!;

new ListBox()
    .Ref(out listBox)
    .Items(items.ToArray());

// 아이템 추가
items.Add("D");
listBox.AddItem("D");
listBox.InvalidateMeasure();

// 전체 새로고침
listBox.ClearItems();
foreach (var item in items)
{
    listBox.AddItem(item);
}
listBox.InvalidateMeasure();
```

---

## ValueBinding\<T> (고급)

컨트롤 내부에서 사용되는 저수준 바인딩 클래스입니다.
일반적으로 직접 사용할 필요는 없지만, 커스텀 컨트롤 개발 시 유용합니다.

```csharp
public sealed class ValueBinding<T> : IDisposable
{
    public ValueBinding(
        Func<T> get,                    // 값 읽기
        Action<T>? set,                 // 값 쓰기 (null이면 단방향)
        Action<Action>? subscribe,      // 변경 구독
        Action<Action>? unsubscribe,    // 구독 해제
        Action onSourceChanged          // 소스 변경 시 콜백
    );

    public T Get();
    public void Set(T value);
    public void Dispose();
}
```

### 커스텀 컨트롤에서 바인딩 구현

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

## 메모리 관리

### 자동 정리

컨트롤이 Dispose될 때 바인딩도 자동으로 정리됩니다:

```csharp
var vm = new ViewModel();
var textBox = new TextBox().BindText(vm.Name);

// Window가 닫힐 때 자동으로 바인딩 해제
// (Changed 이벤트에서 구독 해제)
```

### 수동 정리

필요한 경우 수동으로 구독을 해제할 수 있습니다:

```csharp
var counter = new ObservableValue<int>(0);

void OnChanged() => Console.WriteLine(counter.Value);

counter.Subscribe(OnChanged);

// 나중에 해제
counter.Unsubscribe(OnChanged);
```

---

## 모범 사례

### 1. ViewModel에서 ObservableValue 사용

```csharp
// Good
class ViewModel
{
    public ObservableValue<string> Name { get; } = new("");
}

// Bad - 일반 속성은 바인딩 불가
class ViewModel
{
    public string Name { get; set; }
}
```

### 2. Coerce로 유효성 보장

```csharp
// Good - 항상 유효한 값
var age = new ObservableValue<int>(0, v => Math.Clamp(v, 0, 150));

// Bad - 잘못된 값이 들어갈 수 있음
var age = new ObservableValue<int>(0);
```

### 3. 변환 바인딩으로 표시 로직 분리

```csharp
// Good - 표시 로직이 UI 레이어에
new Label().BindText(vm.Price, p => $"₩{p:N0}")

// Bad - ViewModel에 표시 로직이 섞임
class ViewModel
{
    public ObservableValue<string> FormattedPrice { get; }
}
```

### 4. 단방향 vs 양방향 바인딩 구분

```csharp
// 단방향: Label, ProgressBar (표시만)
new Label().BindText(vm.Status)
new ProgressBar().BindValue(vm.Progress)

// 양방향: TextBox, CheckBox, Slider (입력)
new TextBox().BindText(vm.Name)
new CheckBox().BindIsChecked(vm.IsEnabled)
new Slider().BindValue(vm.Volume)
```
