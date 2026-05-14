using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

namespace MarkFlow.ViewModels
{
    public class EditorViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private string _title = "새 문서";
        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        private string _markdownContent = "";
        public string MarkdownContent
        {
            get => _markdownContent;
            set
            {
                _markdownContent = value;
                OnPropertyChanged();
                UpdatePreview();
            }
        }

        private FlowDocument _previewDocument = new FlowDocument();
        public FlowDocument PreviewDocument
        {
            get => _previewDocument;
            set { _previewDocument = value; OnPropertyChanged(); }
        }

        public ICommand SaveCommand { get; }
        public ICommand OpenCommand { get; }
        public ICommand NewDocumentCommand { get; }

        public Action? OnBeforeSave { get; set; }

        public string? CurrentFilePath { get; set; } = null;
        public string? CurrentFolderPath { get; set; } = null;

        public EditorViewModel()
        {
            SaveCommand = new RelayCommand(Save);
            OpenCommand = new RelayCommand(Open);
            NewDocumentCommand = new RelayCommand(NewDocument);

            RegisterCustomHighlighting();
        }

        private void RegisterCustomHighlighting()
        {
            // JavaScript
            var jsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Highlighting", "JavaScript.xshd");
            //MessageBox.Show($"JS 파일 존재: {System.IO.File.Exists(jsPath)}\n경로: {jsPath}");
            if (System.IO.File.Exists(jsPath))
            {
                using var reader = new System.Xml.XmlTextReader(jsPath);
                var jsHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(
                    reader, ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance);
                ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance
                    .RegisterHighlighting("JavaScript-Custom", new[] { ".js" }, jsHighlighting);
            }

            // C#
            var csPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Highlighting", "CSharp.xshd");
            if (System.IO.File.Exists(csPath))
            {
                using var reader = new System.Xml.XmlTextReader(csPath);
                var csHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(
                    reader, ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance);
                ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance
                    .RegisterHighlighting("CSharp-Custom", new[] { ".cs" }, csHighlighting);
            }
        }

