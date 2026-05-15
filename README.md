# MarkFlow

> C# WPF로 만든 마크다운 에디터 + 파일 병합 도구

아이디어를 기획할 때 항상 이런 불편함이 있었습니다.

- 생각을 정리하다 보면 지우고 쓰고를 반복하게 됨
- Ctrl+Z로 이전 내용을 찾아도 앞서 작성했던 내용이 이미 사라져 있음
- 결국 다시 쓰고 복붙하는 과정을 반복해야 함

이 불편함을 해결하기 위해 **MarkFlow**를 만들었습니다.
마크다운으로 아이디어 조각들을 각각 파일로 작성해두고, 나중에 원하는 순서대로 연결해 하나의 흐름으로 합칠 수 있는 앱입니다.
아이디어의 순서를 자유롭게 바꾸고 병합하면서 브레인스토밍을 도울 수 있도록 설계했습니다.

C# 수업 과제로 처음 WPF를 배우며 제작했습니다.

---

## 목차

1. [기술 스택](#기술-스택)
2. [MVVM 패턴이란?](#mvvm-패턴이란)
3. [프로젝트 구조](#프로젝트-구조)
4. [주요 기능](#주요-기능)
5. [기능별 구현 설명](#기능별-구현-설명)
6. [사용한 라이브러리](#사용한-라이브러리)

---

## 기술 스택

| 항목 | 내용 |
|---|---|
| 언어 | C# (.NET 8) |
| UI 프레임워크 | WPF (Windows Presentation Foundation) |
| 에디터 컴포넌트 | AvalonEdit |
| 데이터 직렬화 | System.Text.Json |
| 아키텍처 패턴 | MVVM |

---

## MVVM 패턴이란?

MVVM은 **Model - View - ViewModel** 의 약자로, UI와 비즈니스 로직을 분리하기 위한 설계 패턴입니다.

```
사용자 입력
    ↓
View (화면, XAML)
    ↕  데이터 바인딩
ViewModel (로직, C#)
    ↕
Model (데이터)
```

### 각 역할

**Model** — 순수한 데이터만 담당합니다.

```csharp
// Models/Document.cs
public class Document
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "Untitled";
    public string MarkdownContent { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

**ViewModel** — 화면에 보여줄 데이터를 가공하고 사용자 동작을 처리합니다.
`INotifyPropertyChanged` 인터페이스를 구현해서, 값이 바뀌면 화면이 자동으로 업데이트됩니다.

```csharp
// ViewModels/EditorViewModel.cs
public class EditorViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    // 값이 바뀔 때 화면에 알림을 보내는 헬퍼 메서드
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string _markdownContent = "";
    public string MarkdownContent
    {
        get => _markdownContent;
        set
        {
            _markdownContent = value;
            OnPropertyChanged(); // 화면에 "값이 바뀌었다" 알림
            UpdatePreview();     // 미리보기 자동 갱신
        }
    }
}
```

**View** — XAML로 화면을 그리고, ViewModel과 데이터 바인딩으로 연결합니다.
AvalonEdit은 일반 WPF 바인딩이 안 되서, `MainWindow.xaml.cs`에서 직접 이벤트로 연결했습니다.

```csharp
// Views/MainWindow.xaml.cs
EditorView.TextChanged += text =>
{
    _viewModel.MarkdownContent = text; // 에디터 → ViewModel
};

_viewModel.PropertyChanged += (s, e) =>
{
    if (e.PropertyName == nameof(_viewModel.PreviewDocument))
        EditorView.Preview = _viewModel.PreviewDocument; // ViewModel → 미리보기
};
```

### 왜 MVVM을 썼나요?

React의 컴포넌트 단위 개발 방식처럼, WPF에서도 화면을 기능별로 분리해서 관리하고 싶었습니다.
`EditorView`, `MergeView`, `BrainstormView`를 각각 독립적인 UserControl로 만들고, `MainWindow`에서는 탭 전환만 담당하도록 역할을 나눴습니다.

이렇게 하면 예를 들어 병합 기능을 수정할 때 `MergeView`만 건드리면 되고, 마크다운 파싱 로직을 바꿔도 화면 코드에 영향이 없어서 유지보수가 훨씬 편합니다.

---

## 프로젝트 구조

```
MarkFlow/
├── Models/
│   └── Document.cs              # 문서 데이터 모델
├── ViewModels/
│   └── EditorViewModel.cs       # 마크다운 파싱, 저장/열기 로직
├── Views/
│   ├── MainWindow.xaml/.cs      # 메인 창, 탭 전환, 사이드바
│   ├── EditorView.xaml/.cs      # 마크다운 에디터 + 미리보기 (UserControl)
│   ├── MergeView.xaml/.cs       # 파일 병합 캔버스 (UserControl)
│   └── BrainstormView.xaml/.cs  # 브레인스토밍 캔버스 (UserControl)
├── Highlighting/
│   ├── JavaScript.xshd          # JS 커스텀 하이라이팅 정의
│   └── CSharp.xshd              # C# 커스텀 하이라이팅 정의
└── App.xaml/.cs                 # 앱 진입점
```

### UserControl이란?

WPF에서 화면을 재사용 가능한 컴포넌트로 분리하는 방법입니다.
`EditorView`, `MergeView`, `BrainstormView`를 각각 UserControl로 만들어서 `MainWindow`에서 탭처럼 전환합니다.

```csharp
// 탭 전환 로직 (MainWindow.xaml.cs)
private void SetTab(string tab)
{
    EditorView.Visibility    = tab == "markdown"    ? Visibility.Visible : Visibility.Collapsed;
    MergeView.Visibility     = tab == "merge"       ? Visibility.Visible : Visibility.Collapsed;
    BrainstormView.Visibility = tab == "brainstorm" ? Visibility.Visible : Visibility.Collapsed;
}
```

---

## 주요 기능

### 마크다운 탭
- 실시간 마크다운 편집 + 미리보기 (좌우 분할)
- 지원 문법: 제목(H1~H3), 굵음, 이탤릭, 리스트, 표, 코드블록, 인용구, 링크, 수평선
- 코드블록 문법 하이라이팅 (JavaScript, C#, Python 등)
- 파일 열기 / 저장 / 새 문서
- 폴더 열기로 사이드바에 파일 목록 표시
- 파일 우클릭 삭제

### 파일 병합 탭
- 폴더 내 md 파일들을 노드로 시각화
- 클릭 순서대로 번호(#1, #2, #3...) 표시 + 화살표 연결
- 병합 버튼으로 순서대로 합친 md 파일 생성
- 병합 이력을 `.brainstorm` 파일에 저장
- Ctrl + 마우스 휠로 캔버스 확대/축소

### 브레인스토밍 탭
- 폴더 내 md 파일들을 노드로 시각화
- 노드 드래그로 자유 배치
- 노드 클릭으로 화살표 연결 / 재클릭으로 연결 삭제
- 연결 정보를 `.brainstorm_layout` 파일에 저장 (다음에 열면 복원)
- 노드에 파일의 H1 제목 미리보기

---

## 기능별 구현 설명

### 1. 마크다운 실시간 파싱

라이브러리 없이 직접 파싱하는 방식을 선택했습니다.
처음에는 Markdig 라이브러리를 사용했지만, `System.Windows.Documents`와 네임스페이스 충돌 문제가 있어서 직접 구현했습니다.

```csharp
// ViewModels/EditorViewModel.cs - UpdatePreview() 핵심 부분
private void UpdatePreview()
{
    var doc = new FlowDocument();
    var lines = _markdownContent.Split('\n');
    int i = 0;

    while (i < lines.Length)
    {
        var trimmed = lines[i].TrimEnd();

        // H1 처리
        if (trimmed.StartsWith("# "))
        {
            var p = new Paragraph();
            p.FontSize = 28;
            p.FontWeight = FontWeights.Bold;
            ParseInline(p, trimmed.Substring(2)); // 인라인 요소(굵음, 이탤릭 등) 파싱
            doc.Blocks.Add(p);
        }
        // 표 처리: 다음 줄이 구분선(|---|)인지 확인
        else if (trimmed.Contains("|") &&
            i + 1 < lines.Length &&
            Regex.IsMatch(lines[i + 1].Trim(), @"^[\|\-\s:]+$"))
        {
            // 표 생성 로직...
        }
        i++;
    }

    PreviewDocument = doc; // OnPropertyChanged 트리거 → 화면 자동 갱신
}
```

인라인 요소(굵음 `**`, 이탤릭 `*`, 코드 `` ` ``, 링크)는 문자 하나씩 읽으면서 파싱합니다.

```csharp
// 굵음 ** 파싱 예시
if (i + 1 < text.Length && text[i] == '*' && text[i + 1] == '*')
{
    int end = text.IndexOf("**", i + 2); // 닫는 ** 위치 찾기
    if (end > 0)
    {
        p.Inlines.Add(new Bold(new Run(text.Substring(i + 2, end - i - 2))));
        i = end + 2;
    }
}
```

---

### 2. 코드 하이라이팅

AvalonEdit이 지원하는 `.xshd` XML 형식으로 커스텀 하이라이팅 규칙을 정의했습니다.

```xml
<!-- Highlighting/JavaScript.xshd -->
<SyntaxDefinition name="JavaScript-Custom">
    <Color name="Keyword" foreground="#0000FF" fontWeight="bold"/>
    <Color name="String"  foreground="#A31515"/>
    <Color name="Comment" foreground="#008000" fontStyle="italic"/>

    <RuleSet>
        <Span color="Comment" begin="//" end="\n"/>
        <Span color="String"  begin="&quot;" end="&quot;"/>
        <Keywords color="Keyword">
            <Word>const</Word>
            <Word>let</Word>
            <Word>function</Word>
            <!-- ... -->
        </Keywords>
    </RuleSet>
</SyntaxDefinition>
```

앱 시작 시 이 파일을 읽어서 AvalonEdit에 등록합니다.

```csharp
// ViewModels/EditorViewModel.cs
private void RegisterCustomHighlighting()
{
    var jsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Highlighting", "JavaScript.xshd");
    if (File.Exists(jsPath))
    {
        using var reader = new XmlTextReader(jsPath);
        var definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        HighlightingManager.Instance.RegisterHighlighting("JavaScript-Custom", new[] { ".js" }, definition);
    }
}
```

미리보기의 코드블록은 AvalonEdit `TextEditor`를 `BlockUIContainer`로 감싸서 `FlowDocument` 안에 삽입했습니다.

```csharp
var editor = new ICSharpCode.AvalonEdit.TextEditor();
editor.Text = code;
editor.IsReadOnly = true;
editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("JavaScript-Custom");

var container = new BlockUIContainer(editor); // FlowDocument에 WPF 컨트롤 삽입
doc.Blocks.Add(container);
```

---

### 3. 드래그와 클릭 구분

노드를 드래그로 이동할 때 실수로 클릭(연결)이 되는 문제를 해결했습니다.
마우스를 5px 이상 움직였을 때만 드래그로 판정합니다.

```csharp
private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    _draggingNode = node;
    _hasDragged = false;
    _dragStartPos = e.GetPosition(canvas); // 시작 위치 기록
}

private void Node_MouseMove(object sender, MouseEventArgs e)
{
    var pos = e.GetPosition(canvas);
    if (!_hasDragged)
    {
        // 시작 위치와 현재 위치의 거리 계산
        var dist = Math.Sqrt(Math.Pow(pos.X - _dragStartPos.X, 2) + Math.Pow(pos.Y - _dragStartPos.Y, 2));
        if (dist < 5) return; // 5px 미만이면 드래그 아님
        _hasDragged = true;
    }
    // 노드 위치 업데이트...
}

private void Node_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
{
    if (!_hasDragged)
        HandleNodeClick(node); // 드래그 안 했을 때만 클릭 처리
}
```

---

### 4. 화살표 방향 계산

두 노드를 연결하는 화살표를 그릴 때, 선이 노드 중심이 아닌 테두리에서 시작/끝나도록 수학적으로 계산합니다.

```csharp
// 노드 중심에서 목표 방향으로 테두리까지의 교차점 계산
private Point GetBorderPoint(FileNode node, double targetX, double targetY)
{
    double cx = node.X + 90; // 노드 중심 X
    double cy = node.Y + 40; // 노드 중심 Y
    double dx = targetX - cx; // 목표까지의 방향 벡터
    double dy = targetY - cy;

    // X방향과 Y방향 중 먼저 테두리에 닿는 쪽을 선택
    double scaleX = dx != 0 ? 90.0 / Math.Abs(dx) : double.MaxValue;
    double scaleY = dy != 0 ? 40.0 / Math.Abs(dy) : double.MaxValue;
    double scale = Math.Min(scaleX, scaleY);

    return new Point(cx + dx * scale, cy + dy * scale);
}
```

화살표 머리는 삼각형(`Polygon`)으로 그립니다. `Math.Atan2`로 선의 각도를 구하고, 그 각도에 맞춰 삼각형 꼭짓점을 계산합니다.

```csharp
double angle = Math.Atan2(to.Y - from.Y, to.X - from.X); // 선의 기울기 각도
double size = 10;

conn.Arrow.Points = new PointCollection
{
    new Point(to.X, to.Y), // 화살표 끝 (꼭짓점)
    new Point(to.X - Math.Cos(angle) * size * 1.5 - Math.Sin(angle) * size * 0.6,
              to.Y - Math.Sin(angle) * size * 1.5 + Math.Cos(angle) * size * 0.6),
    new Point(to.X - Math.Cos(angle) * size * 1.5 + Math.Sin(angle) * size * 0.6,
              to.Y - Math.Sin(angle) * size * 1.5 - Math.Cos(angle) * size * 0.6)
};
```

---

### 5. 파일 병합 및 이력 저장

파일 병합 시 클릭 순서대로 md 파일 내용을 이어 붙이고, 병합 이력을 JSON 파일로 저장합니다.

```csharp
// 병합 실행
private void MergeExecute_Click(object sender, RoutedEventArgs e)
{
    // 클릭 순서대로 파일 내용을 --- 구분선으로 합치기
    var merged = string.Join("\n\n---\n\n",
        _orderedNodes.Select(n => File.ReadAllText(n.FilePath)));

    File.WriteAllText(dialog.FileName, merged, Encoding.UTF8);
    SaveHistory(resultFileName); // 이력 저장
}

// 이력을 .brainstorm 파일에 JSON으로 저장
private void SaveHistory(string resultFile)
{
    var entries = new List<HistoryEntry>();
    // 기존 이력 불러오기
    if (File.Exists(brainstormPath))
        entries = JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(brainstormPath));

    entries.Insert(0, new HistoryEntry
    {
        Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
        Sources = _orderedNodes.Select(n => n.FileName).ToList(),
        Result = resultFile
    });

    File.WriteAllText(brainstormPath, JsonSerializer.Serialize(entries));
}
```

---

### 6. 브레인스토밍 레이아웃 저장/복원

노드 위치와 연결 정보를 JSON으로 저장해서 앱을 다시 열었을 때 복원합니다.

```csharp
// 저장 데이터 구조
private class SaveData
{
    public List<NodeData> Nodes { get; set; } = new();
    public List<ConnectionData> Connections { get; set; } = new();
}

// 저장
private void SaveLayout()
{
    var data = new SaveData
    {
        Nodes = _nodes.Select(n => new NodeData
        {
            Id = n.Id,
            FilePath = n.FilePath,
            X = n.X, Y = n.Y
        }).ToList(),
        Connections = _connections.Select(c => new ConnectionData
        {
            FromId = c.FromId, ToId = c.ToId
        }).ToList()
    };
    File.WriteAllText(savePath, JsonSerializer.Serialize(data));
}

// 복원: 저장된 노드 불러오고, 새로 추가된 파일은 자동으로 추가
private void TryLoadSave()
{
    var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(savePath));

    foreach (var nd in data.Nodes)
    {
        if (!File.Exists(nd.FilePath)) continue; // 삭제된 파일은 건너뜀
        var node = new BrainstormNode { Id = nd.Id, X = nd.X, Y = nd.Y, ... };
        CreateNodeControl(node);
    }

    // 저장에 없는 새 파일 자동 추가
    var existingPaths = _nodes.Select(n => n.FilePath).ToHashSet();
    foreach (var file in Directory.GetFiles(_folderPath, "*.md"))
    {
        if (!existingPaths.Contains(file))
            // 새 노드 추가
    }
}
```

---

## 트러블슈팅 및 해결

### 마크다운 파싱 라이브러리(Markdig) 대신 직접 구현한 이유

처음에는 마크다운 파싱을 위해 Markdig 라이브러리를 사용했습니다. 그러나 세 가지 문제가 발생해서 직접 구현하는 방향으로 전환했습니다.

**문제 1. 네임스페이스 충돌**

Markdig을 설치하면 `Block`, `Inline` 같은 클래스 이름이 WPF의 `System.Windows.Documents`와 겹쳐서 컴파일 에러가 연속으로 발생했습니다.

```
'Block'은(는) 'System.Windows.Documents.Block' 및
'Markdig.Syntax.Block' 사이에 모호한 참조입니다.

'Inline'은(는) 'System.Windows.Documents.Inline' 및
'Markdig.Syntax.Inlines.Inline' 사이에 모호한 참조입니다.
```

`using` alias로 해결을 시도했지만 연쇄적으로 충돌이 계속 생겼습니다.

```csharp
// 해결 시도했지만 충돌이 계속 발생
using MdBlock = Markdig.Syntax.Block;
using WpfBlock = System.Windows.Documents.Block;
```

**문제 2. 줄바꿈 동작 차이**

Markdig은 마크다운 표준 스펙대로 엔터 한 번을 줄바꿈으로 처리하지 않습니다. `UseSoftlineBreakAsHardlineBreak()` 옵션을 추가했지만 해결되지 않았고, 직접 구현하면 엔터 한 번도 자연스럽게 줄바꿈으로 처리할 수 있었습니다.

```csharp
// 해결 시도했지만 동작 안 함
_pipeline = new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    .UseSoftlineBreakAsHardlineBreak() // 적용 안 됨
    .Build();
```

**문제 3. 코드 하이라이팅 색상 부족**

AvalonEdit 기본 하이라이팅은 파란색과 검정색 위주라 가독성이 좋지 않았습니다. 직접 `.xshd` 파일을 작성하면 키워드, 문자열, 주석, 숫자 등 요소별로 원하는 색상을 자유롭게 지정할 수 있었습니다.

```xml
<!-- 기본 하이라이팅 → 파란색/검정색만 존재 -->
<!-- 커스텀 .xshd로 VSCode 라이트 테마와 유사하게 구현 -->
<Color name="Keyword" foreground="#0000FF" fontWeight="bold"/> <!-- 파란색 -->
<Color name="String"  foreground="#A31515"/>                   <!-- 붉은색 -->
<Color name="Comment" foreground="#008000" fontStyle="italic"/> <!-- 초록색 -->
<Color name="Number"  foreground="#098658"/>                   <!-- 청록색 -->
```


