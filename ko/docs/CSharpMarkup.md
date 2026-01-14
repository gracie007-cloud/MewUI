# C# Markup Guide

MewUI의 C# Markup은 XAML 없이 순수 C# 코드로 UI를 선언적으로 구성할 수 있는 Fluent API입니다.
Native AOT 컴파일과 호환되며, Reflection을 사용하지 않습니다.

## 컨셉

### 왜 C# Markup인가?

- **Native AOT 호환**: Reflection 없이 컴파일 타임에 모든 것이 결정됨
- **타입 안전성**: 컴파일러가 오류를 잡아줌
- **IntelliSense**: IDE 자동완성 지원
- **코드 재사용**: 일반 C# 메서드로 UI 컴포넌트 추출 가능

### 기본 패턴

```csharp
new Button()
    .Content("Click Me")
    .Width(100)
    .OnClick(() => Console.WriteLine("Clicked!"))
```

모든 확장 메서드는 `this`를 반환하여 메서드 체이닝이 가능합니다.

## 네이밍 정책

### 속성 설정
| 패턴 | 설명 | 예시 |
|------|------|------|
| `PropertyName(value)` | 속성 직접 설정 | `.Width(100)`, `.Text("Hello")` |
| `PropertyName()` | bool 속성을 true로 설정 | `.Bold()`, `.IsChecked()` |

### 이벤트 핸들러
| 패턴 | 설명 | 예시 |
|------|------|------|
| `OnEventName(handler)` | 이벤트 핸들러 등록 | `.OnClick(...)`, `.OnTextChanged(...)` |
| `OnCanEventName(func)` | 조건부 실행 (Commanding) | `.OnCanClick(() => isValid)` |

### 데이터 바인딩
| 패턴 | 설명 | 예시 |
|------|------|------|
| `BindPropertyName(source)` | ObservableValue 바인딩 | `.BindText(vm.Name)` |
| `BindPropertyName(source, converter)` | 변환 바인딩 | `.BindText(vm.Count, c => $"{c}개")` |

### 단축 메서드
자주 사용되는 속성은 간결한 단축 메서드 제공:
- `.Bold()` → `.FontWeight(FontWeight.Bold)`
- `.Horizontal()` → `.Orientation(Orientation.Horizontal)`
- `.Center()` → `.HorizontalAlignment(Center).VerticalAlignment(Center)`

---

## 공통 확장 메서드

### FluentExtensions (모든 참조 타입)

| 메서드 | 설명 |
|--------|------|
| `Ref(out T field)` | 변수에 참조 저장 |

```csharp
new TextBox()
    .Ref(out var nameBox)  // nameBox 변수에 참조 저장
    .Text("Hello")
```

---

## Element 확장 메서드

모든 UI 요소의 기본 클래스입니다.

### DockPanel Attached Properties

| 메서드 | 설명 |
|--------|------|
| `DockTo(Dock dock)` | Dock 위치 설정 |
| `DockLeft()` | 왼쪽 도킹 |
| `DockTop()` | 상단 도킹 |
| `DockRight()` | 오른쪽 도킹 |
| `DockBottom()` | 하단 도킹 |

### Grid Attached Properties

| 메서드 | 설명 |
|--------|------|
| `Row(int row)` | Grid 행 위치 |
| `Column(int column)` | Grid 열 위치 |
| `RowSpan(int rowSpan)` | 행 스팬 |
| `ColumnSpan(int columnSpan)` | 열 스팬 |
| `GridPosition(row, column)` | 행/열 동시 설정 |
| `GridPosition(row, column, rowSpan, columnSpan)` | 전체 위치 설정 |

### Canvas Attached Properties

| 메서드 | 설명 |
|--------|------|
| `CanvasLeft(double left)` | 왼쪽 오프셋 |
| `CanvasTop(double top)` | 상단 오프셋 |
| `CanvasRight(double right)` | 오른쪽 오프셋 |
| `CanvasBottom(double bottom)` | 하단 오프셋 |
| `CanvasPosition(left, top)` | 위치 설정 |

---

## FrameworkElement 확장 메서드

레이아웃 가능한 모든 요소의 기본 클래스입니다.

### 크기

| 메서드 | 설명 |
|--------|------|
| `Width(double)` | 너비 |
| `Height(double)` | 높이 |
| `Size(width, height)` | 너비/높이 동시 설정 |
| `Size(double)` | 정사각형 크기 |
| `MinWidth(double)` | 최소 너비 |
| `MinHeight(double)` | 최소 높이 |
| `MaxWidth(double)` | 최대 너비 |
| `MaxHeight(double)` | 최대 높이 |