        private void UpdatePreview()
        {
            var doc = new FlowDocument();
            doc.FontFamily = new FontFamily("Segoe UI");
            doc.FontSize = 14;
            doc.PagePadding = new Thickness(20);

            var lines = _markdownContent.Split('\n');
            int i = 0;

            while (i < lines.Length)
            {
                var trimmed = lines[i].TrimEnd();

                // 코드블록
                if (trimmed.StartsWith("```"))
                {
                    var lang = trimmed.Substring(3).Trim();
                    i++;
                    var code = "";
                    while (i < lines.Length && !lines[i].TrimEnd().StartsWith("```"))
                    {
                        code += lines[i] + "\n";
                        i++;
                    }

                    var editor = new ICSharpCode.AvalonEdit.TextEditor();
                    editor.Text = code.TrimEnd();
                    editor.IsReadOnly = true;
                    editor.FontFamily = new FontFamily("Consolas, Courier New");
                    editor.FontSize = 13;
                    editor.Background = new SolidColorBrush(Color.FromRgb(246, 245, 244));
                    editor.BorderThickness = new Thickness(0);
                    editor.Padding = new Thickness(12);
                    editor.MinHeight = 40;
                    editor.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
                    editor.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;

                    if (!string.IsNullOrEmpty(lang))
                    {
                        ICSharpCode.AvalonEdit.Highlighting.IHighlightingDefinition? highlighting = null;

                        if (lang == "javascript" || lang == "js")
                            highlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("JavaScript-Custom");
                        else if (lang == "csharp" || lang == "cs" || lang == "c#")
                            highlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("CSharp-Custom");
                        else
                            highlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance
                                .GetDefinitionByExtension("." + lang switch
                                {
                                    "python" or "py" => "py",
                                    "html" => "html",
                                    "css" => "css",
                                    "xml" => "xml",
                                    "json" => "json",
                                    "sql" => "sql",
                                    "cpp" or "c++" => "cpp",
                                    "java" => "java",
                                    _ => lang
                                });

                        if (highlighting != null)
                            editor.SyntaxHighlighting = highlighting;
                        if (highlighting != null)
                            editor.SyntaxHighlighting = highlighting;
                    }

                    var container = new BlockUIContainer(editor);
                    container.Margin = new Thickness(0, 4, 0, 4);
                    doc.Blocks.Add(container);
                    i++;
                    continue;
                }

                // 수평선
                if (trimmed == "---" || trimmed == "***" || trimmed == "___")
                {
                    var line = new BlockUIContainer(new System.Windows.Controls.Separator());
                    line.Margin = new Thickness(0, 8, 0, 8);
                    doc.Blocks.Add(line);
                    i++;
                    continue;
                }

                // 표
                if (trimmed.Contains("|") &&
                    i + 1 < lines.Length &&
                    System.Text.RegularExpressions.Regex.IsMatch(lines[i + 1].Trim(), @"^[\|\-\s:]+$"))
                {
                    var table = new Table();
                    table.BorderBrush = Brushes.LightGray;
                    table.BorderThickness = new Thickness(1);
                    table.CellSpacing = 0;
                    table.Margin = new Thickness(0, 8, 0, 8);

                    var headerCells = trimmed.Split('|').Where(c => c.Trim() != "").ToList();
                    foreach (var _ in headerCells)
                        table.Columns.Add(new TableColumn());

                    var rowGroup = new TableRowGroup();
                    table.RowGroups.Add(rowGroup);

                    var headerRow = new TableRow();
                    headerRow.Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                    foreach (var cell in headerCells)
                    {
                        var p = new Paragraph();
                        p.FontWeight = FontWeights.Bold;
                        ParseInline(p, cell.Trim());
                        var tc = new TableCell(p);
                        tc.Padding = new Thickness(8, 4, 8, 4);
                        tc.BorderBrush = Brushes.LightGray;
                        tc.BorderThickness = new Thickness(1);
                        headerRow.Cells.Add(tc);
                    }
                    rowGroup.Rows.Add(headerRow);

                    i += 2;

                    while (i < lines.Length && lines[i].TrimEnd().Contains("|"))
                    {
                        var dataCells = lines[i].TrimEnd().Split('|').Where(c => c.Trim() != "").ToList();
                        var dataRow = new TableRow();
                        foreach (var cell in dataCells)
                        {
                            var p = new Paragraph();
                            ParseInline(p, cell.Trim());
                            var tc = new TableCell(p);
                            tc.Padding = new Thickness(8, 4, 8, 4);
                            tc.BorderBrush = Brushes.LightGray;
                            tc.BorderThickness = new Thickness(1);
                            dataRow.Cells.Add(tc);
                        }
                        rowGroup.Rows.Add(dataRow);
                        i++;
                    }

                    doc.Blocks.Add(table);
                    continue;
                }

                // 인용구
                if (trimmed.StartsWith("> "))
                {
                    var p = new Paragraph();
                    p.Margin = new Thickness(16, 2, 0, 2);
                    p.Padding = new Thickness(12, 4, 4, 4);
                    p.BorderBrush = new SolidColorBrush(Color.FromRgb(97, 93, 89));
                    p.BorderThickness = new Thickness(4, 0, 0, 0);
                    p.Background = new SolidColorBrush(Color.FromRgb(246, 245, 244));
                    p.Foreground = new SolidColorBrush(Color.FromRgb(97, 93, 89));
                    ParseInline(p, trimmed.Substring(2));
                    doc.Blocks.Add(p);
                    i++;
                    continue;
                }

                // 번호 리스트
                if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d+\. "))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"^(\d+)\. (.*)");
                    var p = new Paragraph();
                    p.Margin = new Thickness(16, 1, 0, 1);
                    ParseInline(p, match.Groups[1].Value + ". " + match.Groups[2].Value);
                    doc.Blocks.Add(p);
                    i++;
                    continue;
                }

                // H1
                if (trimmed.StartsWith("# "))
                {
                    var p = new Paragraph();
                    p.FontSize = 28;
                    p.FontWeight = FontWeights.Bold;
                    p.Margin = new Thickness(0, 8, 0, 4);
                    ParseInline(p, trimmed.Substring(2));
                    doc.Blocks.Add(p);
                }
                // H2
                else if (trimmed.StartsWith("## "))
                {
                    var p = new Paragraph();
                    p.FontSize = 22;
                    p.FontWeight = FontWeights.Bold;
                    p.Margin = new Thickness(0, 6, 0, 3);
                    ParseInline(p, trimmed.Substring(3));
                    doc.Blocks.Add(p);
                }
                // H3
                else if (trimmed.StartsWith("### "))
                {
                    var p = new Paragraph();
                    p.FontSize = 18;
                    p.FontWeight = FontWeights.Bold;
                    p.Margin = new Thickness(0, 4, 0, 2);
                    ParseInline(p, trimmed.Substring(4));
                    doc.Blocks.Add(p);
                }
                // 불릿 리스트
                else if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                {
                    var p = new Paragraph();
                    p.Margin = new Thickness(16, 1, 0, 1);
                    p.Inlines.Add(new Run("• "));
                    ParseInline(p, trimmed.Substring(2));
                    doc.Blocks.Add(p);
                }
                // 빈 줄
                else if (string.IsNullOrWhiteSpace(trimmed))
                {
                    doc.Blocks.Add(new Paragraph { Margin = new Thickness(0, 2, 0, 2) });
                }
                // 일반 텍스트
                else
                {
                    var p = new Paragraph();
                    p.Margin = new Thickness(0, 1, 0, 1);
                    ParseInline(p, trimmed);
                    doc.Blocks.Add(p);
                }

                i++;
            }

