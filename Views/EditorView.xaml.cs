using System.Windows.Controls;
using System.Windows.Documents;
using ICSharpCode.AvalonEdit.Highlighting;
using UserControl = System.Windows.Controls.UserControl;

namespace MarkFlow.Views
{
    public partial class EditorView : UserControl
    {
        public EditorView()
        {
            InitializeComponent();
            MarkdownEditor.SyntaxHighlighting =
                HighlightingManager.Instance.GetDefinition("MarkDown");
        }

        public string EditorText
        {
            get => MarkdownEditor.Text;
            set { if (MarkdownEditor.Text != value) MarkdownEditor.Text = value; }
        }

        public FlowDocument? Preview
        {
            set => PreviewViewer.Document = value;
        }

        public event System.Action<string>? TextChanged;

        public void Initialize()
        {
            MarkdownEditor.TextChanged += (s, e) =>
                TextChanged?.Invoke(MarkdownEditor.Text);
        }
    }
}