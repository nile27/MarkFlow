# MarkFlow 발표 자료

---

## 1. 프로젝트 소개

### 왜 만들었나?

아이디어를 기획할 때 항상 이런 불편함이 있었습니다.

- 생각을 정리하다 보면 **지우고 쓰고를 반복**하게 됨
- Ctrl+Z로 이전 내용을 찾아도 **앞서 작성했던 내용이 이미 사라져 있음**
- 결국 다시 쓰고 복붙하는 과정을 반복해야 함

> 아이디어 조각들을 각각 파일로 작성해두고,
> 원하는 순서대로 연결해 하나의 흐름으로 합칠 수 있는 앱이 있으면 어떨까?

---

## 2. MarkFlow가 뭔가요?

**Windows 데스크탑 마크다운 에디터 + 파일 병합 도구**

| 탭 | 기능 |
|---|---|
| 마크다운 | 노션처럼 마크다운 작성 + 실시간 미리보기 |
| 파일 병합 | 파일들을 클릭 순서대로 연결 후 하나로 병합 |
| 브레인스토밍 | 파일 관계를 화살표로 시각화 |

---

## 3. 기술 스택

| 항목 | 내용 |
|---|---|
| 언어 | C# (.NET 8) |
| UI 프레임워크 | WPF |
| 에디터 | AvalonEdit |
| 아키텍처 | MVVM 패턴 |
| 데이터 저장 | System.Text.Json |

### 사용한 라이브러리
 
| 라이브러리 | 용도 |
|---|---|
| **AvalonEdit** | 마크다운 입력창, 미리보기 코드블록 하이라이팅 |
| **System.Text.Json** | `.brainstorm`, `.brainstorm_layout` JSON 저장/불러오기 |
| **System.Windows.Forms** | 폴더 선택 다이얼로그. WPF에 폴더 선택 창이 없어서 WinForms 것을 사용 |
 
---

## 4. MVVM 패턴

코드를 세 가지 역할로 나누는 설계 방식입니다. React의 컴포넌트 방식처럼 기능별로 분리해서 유지보수를 쉽게 하고 싶었습니다.

| 역할 | 설명 | React 비유 |
|---|---|---|
| **View** | XAML로 화면을 그리는 부분 | JSX |
| **ViewModel** | 데이터 가공, 사용자 동작 처리 | useState + 함수들 |
| **Model** | 순수한 데이터 구조 | TypeScript 인터페이스 |

```
View (화면, XAML)
    ↕  이벤트 / 데이터 바인딩
ViewModel (로직, C#)
    ↕
Model (데이터)
```

**View** — 화면에 값을 표시, ViewModel의 값이 바뀌면 자동으로 갱신
```xml
<TextBlock Text="{Binding Title}"/>
```

**ViewModel** — 마크다운 파싱, 저장/열기 로직 담당
```csharp
public string Title { get; set; } = "새 문서";
```

**Model** — 순수 데이터 구조
```csharp
public class Document
{
    public string Title { get; set; } = "Untitled";
    public string MarkdownContent { get; set; } = "";
}
```

### MarkFlow에서는?

원래 Firebase 연동을 고려해서 `Document.cs` Model을 설계했지만 구현하지 못했습니다. 현재는 `EditorViewModel`이 로직과 데이터 관리를 같이 담당하고 있어서 엄밀히 말하면 완전한 MVVM보다 **MV(VM) 구조**에 가깝습니다.

---

## 5. 프로젝트 구조

```
MarkFlow/
├── Models/          # 데이터 모델
├── ViewModels/      # 비즈니스 로직
├── Views/
│   ├── MainWindow   # 탭 전환, 사이드바
│   ├── EditorView   # 마크다운 에디터 + 미리보기
│   ├── MergeView    # 파일 병합 캔버스
│   └── BrainstormView # 브레인스토밍 캔버스
└── Highlighting/    # 커스텀 코드 하이라이팅
```

---