            PreviewDocument = doc;
        }

        private void ParseInline(Paragraph p, string text)
        {
            int i = 0;
            string current = "";

            while (i < text.Length)
            {
                // 굵음 **
                if (i + 1 < text.Length && text[i] == '*' && text[i + 1] == '*')
                {
                    if (current.Length > 0) { p.Inlines.Add(new Run(current)); current = ""; }
                    int end = text.IndexOf("**", i + 2);
                    if (end > 0)
                    {
                        p.Inlines.Add(new Bold(new Run(text.Substring(i + 2, end - i - 2))));
                        i = end + 2;
                    }
                    else { current += text[i]; i++; }
                }
                // 이탤릭 *
                else if (text[i] == '*')
                {
                    if (current.Length > 0) { p.Inlines.Add(new Run(current)); current = ""; }
                    int end = text.IndexOf('*', i + 1);
                    if (end > 0)
                    {
                        p.Inlines.Add(new Italic(new Run(text.Substring(i + 1, end - i - 1))));
                        i = end + 1;
                    }
                    else { current += text[i]; i++; }
                }
                // 인라인 코드 `
                else if (text[i] == '`')
                {
                    if (current.Length > 0) { p.Inlines.Add(new Run(current)); current = ""; }
                    int end = text.IndexOf('`', i + 1);
                    if (end > 0)
                    {
                        var run = new Run(text.Substring(i + 1, end - i - 1));
                        run.FontFamily = new FontFamily("Consolas, Courier New");
                        run.Background = new SolidColorBrush(Color.FromRgb(246, 245, 244));
                        p.Inlines.Add(run);
                        i = end + 1;
                    }
                    else { current += text[i]; i++; }
                }
                // 링크 [텍스트](url)
                else if (text[i] == '[')
                {
                    int closeBracket = text.IndexOf(']', i);
                    if (closeBracket > 0 && closeBracket + 1 < text.Length && text[closeBracket + 1] == '(')
                    {
                        int closeParen = text.IndexOf(')', closeBracket + 1);
                        if (closeParen > 0)
                        {
                            if (current.Length > 0) { p.Inlines.Add(new Run(current)); current = ""; }
                            var linkText = text.Substring(i + 1, closeBracket - i - 1);
                            var url = text.Substring(closeBracket + 2, closeParen - closeBracket - 2);
                            var hyperlink = new Hyperlink(new Run(linkText));
                            hyperlink.Foreground = new SolidColorBrush(Color.FromRgb(35, 131, 226));
                            try { hyperlink.NavigateUri = new Uri(url); } catch { }
                            hyperlink.RequestNavigate += (s, e) =>
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                            };
                            p.Inlines.Add(hyperlink);
                            i = closeParen + 1;
                            continue;
                        }
                    }
                    current += text[i]; i++;
                }
                else
                {
                    current += text[i]; i++;
                }
            }

            if (current.Length > 0) p.Inlines.Add(new Run(current));
        }

        public void Save()
        {
            
            if (CurrentFilePath != null)
            {
                // 기존 파일에 바로 저장
                File.WriteAllText(CurrentFilePath, _markdownContent, System.Text.Encoding.UTF8);
                MessageBox.Show("저장 완료!", "MarkFlow", MessageBoxButton.OK);
                return;
            }

            // 새 문서면 파일 탐색기 열기
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Markdown Files (*.md)|*.md|Text Files (*.txt)|*.txt",
                DefaultExt = ".md",
                FileName = Title,
                InitialDirectory = CurrentFolderPath ?? ""
            };
            if (dialog.ShowDialog() == true)
            {
                CurrentFilePath = dialog.FileName;
                File.WriteAllText(dialog.FileName, _markdownContent, System.Text.Encoding.UTF8);
                Title = Path.GetFileNameWithoutExtension(dialog.FileName);
                MessageBox.Show("저장 완료!", "MarkFlow", MessageBoxButton.OK);
            }
        }

        public void Open()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Markdown Files (*.md)|*.md|Text Files (*.txt)|*.txt"
            };
            if (dialog.ShowDialog() == true)
            {
                MarkdownContent = File.ReadAllText(dialog.FileName);
                Title = Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }

        private void NewDocument()
        {
            CurrentFilePath = null;
            Title = "새 문서";
            MarkdownContent = "";
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => _execute();
    }


}

