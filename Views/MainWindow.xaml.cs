using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarkFlow.ViewModels;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;

namespace MarkFlow.Views
{
    public partial class MainWindow : Window
    {
        private EditorViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            if (DataContext is not EditorViewModel)
                DataContext = new EditorViewModel();

            _viewModel = (EditorViewModel)DataContext;

            EditorView.Initialize();

            EditorView.TextChanged += text =>
            {
                _viewModel.MarkdownContent = text;
            };

            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.PreviewDocument))
                    EditorView.Preview = _viewModel.PreviewDocument;
                if (e.PropertyName == nameof(_viewModel.MarkdownContent))
                    EditorView.EditorText = _viewModel.MarkdownContent;
                if (e.PropertyName == nameof(_viewModel.Title))
                    TitleText.Text = _viewModel.Title;
            };

            MergeView.OnFileMerged += RefreshFileList;
            BrainstormView.OnNodeCreated += RefreshFileList;
        }

        private void SetTab(string tab)
        {
            if (tab != "markdown" && string.IsNullOrEmpty(_viewModel.CurrentFolderPath))
            {
                MessageBox.Show("먼저 폴더를 선택해주세요.", "MarkFlow");
                return;
            }
            EditorView.Visibility = tab == "markdown" ? Visibility.Visible : Visibility.Collapsed;
            MergeView.Visibility = tab == "merge" ? Visibility.Visible : Visibility.Collapsed;
            BrainstormView.Visibility = tab == "brainstorm" ? Visibility.Visible : Visibility.Collapsed;

            Sidebar.Visibility = tab == "markdown" ? Visibility.Visible : Visibility.Collapsed;
            SidebarSplitter.Visibility = tab == "markdown" ? Visibility.Visible : Visibility.Collapsed;
            SidebarColumn.Width = tab == "markdown" ? new GridLength(240, GridUnitType.Pixel) : new GridLength(0);
            SplitterColumn.Width = tab == "markdown" ? new GridLength(5) : new GridLength(0);

            EditorButtons.Visibility = tab == "markdown" ? Visibility.Visible : Visibility.Collapsed;
            TitleText.Visibility = tab == "markdown" ? Visibility.Visible : Visibility.Collapsed;

            TabMarkdown.Background = tab == "markdown"
                ? new SolidColorBrush(Color.FromRgb(23, 23, 23)) : System.Windows.Media.Brushes.Transparent;
            TabMarkdown.Foreground = tab == "markdown"
                ? System.Windows.Media.Brushes.White : new SolidColorBrush(Color.FromRgb(97, 93, 89));

            TabMerge.Background = tab == "merge"
                ? new SolidColorBrush(Color.FromRgb(23, 23, 23)) : System.Windows.Media.Brushes.Transparent;
            TabMerge.Foreground = tab == "merge"
                ? System.Windows.Media.Brushes.White : new SolidColorBrush(Color.FromRgb(97, 93, 89));

            TabBrainstorm.Background = tab == "brainstorm"
                ? new SolidColorBrush(Color.FromRgb(23, 23, 23)) : System.Windows.Media.Brushes.Transparent;
            TabBrainstorm.Foreground = tab == "brainstorm"
                ? System.Windows.Media.Brushes.White : new SolidColorBrush(Color.FromRgb(97, 93, 89));

            if (!string.IsNullOrEmpty(_viewModel.CurrentFolderPath))
            {
                if (tab == "merge") MergeView.LoadFolder(_viewModel.CurrentFolderPath);
                if (tab == "brainstorm") BrainstormView.LoadFolder(_viewModel.CurrentFolderPath);
            }
        }

        private void TabMarkdown_Click(object sender, RoutedEventArgs e) => SetTab("markdown");
        private void TabMerge_Click(object sender, RoutedEventArgs e) => SetTab("merge");
        private void TabBrainstorm_Click(object sender, RoutedEventArgs e) => SetTab("brainstorm");

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _viewModel.CurrentFolderPath = dialog.SelectedPath;
                FolderNameText.Text = System.IO.Path.GetFileName(dialog.SelectedPath);

                _viewModel.CurrentFilePath = null;
                _viewModel.Title = "새 문서";
                _viewModel.MarkdownContent = "";
                EditorView.EditorText = "";
                TitleText.Text = "새 문서";

                MergeView.LoadFolder(dialog.SelectedPath);

                RefreshFileList();
            }
        }

        private void RefreshFileList()
        {
            if (string.IsNullOrEmpty(_viewModel.CurrentFolderPath)) return;
            var files = Directory.GetFiles(_viewModel.CurrentFolderPath, "*.md")
                .Select(f => new System.IO.FileInfo(f)).ToList();
            FileListBox.ItemsSource = files;
        }

        private void FileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FileListBox.SelectedItem is System.IO.FileInfo file)
            {
                _viewModel.CurrentFilePath = file.FullName;
                _viewModel.MarkdownContent = File.ReadAllText(file.FullName);
                _viewModel.Title = file.Name;
                EditorView.EditorText = _viewModel.MarkdownContent;
                TitleText.Text = file.Name;
            }
        }

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
                _viewModel.Title = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
                EditorView.EditorText = _viewModel.MarkdownContent;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.MarkdownContent = EditorView.EditorText;
            _viewModel.Save();
        }

        private void NewDocumentButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CurrentFilePath = null;
            _viewModel.Title = "새 문서";
            _viewModel.MarkdownContent = "";
            EditorView.EditorText = "";
            TitleText.Text = "새 문서";
        }

        private void DeleteFile_Click(object sender, RoutedEventArgs e)
        {
            if (FileListBox.SelectedItem is System.IO.FileInfo file)
            {
                var result = MessageBox.Show($"{file.Name}을(를) 삭제하시겠습니까?",
                    "파일 삭제", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    File.Delete(file.FullName);
                    if (_viewModel.CurrentFilePath == file.FullName)
                    {
                        _viewModel.CurrentFilePath = null;
                        _viewModel.Title = "새 문서";
                        _viewModel.MarkdownContent = "";
                        EditorView.EditorText = "";
                        TitleText.Text = "새 문서";
                    }
                    RefreshFileList();
                    MergeView.RemoveHistoryByFile(file.Name);
                }
            }
        }
    }
}