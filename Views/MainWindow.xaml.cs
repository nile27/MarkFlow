using System.IO;
using System.Windows;
using System.Windows.Controls;
using MarkFlow.ViewModels;

namespace MarkFlow
{
    public partial class MainWindow : Window
    {
        private EditorViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new EditorViewModel();
            DataContext = _viewModel;

            MarkdownEditor.SyntaxHighlighting =
                ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance
                .GetDefinition("MarkDown");

            // 에디터 → ViewModel
            MarkdownEditor.TextChanged += (s, e) =>
            {
                if (_viewModel.MarkdownContent != MarkdownEditor.Text)
                    _viewModel.MarkdownContent = MarkdownEditor.Text;
            };

            // ViewModel → 미리보기 + 에디터
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.PreviewDocument))
                    PreviewViewer.Document = _viewModel.PreviewDocument;

                if (e.PropertyName == nameof(_viewModel.MarkdownContent))
                {
                    if (MarkdownEditor.Text != _viewModel.MarkdownContent)
                        MarkdownEditor.Text = _viewModel.MarkdownContent;
                }
            };

            MarkdownEditor.Text = _viewModel.MarkdownContent;
            PreviewViewer.Document = _viewModel.PreviewDocument;
        }

        // 폴더 열기
        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var folderPath = dialog.SelectedPath;
                _viewModel.CurrentFolderPath = folderPath;
                FolderNameText.Text = Path.GetFileName(folderPath);

                var files = Directory.GetFiles(folderPath, "*.md")
                    .Select(f => new FileInfo(f))
                    .ToList();

                FileListBox.ItemsSource = files;
            }
        }

        // 파일 클릭
        private void FileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FileListBox.SelectedItem is FileInfo file)
            {
                _viewModel.CurrentFilePath = file.FullName;
                _viewModel.MarkdownContent = File.ReadAllText(file.FullName);
                _viewModel.Title = Path.GetFileNameWithoutExtension(file.FullName);
                MarkdownEditor.Text = _viewModel.MarkdownContent;
            }
        }

        // 열기
        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Markdown Files (*.md)|*.md|Text Files (*.txt)|*.txt"
            };
            if (dialog.ShowDialog() == true)
            {
                _viewModel.CurrentFilePath = dialog.FileName;
                _viewModel.MarkdownContent = File.ReadAllText(dialog.FileName);
                _viewModel.Title = Path.GetFileNameWithoutExtension(dialog.FileName);
                MarkdownEditor.Text = _viewModel.MarkdownContent;
            }
        }

        // 저장
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.MarkdownContent = MarkdownEditor.Text;
            _viewModel.Save();
        }

        // 새 문서
        private void NewDocumentButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CurrentFilePath = null;
            _viewModel.Title = "새 문서";
            _viewModel.MarkdownContent = "";
            MarkdownEditor.Text = "";
        }

    }
}