//using System;
//using System.ComponentModel;
//using System.IO;
//using System.Linq;
//using System.Runtime.CompilerServices;
//using System.Windows;
//using System.Windows.Documents;
//using System.Windows.Input;
//using System.Windows.Media;
//using Markdig;
//using Markdig.Syntax;
//using Markdig.Syntax.Inlines;
//using MdBlock = Markdig.Syntax.Block;
//using MdInline = Markdig.Syntax.Inlines.Inline;
//using WpfBlock = System.Windows.Documents.Block;
//using WpfInline = System.Windows.Documents.Inline;

//namespace MarkFlow.ViewModels
//{
//    public class EditorViewModel : INotifyPropertyChanged
//    {
//        public event PropertyChangedEventHandler? PropertyChanged;
//        protected void OnPropertyChanged([CallerMemberName] string? name = null)
//            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

//        public Action? OnBeforeSave { get; set; }

//        private string _title = "새 문서";
//        public string Title
//        {
//            get => _title;
//            set { _title = value; OnPropertyChanged(); }
//        }

//        private string _markdownContent = "";
//        public string MarkdownContent
//        {
//            get => _markdownContent;
//            set
//            {
//                _markdownContent = value;
//                OnPropertyChanged();
//                UpdatePreview();
//            }
//        }

//        private FlowDocument _previewDocument = new FlowDocument();
//        public FlowDocument PreviewDocument
//        {
//            get => _previewDocument;
//            set { _previewDocument = value; OnPropertyChanged(); }
//        }

//        public ICommand SaveCommand { get; }
//        public ICommand OpenCommand { get; }
//        public ICommand NewDocumentCommand { get; }

//        private readonly MarkdownPipeline _pipeline;

//        public EditorViewModel()
//        {
//            SaveCommand = new RelayCommand(Save);
//            OpenCommand = new RelayCommand(Open);
//            NewDocumentCommand = new RelayCommand(NewDocument);

//            _pipeline = new MarkdownPipelineBuilder()
//                .UseAdvancedExtensions()
//                .UseSoftlineBreakAsHardlineBreak().Build();
//        }

//        private void UpdatePreview()
//        {
//            var doc = new FlowDocument();
//            doc.FontFamily = new FontFamily("Segoe UI");
//            doc.FontSize = 14;
//            doc.PagePadding = new Thickness(20);

//            var markdownDoc = Markdown.Parse(_markdownContent, _pipeline);

//            foreach (var block in markdownDoc)
//            {
//                var wpfBlock = ConvertBlock(block);
//                if (wpfBlock != null)
//                    doc.Blocks.Add(wpfBlock);
//            }

//            PreviewDocument = doc;
//        }

//        private WpfBlock? ConvertBlock(MdBlock block)
//        {
//            switch (block)
//            {
//                case HeadingBlock heading:
//                    {
//                        var p = new Paragraph();
//                        p.FontWeight = FontWeights.Bold;
//                        p.Margin = new Thickness(0, 8, 0, 4);
//                        p.FontSize = heading.Level switch
//                        {
//                            1 => 28,
//                            2 => 22,
//                            3 => 18,
//                            _ => 16
//                        };
//                        if (heading.Inline != null)
//                            foreach (var inline in heading.Inline)
//                                AddInline(p, inline);
//                        return p;
//                    }

//                case ParagraphBlock para:
//                    {
//                        var p = new Paragraph();
//                        p.Margin = new Thickness(0, 2, 0, 2);
//                        if (para.Inline != null)
//                            foreach (var inline in para.Inline)
//                                AddInline(p, inline);
//                        return p;
//                    }