### 여백

| 메서드 | 설명 |
|--------|------|
| `Margin(uniform)` | 균일 여백 |
| `Margin(horizontal, vertical)` | 수평/수직 여백 |
| `Margin(left, top, right, bottom)` | 개별 여백 |
| `Padding(uniform)` | 균일 패딩 |
| `Padding(horizontal, vertical)` | 수평/수직 패딩 |
| `Padding(left, top, right, bottom)` | 개별 패딩 |

### 정렬

| 메서드 | 설명 |
|--------|------|
| `HorizontalAlignment(alignment)` | 수평 정렬 |
| `VerticalAlignment(alignment)` | 수직 정렬 |
| `Center()` | 중앙 정렬 (수평+수직) |
| `CenterHorizontal()` | 수평 중앙 |
| `CenterVertical()` | 수직 중앙 |
| `Left()` | 왼쪽 정렬 |
| `Right()` | 오른쪽 정렬 |
| `Top()` | 상단 정렬 |
| `Bottom()` | 하단 정렬 |
| `StretchHorizontal()` | 수평 늘이기 |
| `StretchVertical()` | 수직 늘이기 |

---

## UIElement 확장 메서드

입력 이벤트를 처리하는 모든 요소의 기본 클래스입니다.

### 바인딩

| 메서드 | 설명 |
|--------|------|
| `BindIsVisible(ObservableValue<bool>)` | 가시성 바인딩 |
| `BindIsEnabled(ObservableValue<bool>)` | 활성화 바인딩 |

### 포커스 이벤트

| 메서드 | 설명 |
|--------|------|
| `OnGotFocus(Action)` | 포커스 획득 |
| `OnLostFocus(Action)` | 포커스 손실 |

### 마우스 이벤트

| 메서드 | 설명 |
|--------|------|
| `OnMouseEnter(Action)` | 마우스 진입 |
| `OnMouseLeave(Action)` | 마우스 이탈 |
| `OnMouseDown(Action<MouseEventArgs>)` | 마우스 버튼 누름 |
| `OnMouseUp(Action<MouseEventArgs>)` | 마우스 버튼 뗌 |
| `OnMouseMove(Action<MouseEventArgs>)` | 마우스 이동 |
| `OnMouseWheel(Action<MouseWheelEventArgs>)` | 마우스 휠 |

### 키보드 이벤트

| 메서드 | 설명 |
|--------|------|
| `OnKeyDown(Action<KeyEventArgs>)` | 키 누름 |
| `OnKeyUp(Action<KeyEventArgs>)` | 키 뗌 |
| `OnTextInput(Action<TextInputEventArgs>)` | 텍스트 입력 |

---

## Control 확장 메서드

시각적 스타일을 가진 모든 컨트롤의 기본 클래스입니다.

### 색상

| 메서드 | 설명 |
|--------|------|
| `Background(Color)` | 배경색 |
| `Foreground(Color)` | 전경색 (텍스트) |
| `BorderBrush(Color)` | 테두리 색상 |
| `BorderThickness(double)` | 테두리 두께 |

### 폰트

| 메서드 | 설명 |
|--------|------|
| `FontFamily(string)` | 폰트 이름 |
| `FontSize(double)` | 폰트 크기 |
| `FontWeight(FontWeight)` | 폰트 굵기 |
| `Bold()` | 굵게 (단축) |

---

## 개별 컨트롤 확장 메서드

### Window

```csharp
new Window()
    .Title("My App")
    .Resizable(800, 600)
    .Content(...)
    .OnLoaded(() => ...)
    .OnClosed(() => ...)
```

| 메서드 | 설명 |
|--------|------|
| `Title(string)` | 창 제목 |
| `Width(double)` | 창 너비 |
| `Height(double)` | 창 높이 |
| `Size(width, height)` | 창 크기 |
| `Resizable(width, height)` | 크기 조절 가능 |
| `Fixed(width, height)` | 고정 크기 |
| `FitContentWidth(maxWidth, fixedHeight)` | 콘텐츠에 맞춤 (너비) |
| `FitContentHeight(fixedWidth, maxHeight)` | 콘텐츠에 맞춤 (높이) |
| `FitContentSize(maxWidth, maxHeight)` | 콘텐츠에 맞춤 |
| `Content(Element)` | 창 내용 |
| `OnLoaded(Action)` | 로드 완료 |
| `OnClosed(Action)` | 창 닫힘 |
| `OnActivated(Action)` | 창 활성화 |
| `OnDeactivated(Action)` | 창 비활성화 |
| `OnSizeChanged(Action<Size>)` | 크기 변경 |
| `OnDpiChanged(Action<uint, uint>)` | DPI 변경 |
| `OnThemeChanged(Action<Theme, Theme>)` | 테마 변경 |
| `OnFirstFrameRendered(Action)` | 첫 프레임 렌더링 |
| `OnPreviewKeyDown(Action<KeyEventArgs>)` | 키 누름 (미리보기) |
| `OnPreviewKeyUp(Action<KeyEventArgs>)` | 키 뗌 (미리보기) |
| `OnPreviewTextInput(Action<TextInputEventArgs>)` | 텍스트 입력 (미리보기) |