## 6. 핵심 기능 1 — 마크다운 실시간 파싱

### 구조

마크다운 관련 기능은 두 가지로 나뉩니다.

| 역할 | 구현 방식 |
|---|---|
| 에디터 입력창 (왼쪽) | AvalonEdit 라이브러리 사용 (줄 번호, 문법 색상 제공) |
| 미리보기 파싱 (오른쪽) | 직접 구현 (외부 라이브러리 없음) |

즉, **마크다운 문법을 화면으로 변환하는 파싱 로직은 직접 구현**했고,  
에디터 입력창과 미리보기 안의 코드블록 렌더링에만 AvalonEdit을 사용했습니다.

### 파싱 흐름

```csharp
public string MarkdownContent
{
    set
    {
        _markdownContent = value;
        OnPropertyChanged();
        UpdatePreview(); // 값이 바뀔 때마다 자동 호출
    }
}
```

텍스트가 바뀔 때마다 `UpdatePreview()`가 호출되고,  
줄 단위로 마크다운 문법을 파싱해서 WPF `FlowDocument`로 변환합니다.

### 지원 문법

| 문법 | 예시 |
|---|---|
| 제목 | `# H1` `## H2` `### H3` |
| 강조 | `**굵음**` `*이탤릭*` |
| 리스트 | `- 항목` `1. 항목` |
| 표 | `\| 헤더 \| 헤더 \|` |
| 코드블록 | ` ```js ``` ` |
| 인용구 | `> 텍스트` |
| 링크 | `[텍스트](url)` |

### 코드블록 렌더링

미리보기 안의 코드블록은 AvalonEdit을 읽기 전용으로 삽입해서 문법 하이라이팅을 제공합니다.

```csharp
var editor = new ICSharpCode.AvalonEdit.TextEditor();
editor.IsReadOnly = true;
editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("JavaScript-Custom");
doc.Blocks.Add(new BlockUIContainer(editor)); // FlowDocument 안에 삽입
```

---

## 7. 핵심 기능 2 — 코드 하이라이팅

AvalonEdit 기본 하이라이팅은 파란색/검정색만 있어서 직접 `.xshd` 파일로 구현했습니다.

```xml
<Color name="Keyword" foreground="#0000FF" fontWeight="bold"/>
<Color name="String"  foreground="#A31515"/>
<Color name="Comment" foreground="#008000" fontStyle="italic"/>
<Color name="Number"  foreground="#098658"/>
```

미리보기의 코드블록은 AvalonEdit을 `BlockUIContainer`로 감싸서 FlowDocument에 삽입했습니다.

```csharp
var editor = new ICSharpCode.AvalonEdit.TextEditor();
editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("JavaScript-Custom");
doc.Blocks.Add(new BlockUIContainer(editor));
```

---

## 8. 핵심 기능 3 — 파일 병합 캔버스

> 클릭 순서대로 화살표가 연결되고, 병합하면 그 순서대로 하나의 md 파일이 됩니다.

```csharp
private void ToggleNodeOrder(FileNode node)
{
    _orderedNodes.Add(node);
    node.Order = _orderedNodes.Count; // #1, #2, #3 ...

    RebuildConnections(); // 순서대로 화살표 다시 그리기
}
```

### 왜 `.brainstorm` 파일을 만들었나?

- 별도의 데이터베이스가 없어서 **파일 자체가 기록 수단**이 되어야 했음
- `.brainstorm`은 **dotfile** — `.gitignore`, `.env`처럼 메타 데이터를 저장하는 관례적인 방식, 내용물은 **JSON 형식**
- 아이디어를 여러 번 병합하다 보면 **어떤 흐름으로 정리했는지 혼동**이 올 수 있어서 이력을 남김
- 결과 파일이 삭제되면 해당 이력도 자동 제거
- 기존 md 파일은 건드리지 않아서 **원본에 영향 없음**