//                case ListBlock list:
//                    {
//                        var section = new Section();
//                        int index = 1;
//                        foreach (var item in list.OfType<ListItemBlock>())
//                        {
//                            var p = new Paragraph();
//                            p.Margin = new Thickness(16, 1, 0, 1);
//                            var prefix = list.IsOrdered ? $"{index++}. " : "• ";
//                            p.Inlines.Add(new Run(prefix));
//                            foreach (var child in item.OfType<ParagraphBlock>())
//                                if (child.Inline != null)
//                                    foreach (var inline in child.Inline)
//                                        AddInline(p, inline);
//                            section.Blocks.Add(p);
//                        }
//                        return section;
//                    }

//                case QuoteBlock quote:
//                    {
//                        var section = new Section();
//                        section.Margin = new Thickness(16, 2, 0, 2);
//                        section.Padding = new Thickness(12, 4, 4, 4);
//                        section.BorderBrush = new SolidColorBrush(Color.FromRgb(97, 93, 89));
//                        section.BorderThickness = new Thickness(4, 0, 0, 0);
//                        section.Background = new SolidColorBrush(Color.FromRgb(246, 245, 244));
//                        foreach (var child in quote)
//                        {
//                            var converted = ConvertBlock(child);
//                            if (converted != null)
//                                section.Blocks.Add(converted);
//                        }
//                        return section;
//                    }

//                case FencedCodeBlock codeBlock:
//                    {
//                        var code = codeBlock.Lines.ToString();
//                        var lang = codeBlock.Info ?? "";

//                        var editor = new ICSharpCode.AvalonEdit.TextEditor();
//                        editor.Text = code.TrimEnd();
//                        editor.IsReadOnly = true;
//                        editor.FontFamily = new FontFamily("Consolas, Courier New");
//                        editor.FontSize = 13;
//                        editor.Background = new SolidColorBrush(Color.FromRgb(246, 245, 244));
//                        editor.BorderThickness = new Thickness(0);
//                        editor.Padding = new Thickness(12);
//                        editor.MinHeight = 40;
//                        editor.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
//                        editor.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;

//                        if (!string.IsNullOrEmpty(lang))
//                        {
//                            var ext = lang.ToLower() switch
//                            {
//                                "javascript" or "js" => ".js",
//                                "python" or "py" => ".py",
//                                "csharp" or "cs" => ".cs",
//                                "html" => ".html",
//                                "css" => ".css",
//                                "xml" => ".xml",
//                                "json" => ".json",
//                                "sql" or "tsql" => ".sql",
//                                "cpp" or "c++" => ".cpp",
//                                "java" => ".java",
//                                _ => "." + lang
//                            };
//                            var highlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance
//                                .GetDefinitionByExtension(ext);
//                            if (highlighting != null)
//                                editor.SyntaxHighlighting = highlighting;
//                        }

//                        return new BlockUIContainer(editor) { Margin = new Thickness(0, 4, 0, 4) };
//                    }

//                case ThematicBreakBlock:
//                    {
//                        return new BlockUIContainer(new System.Windows.Controls.Separator())
//                        {
//                            Margin = new Thickness(0, 8, 0, 8)
//                        };
//                    }

//                case Markdig.Extensions.Tables.Table table:
//                    {
//                        var wpfTable = new Table();
//                        wpfTable.BorderBrush = Brushes.LightGray;
//                        wpfTable.BorderThickness = new Thickness(1);
//                        wpfTable.CellSpacing = 0;
//                        wpfTable.Margin = new Thickness(0, 8, 0, 8);

//                        var rowGroup = new TableRowGroup();
//                        wpfTable.RowGroups.Add(rowGroup);

//                        bool isHeader = true;
//                        foreach (var row in table.OfType<Markdig.Extensions.Tables.TableRow>())
//                        {
//                            var wpfRow = new TableRow();
//                            if (isHeader)
//                                wpfRow.Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));

//                            foreach (var cell in row.OfType<Markdig.Extensions.Tables.TableCell>())
//                            {
//                                var p = new Paragraph();
//                                if (isHeader) p.FontWeight = FontWeights.Bold;
//                                foreach (var child in cell.OfType<ParagraphBlock>())
//                                    if (child.Inline != null)
//                                        foreach (var inline in child.Inline)
//                                            AddInline(p, inline);