### Label

```csharp
new Label()
    .Text("Hello World")
    .Bold()
    .FontSize(16)
```

| 메서드 | 설명 |
|--------|------|
| `Text(string)` | 텍스트 내용 |
| `TextAlignment(TextAlignment)` | 수평 텍스트 정렬 |
| `VerticalTextAlignment(TextAlignment)` | 수직 텍스트 정렬 |
| `TextWrapping(TextWrapping)` | 텍스트 줄바꿈 |
| `BindText(ObservableValue<string>)` | 텍스트 바인딩 |
| `BindText(source, converter)` | 변환 바인딩 |

### Button

```csharp
new Button()
    .Content("Click Me")
    .OnCanClick(() => isFormValid)
    .OnClick(() => Submit())
```

| 메서드 | 설명 |
|--------|------|
| `Content(string)` | 버튼 텍스트 |
| `OnClick(Action)` | 클릭 핸들러 |
| `OnCanClick(Func<bool>)` | 클릭 가능 조건 (Commanding) |
| `BindContent(ObservableValue<string>)` | 콘텐츠 바인딩 |

### TextBox

```csharp
new TextBox()
    .Placeholder("Enter name...")
    .BindText(vm.Name)
```

| 메서드 | 설명 |
|--------|------|
| `Text(string)` | 텍스트 내용 |
| `Placeholder(string)` | 플레이스홀더 |
| `IsReadOnly(bool)` | 읽기 전용 |
| `AcceptTab(bool)` | 탭 키 허용 |
| `OnTextChanged(Action<string>)` | 텍스트 변경 핸들러 |
| `BindText(ObservableValue<string>)` | 텍스트 바인딩 (양방향) |

### MultiLineTextBox

```csharp
new MultiLineTextBox()
    .Placeholder("Enter notes...")
    .Wrap(true)
    .Height(100)
```

| 메서드 | 설명 |
|--------|------|
| `Text(string)` | 텍스트 내용 |
| `Placeholder(string)` | 플레이스홀더 |
| `IsReadOnly(bool)` | 읽기 전용 |
| `AcceptTab(bool)` | 탭 키 허용 |
| `Wrap(bool)` | 줄바꿈 |
| `OnWrapChanged(Action<bool>)` | 줄바꿈 변경 핸들러 |
| `OnTextChanged(Action<string>)` | 텍스트 변경 핸들러 |
| `BindText(ObservableValue<string>)` | 텍스트 바인딩 |

### CheckBox

```csharp
new CheckBox()
    .Text("Enable feature")
    .BindIsChecked(vm.IsEnabled)
```

| 메서드 | 설명 |
|--------|------|
| `Text(string)` | 레이블 텍스트 |
| `IsChecked(bool)` | 체크 상태 |
| `OnCheckedChanged(Action<bool>)` | 체크 변경 핸들러 |
| `BindIsChecked(ObservableValue<bool>)` | 체크 바인딩 |
| `BindWrap(MultiLineTextBox)` | Wrap 속성 연동 |

### RadioButton

```csharp
new RadioButton()
    .Text("Option A")
    .GroupName("options")
    .IsChecked(true)
```

| 메서드 | 설명 |
|--------|------|
| `Text(string)` | 레이블 텍스트 |
| `GroupName(string?)` | 그룹 이름 (같은 그룹 내 하나만 선택) |
| `IsChecked(bool)` | 선택 상태 |
| `OnCheckedChanged(Action<bool>)` | 선택 변경 핸들러 |
| `BindIsChecked(ObservableValue<bool>)` | 선택 바인딩 |

### ToggleSwitch

```csharp
new ToggleSwitch()
    .Text("Dark Mode")
    .BindIsChecked(vm.IsDarkMode)
```