```json
[
  {
    "Date": "2025-05-13 11:42",
    "Sources": ["idea1.md", "idea3.md", "idea2.md"],
    "Result": "merged.md"
  }
]
```

---

## 9. 핵심 기능 4 — 브레인스토밍 캔버스

> 노드를 자유롭게 배치하고, 클릭으로 연결, 호버 후 클릭으로 삭제, 레이아웃 저장/복원

### 드래그와 클릭 구분

```csharp
// 5px 이상 움직여야 드래그로 판정
var dist = Math.Sqrt(Math.Pow(dx, 2) + Math.Pow(dy, 2));
if (dist < 5) return;
_hasDragged = true;
```

### 레이아웃 저장/복원

- 각 노드에 `Guid`로 고유 Id 부여
- 노드 위치 + 연결 정보를 `.brainstorm_layout` JSON 파일에 저장
- 앱 재시작 시 자동 복원, 삭제된 파일은 자동 제거

---

## 10. 트러블슈팅

### 문제 1. 라이브러리(Markdig) 네임스페이스 충돌

처음에는 마크다운 파싱을 직접 구현하지 않고 **Markdig**이라는 외부 라이브러리를 사용하려 했습니다.

**Markdig이란?**  
마크다운 텍스트를 자동으로 파싱해주는 C# 라이브러리입니다.  
직접 구현할 필요 없이 `Markdown.Parse(text)`만 호출하면 됩니다.

**왜 충돌이 났나?**  
Markdig과 WPF 둘 다 동일한 이름의 클래스를 갖고 있었습니다.

| 클래스명 | WPF | Markdig |
|---|---|---|
| `Block` | 화면에 텍스트 문단을 그리는 클래스 | 마크다운 문단 구조를 나타내는 클래스 |
| `Inline` | WPF 문서 안의 인라인 요소 | 마크다운 인라인 요소 |
| `Table` | WPF 표 요소 | 마크다운 표 요소 |

이름이 완전히 같기 때문에 코드에서 `Block`이라고 쓰면 컴파일러가 어느 쪽인지 구분하지 못해서 아래 에러가 발생했습니다.

```
'Block'은(는) 'System.Windows.Documents.Block' 및
'Markdig.Syntax.Block' 사이에 모호한 참조입니다.
```

**왜 별칭으로 해결하지 않았나?**  
`using MdBlock = Markdig.Syntax.Block` 처럼 별칭을 붙여서 구분할 수 있지만,  
겹치는 클래스가 `Block`, `Inline`, `Table` 등 여러 개였기 때문에  
별칭을 하나하나 붙이다 보니 코드가 오히려 복잡해졌습니다.

**해결** → Markdig을 제거하고 마크다운 파싱을 직접 구현했습니다.

---

### 문제 2. AvalonEdit 바인딩 불가

**AvalonEdit이란?**
WPF 기본 `TextBox`는 줄 번호, 하이라이팅 같은 기능이 없어서 사용한 WPF용 코드 에디터 라이브러리입니다.

**왜 안됐나?**
WPF 기본 `TextBox`는 바인딩을 자동으로 지원하지만, AvalonEdit `TextEditor`는 라이브러리 한계로 바인딩을 지원하지 않습니다. 그래서 타이핑해도 `MarkdownContent` 상태가 업데이트되지 않아 아래 흐름이 끊겼습니다.

```
에디터에 타이핑
    ↓
MarkdownContent 상태 업데이트 ← 여기서 막힘
    ↓
UpdatePreview() 호출 (미리보기 갱신 함수)
    ↓
미리보기 갱신 ← 결국 안 됨
```

**해결** → `TextChanged` 이벤트로 직접 연결해서 타이핑할 때마다 수동으로 상태 업데이트

```csharp
MarkdownEditor.TextChanged += (s, e) =>
{
    _viewModel.MarkdownContent = MarkdownEditor.Text; // 직접 상태 업데이트
};
```



> **MarkFlow** — 아이디어의 흐름을 만드는 마크다운 에디터