//                                var tc = new TableCell(p);
//                                tc.Padding = new Thickness(8, 4, 8, 4);
//                                tc.BorderBrush = Brushes.LightGray;
//                                tc.BorderThickness = new Thickness(1);
//                                wpfRow.Cells.Add(tc);
//                            }

//                            if (isHeader)
//                            {
//                                foreach (var _ in row.OfType<Markdig.Extensions.Tables.TableCell>())
//                                    wpfTable.Columns.Add(new TableColumn());
//                            }

//                            rowGroup.Rows.Add(wpfRow);
//                            isHeader = false;
//                        }

//                        return wpfTable;
//                    }

//                default:
//                    return null;
//            }
//        }

//        private void AddInline(Paragraph p, MdInline inline)
//        {
//            switch (inline)
//            {
//                case LiteralInline literal:
//                    p.Inlines.Add(new Run(literal.Content.ToString()));
//                    break;

//                case EmphasisInline emphasis:
//                    {
//                        var span = emphasis.DelimiterCount == 2
//                            ? (WpfInline)new Bold()
//                            : new Italic();

//                        var innerP = new Paragraph();
//                        foreach (var child in emphasis)
//                            AddInline(innerP, child);

//                        if (span is Bold bold)
//                            foreach (var i in innerP.Inlines.ToList())
//                                bold.Inlines.Add(i);
//                        else if (span is Italic italic)
//                            foreach (var i in innerP.Inlines.ToList())
//                                italic.Inlines.Add(i);

//                        p.Inlines.Add(span);
//                        break;
//                    }

//                case CodeInline code:
//                    {
//                        var run = new Run(code.Content);
//                        run.FontFamily = new FontFamily("Consolas, Courier New");
//                        run.Background = new SolidColorBrush(Color.FromRgb(246, 245, 244));
//                        p.Inlines.Add(run);
//                        break;
//                    }

//                case LinkInline link:
//                    {
//                        var innerP = new Paragraph();
//                        foreach (var child in link)
//                            AddInline(innerP, child);

//                        var hyperlink = new Hyperlink();
//                        hyperlink.Foreground = new SolidColorBrush(Color.FromRgb(35, 131, 226));
//                        foreach (var i in innerP.Inlines.ToList())
//                            hyperlink.Inlines.Add(i);

//                        try { hyperlink.NavigateUri = new Uri(link.Url ?? ""); } catch { }
//                        hyperlink.RequestNavigate += (s, e) =>
//                        {
//                            System.Diagnostics.Process.Start(
//                                new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri)
//                                { UseShellExecute = true });
//                        };
//                        p.Inlines.Add(hyperlink);
//                        break;
//                    }

//                case LineBreakInline lineBreak:
//                    System.Diagnostics.Debug.WriteLine($"LineBreak 감지! IsHard: {lineBreak.IsHard}");
//                    p.Inlines.Add(new LineBreak());
//                    break;
//            }
//        }

//        public void Save()
//        {
//            var dialog = new Microsoft.Win32.SaveFileDialog
//            {
//                Filter = "Markdown Files (*.md)|*.md|Text Files (*.txt)|*.txt",
//                DefaultExt = ".md",
//                FileName = Title
//            };
//            if (dialog.ShowDialog() == true)
//            {
//                File.WriteAllText(dialog.FileName, _markdownContent, System.Text.Encoding.UTF8);
//                Title = Path.GetFileNameWithoutExtension(dialog.FileName);
//                MessageBox.Show("저장 완료!", "MarkFlow", MessageBoxButton.OK);
//            }
//        }

//        private void Open()
//        {
//            var dialog = new Microsoft.Win32.OpenFileDialog
//            {
//                Filter = "Markdown Files (*.md)|*.md|Text Files (*.txt)|*.txt"
//            };
//            if (dialog.ShowDialog() == true)
//            {
//                MarkdownContent = File.ReadAllText(dialog.FileName);
//                Title = Path.GetFileNameWithoutExtension(dialog.FileName);
//            }
//        }

//        private void NewDocument()
//        {
//            Title = "새 문서";
//            MarkdownContent = "";
//        }
//    }

//    public class RelayCommand : ICommand
//    {
//        private readonly Action _execute;
//        private readonly Func<bool>? _canExecute;

//        public RelayCommand(Action execute, Func<bool>? canExecute = null)
//        {
//            _execute = execute;
//            _canExecute = canExecute;
//        }

//        public event EventHandler? CanExecuteChanged;
//        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
//        public void Execute(object? parameter) => _execute();
//    }
//}