| 메서드 | 설명 |
|--------|------|
| `Text(string)` | 레이블 텍스트 |
| `IsChecked(bool)` | 토글 상태 |
| `OnCheckedChanged(Action<bool>)` | 토글 변경 핸들러 |
| `BindIsChecked(ObservableValue<bool>)` | 토글 바인딩 |

### ListBox

```csharp
new ListBox()
    .Items("Apple", "Banana", "Cherry")
    .SelectedIndex(0)
    .Height(120)
```

| 메서드 | 설명 |
|--------|------|
| `Items(params string[])` | 아이템 목록 |
| `ItemHeight(double)` | 아이템 높이 |
| `ItemPadding(Thickness)` | 아이템 패딩 |
| `SelectedIndex(int)` | 선택 인덱스 |
| `OnSelectionChanged(Action<int>)` | 선택 변경 핸들러 |
| `BindSelectedIndex(ObservableValue<int>)` | 선택 바인딩 |

### ComboBox

```csharp
new ComboBox()
    .Items("Small", "Medium", "Large")
    .Placeholder("Select size...")
    .SelectedIndex(1)
```

| 메서드 | 설명 |
|--------|------|
| `Items(params string[])` | 아이템 목록 |
| `SelectedIndex(int)` | 선택 인덱스 |
| `Placeholder(string)` | 플레이스홀더 |
| `OnSelectionChanged(Action<int>)` | 선택 변경 핸들러 |
| `BindSelectedIndex(ObservableValue<int>)` | 선택 바인딩 |

### Slider

```csharp
new Slider()
    .Minimum(0)
    .Maximum(100)
    .BindValue(vm.Volume)
```

| 메서드 | 설명 |
|--------|------|
| `Minimum(double)` | 최소값 |
| `Maximum(double)` | 최대값 |
| `Value(double)` | 현재값 |
| `SmallChange(double)` | 작은 변경 단위 |
| `OnValueChanged(Action<double>)` | 값 변경 핸들러 |
| `BindValue(ObservableValue<double>)` | 값 바인딩 |

### ProgressBar

```csharp
new ProgressBar()
    .Minimum(0)
    .Maximum(100)
    .BindValue(vm.Progress)
```

| 메서드 | 설명 |
|--------|------|
| `Minimum(double)` | 최소값 |
| `Maximum(double)` | 최대값 |
| `Value(double)` | 현재값 |
| `BindValue(ObservableValue<double>)` | 값 바인딩 |

### Image

```csharp
new Image()
    .SourceFile("logo.png")
    .Size(64, 64)
    .StretchMode(ImageStretch.Uniform)
```

| 메서드 | 설명 |
|--------|------|
| `Source(ImageSource?)` | 이미지 소스 |
| `SourceFile(string path)` | 파일에서 로드 |
| `SourceResource(Assembly, string)` | 리소스에서 로드 |
| `SourceResource<TAnchor>(string)` | 리소스에서 로드 (제네릭) |
| `StretchMode(ImageStretch)` | 늘이기 모드 |

### TabControl

```csharp
new TabControl()
    .TabItems(
        new TabItem().Header("Home").Content(...),
        new TabItem().Header("Settings").Content(...)
    )
```

| 메서드 | 설명 |
|--------|------|
| `TabItems(params TabItem[])` | 탭 아이템 목록 |
| `Tab(header, content)` | 탭 추가 (문자열 헤더) |
| `Tab(Element header, content)` | 탭 추가 (요소 헤더) |
| `SelectedIndex(int)` | 선택 탭 인덱스 |
| `OnSelectionChanged(Action<int>)` | 탭 변경 핸들러 |
| `VerticalScroll(ScrollMode)` | 수직 스크롤 |
| `HorizontalScroll(ScrollMode)` | 수평 스크롤 |
| `AutoVerticalScroll()` | 자동 수직 스크롤 |
| `AutoHorizontalScroll()` | 자동 수평 스크롤 |

### TabItem

```csharp
new TabItem()
    .Header("Settings")
    .Content(new StackPanel().Children(...))
    .IsEnabled(true)
```

| 메서드 | 설명 |
|--------|------|
| `Header(string)` | 헤더 텍스트 |
| `Header(Element)` | 헤더 요소 |
| `Content(Element)` | 탭 내용 |
| `IsEnabled(bool)` | 활성화 상태 |

### GroupBox (HeaderedContentControl)

```csharp
new GroupBox()
    .Header("Options")
    .Content(new StackPanel().Children(...))
```

| 메서드 | 설명 |
|--------|------|
| `Header(string)` | 헤더 텍스트 (Bold 스타일) |
| `Header(Element)` | 헤더 요소 |
| `HeaderSpacing(double)` | 헤더-콘텐츠 간격 |
| `Content(Element)` | 그룹 내용 |

### ScrollViewer

```csharp
new ScrollViewer()
    .AutoVerticalScroll()
    .NoHorizontalScroll()
    .Content(...)
```

| 메서드 | 설명 |
|--------|------|
| `VerticalScroll(ScrollMode)` | 수직 스크롤 모드 |
| `HorizontalScroll(ScrollMode)` | 수평 스크롤 모드 |
| `Scroll(vertical, horizontal)` | 스크롤 모드 동시 설정 |
| `NoVerticalScroll()` | 수직 스크롤 비활성화 |
| `AutoVerticalScroll()` | 자동 수직 스크롤 |
| `ShowVerticalScroll()` | 수직 스크롤 항상 표시 |
| `NoHorizontalScroll()` | 수평 스크롤 비활성화 |
| `AutoHorizontalScroll()` | 자동 수평 스크롤 |
| `ShowHorizontalScroll()` | 수평 스크롤 항상 표시 |
| `Content(Element)` | 스크롤할 내용 |

---

## Panel 확장 메서드

### Panel (공통)

| 메서드 | 설명 |
|--------|------|
| `Children(params Element[])` | 자식 요소 추가 |

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

| 메서드 | 설명 |
|--------|------|
| `Orientation(Orientation)` | 방향 |
| `Horizontal()` | 수평 방향 (단축) |
| `Vertical()` | 수직 방향 (단축) |
| `Spacing(double)` | 요소 간 간격 |

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

| 메서드 | 설명 |
|--------|------|
| `Rows(params GridLength[])` | 행 정의 |
| `Rows(string)` | 행 정의 (문자열: "Auto,*,2*,100") |
| `Columns(string)` | 열 정의 (문자열) |
| `Spacing(double)` | 셀 간 간격 |
| `AutoIndexing(bool)` | 자동 인덱싱 (Row/Column 자동 증가) |

**GridLength 문자열 문법:**
- `Auto` - 내용에 맞춤
- `*` - 1 비율
- `2*` - 2 비율
- `100` - 100 픽셀

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

| 메서드 | 설명 |
|--------|------|
| `Rows(int)` | 행 개수 |
| `Columns(int)` | 열 개수 |
| `Spacing(double)` | 셀 간 간격 |

### WrapPanel

```csharp
new WrapPanel()
    .Orientation(Orientation.Horizontal)
    .Spacing(8)
    .ItemWidth(100)
    .ItemHeight(100)
    .Children(...)
```

| 메서드 | 설명 |
|--------|------|
| `Orientation(Orientation)` | 방향 |
| `Spacing(double)` | 요소 간 간격 |
| `ItemWidth(double)` | 아이템 너비 |
| `ItemHeight(double)` | 아이템 높이 |

### DockPanel

```csharp
new DockPanel()
    .LastChildFill()
    .Spacing(8)
    .Children(
        new Label().Text("Header").DockTop(),
        new Label().Text("Footer").DockBottom(),
        new Label().Text("Content")  // 남은 공간 채움
    )
```

| 메서드 | 설명 |
|--------|------|
| `LastChildFill(bool)` | 마지막 자식이 남은 공간 채움 |
| `Spacing(double)` | 요소 간 간격 |

---

## Commanding (CanExecute 패턴)

Button의 `OnCanClick`을 사용하여 WPF ICommand와 유사한 패턴을 구현할 수 있습니다.

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

### 자동 재평가 시점

`CanClick`은 다음 시점에 자동으로 재평가됩니다:
- **Focus 변경** - 포커스가 이동할 때
- **MouseUp** - 마우스 버튼을 뗄 때
- **KeyUp** - 키를 뗄 때

### 수동 재평가

상태가 변경된 후 수동으로 재평가가 필요한 경우:

```csharp
// 이벤트 핸들러 내에서 상태 변경 후
counter.Value++;
window.RequerySuggested();  // CanClick 재평가 트리거
```

---

## Apply 패턴

복잡한 초기화나 지원되지 않는 속성 설정 시 `Apply` 패턴을 사용합니다:

```csharp
public static T Apply<T>(this T obj, Action<T> action)
{
    action(obj);
    return obj;
}

// 사용 예
new TextBox()
    .OnTextChanged(text => Console.WriteLine(text))
    .Apply(tb => tb.MaxLength = 100)
```
