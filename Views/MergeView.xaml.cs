using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Text.Json;

using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Path = System.IO.Path;
using Cursors = System.Windows.Input.Cursors;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace MarkFlow.Views
{
    public partial class MergeView : UserControl
    {
        private class FileNode
        {
            public string FilePath { get; set; } = "";
            public string FileName { get; set; } = "";
            public double X { get; set; }
            public double Y { get; set; }
            public Border? Control { get; set; }
            public int Order { get; set; } = 0;
        }

        private class Connection
        {
            public FileNode From { get; set; } = null!;
            public FileNode To { get; set; } = null!;
            public Line? Line { get; set; }
            public Polygon? Arrow { get; set; }
        }

        private class HistoryEntry
        {
            public string Date { get; set; } = "";
            public List<string> Sources { get; set; } = new();
            public string Result { get; set; } = "";
        }

        private List<FileNode> _nodes = new();
        private List<Connection> _connections = new();
        private List<FileNode> _orderedNodes = new();
        private FileNode? _draggingNode = null;
        private Point _dragOffset;
        private bool _hasDragged = false;
        private Point _dragStartPos;
        private string _folderPath = "";

        private double _zoom = 1.0;
        private ScaleTransform _scaleTransform = new ScaleTransform(1, 1);

        public event Action? OnFileMerged;

        public MergeView()
        {
            InitializeComponent();
            MergeCanvas.LayoutTransform = _scaleTransform;
        }

        public void LoadFolder(string folderPath)
        {
            _folderPath = folderPath;
            ClearAll();

            var files = Directory.GetFiles(folderPath, "*.md");
            int col = 0, row = 0;

            foreach (var file in files)
            {
                var node = new FileNode
                {
                    FilePath = file,
                    FileName = Path.GetFileName(file),
                    X = 80 + col * 240,
                    Y = 80 + row * 160
                };
                col++;
                if (col >= 5) { col = 0; row++; }
                CreateNodeControl(node);
                _nodes.Add(node);
            }

            LoadHistory();
        }

        private void CreateNodeControl(FileNode node)
        {
            var border = new Border
            {
                Width = 180,
                Height = 80,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(8),
                Cursor = Cursors.Hand,
                Tag = node
            };

            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };

            var orderText = new TextBlock
            {
                Text = "",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(35, 131, 226)),
                TextAlignment = TextAlignment.Center,
                Tag = "order"
            };

            var nameText = new TextBlock
            {
                Text = node.FileName,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(8, 2, 8, 0)
            };

            stack.Children.Add(orderText);
            stack.Children.Add(nameText);
            border.Child = stack;

            border.MouseLeftButtonDown += Node_MouseLeftButtonDown;
            border.MouseLeftButtonUp += Node_MouseLeftButtonUp;
            border.MouseMove += Node_MouseMove;

            Canvas.SetLeft(border, node.X);
            Canvas.SetTop(border, node.Y);
            Canvas.SetZIndex(border, 10);
            MergeCanvas.Children.Add(border);
            node.Control = border;
        }

        private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is FileNode node)
            {
                _draggingNode = node;
                _hasDragged = false;
                _dragStartPos = e.GetPosition(MergeCanvas);
                _dragOffset = e.GetPosition(border);
                border.CaptureMouse();
                e.Handled = true;
            }
        }

        private void Node_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is FileNode node)
            {
                border.ReleaseMouseCapture();
                _draggingNode = null;

                if (!_hasDragged)
                    ToggleNodeOrder(node);

                _hasDragged = false;
                e.Handled = true;
            }
        }

        private void Node_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggingNode != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(MergeCanvas);
                if (!_hasDragged)
                {
                    var dist = Math.Sqrt(Math.Pow(pos.X - _dragStartPos.X, 2) + Math.Pow(pos.Y - _dragStartPos.Y, 2));
                    if (dist < 5) return;
                    _hasDragged = true;
                }

                var newX = pos.X - _dragOffset.X;
                var newY = pos.Y - _dragOffset.Y;
                _draggingNode.X = Math.Max(0, Math.Min(newX, MergeCanvas.Width - 180));
                _draggingNode.Y = Math.Max(0, Math.Min(newY, MergeCanvas.Height - 80));

                Canvas.SetLeft(_draggingNode.Control!, _draggingNode.X);
                Canvas.SetTop(_draggingNode.Control!, _draggingNode.Y);
                UpdateConnections();
            }
        }

        private void ToggleNodeOrder(FileNode node)
        {
            if (_orderedNodes.Contains(node))
            {
                _orderedNodes.Remove(node);
                node.Order = 0;
                for (int i = 0; i < _orderedNodes.Count; i++)
                    _orderedNodes[i].Order = i + 1;
            }
            else
            {
                _orderedNodes.Add(node);
                node.Order = _orderedNodes.Count;
            }

            UpdateAllNodeUI();
            RebuildConnections();
        }

        private void UpdateAllNodeUI()
        {
            foreach (var node in _nodes)
            {
                if (node.Control?.Child is StackPanel stack)
                {
                    var orderText = stack.Children.OfType<TextBlock>().FirstOrDefault(t => t.Tag?.ToString() == "order");
                    if (orderText != null)
                        orderText.Text = node.Order > 0 ? $"#{node.Order}" : "";
                }

                if (node.Control != null)
                {
                    node.Control.BorderBrush = node.Order > 0
                        ? new SolidColorBrush(Color.FromRgb(35, 131, 226))
                        : new SolidColorBrush(Color.FromRgb(200, 200, 200));
                    node.Control.BorderThickness = node.Order > 0
                        ? new Thickness(2.5)
                        : new Thickness(1.5);
                }
            }
        }

        private void RebuildConnections()
        {
            foreach (var conn in _connections)
            {
                if (conn.Line != null) MergeCanvas.Children.Remove(conn.Line);
                if (conn.Arrow != null) MergeCanvas.Children.Remove(conn.Arrow);
            }
            _connections.Clear();

            for (int i = 0; i < _orderedNodes.Count - 1; i++)
                AddConnection(_orderedNodes[i], _orderedNodes[i + 1]);
        }

        private void AddConnection(FileNode from, FileNode to)
        {
            var line = new Line
            {
                Stroke = new SolidColorBrush(Color.FromRgb(35, 131, 226)),
                StrokeThickness = 2,
                IsHitTestVisible = false
            };
            var arrow = new Polygon
            {
                Fill = new SolidColorBrush(Color.FromRgb(35, 131, 226)),
                IsHitTestVisible = false
            };

            Canvas.SetZIndex(line, 3);
            Canvas.SetZIndex(arrow, 4);
            MergeCanvas.Children.Add(line);
            MergeCanvas.Children.Add(arrow);

            var conn = new Connection { From = from, To = to, Line = line, Arrow = arrow };
            _connections.Add(conn);
            UpdateConnection(conn);
        }

        private void UpdateConnections()
        {
            foreach (var conn in _connections)
                UpdateConnection(conn);
        }

        private void UpdateConnection(Connection conn)
        {
            var from = GetBorderPoint(conn.From, conn.To.X + 90, conn.To.Y + 40);
            var to = GetBorderPoint(conn.To, conn.From.X + 90, conn.From.Y + 40);

            if (conn.Line != null)
            {
                conn.Line.X1 = from.X; conn.Line.Y1 = from.Y;
                conn.Line.X2 = to.X; conn.Line.Y2 = to.Y;
            }

            if (conn.Arrow != null)
            {
                double angle = Math.Atan2(to.Y - from.Y, to.X - from.X);
                double size = 10;
                conn.Arrow.Points = new PointCollection
                {
                    new Point(to.X, to.Y),
                    new Point(to.X - Math.Cos(angle) * size * 1.5 - Math.Sin(angle) * size * 0.6,
                              to.Y - Math.Sin(angle) * size * 1.5 + Math.Cos(angle) * size * 0.6),
                    new Point(to.X - Math.Cos(angle) * size * 1.5 + Math.Sin(angle) * size * 0.6,
                              to.Y - Math.Sin(angle) * size * 1.5 - Math.Cos(angle) * size * 0.6)
                };
            }
        }

        private Point GetBorderPoint(FileNode node, double targetX, double targetY)
        {
            double cx = node.X + 90;
            double cy = node.Y + 40;
            double dx = targetX - cx;
            double dy = targetY - cy;
            if (dx == 0 && dy == 0) return new Point(cx, cy);
            double scaleX = dx != 0 ? 90.0 / Math.Abs(dx) : double.MaxValue;
            double scaleY = dy != 0 ? 40.0 / Math.Abs(dy) : double.MaxValue;
            double scale = Math.Min(scaleX, scaleY);
            return new Point(cx + dx * scale, cy + dy * scale);
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { }

        private void MergeExecute_Click(object sender, RoutedEventArgs e)
        {
            if (_orderedNodes.Count < 2)
            {
                MessageBox.Show("2개 이상 파일을 선택해주세요.", "MarkFlow");
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Markdown Files (*.md)|*.md",
                DefaultExt = ".md",
                FileName = "merged",
                InitialDirectory = _folderPath
            };

            if (dialog.ShowDialog() == true)
            {
                var merged = string.Join("\n\n---\n\n", _orderedNodes.Select(n => File.ReadAllText(n.FilePath)));
                File.WriteAllText(dialog.FileName, merged, System.Text.Encoding.UTF8);

                var resultFileName = Path.GetFileName(dialog.FileName);
                SaveHistory(resultFileName);
                LoadHistory();
                MergeReset_Click(null, null!);

                MessageBox.Show($"병합 완료! → {resultFileName}", "MarkFlow");
                OnFileMerged?.Invoke();
            }
        }

        private void MergeReset_Click(object sender, RoutedEventArgs e)
        {
            _orderedNodes.Clear();
            foreach (var node in _nodes) node.Order = 0;
            UpdateAllNodeUI();
            RebuildConnections();
        }

        private void ClearAll()
        {
            MergeCanvas.Children.Clear();
            _nodes.Clear();
            _connections.Clear();
            _orderedNodes.Clear();
        }

        private void SaveHistory(string resultFile)
        {
            var brainstormPath = Path.Combine(_folderPath, ".brainstorm");
            var entries = new List<HistoryEntry>();

            if (File.Exists(brainstormPath))
            {
                try
                {
                    var existing = JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(brainstormPath));
                    if (existing != null) entries = existing;
                }
                catch { }
            }

            entries.Insert(0, new HistoryEntry
            {
                Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Sources = _orderedNodes.Select(n => n.FileName).ToList(),
                Result = resultFile
            });

            File.WriteAllText(brainstormPath, JsonSerializer.Serialize(entries));
        }

        private void LoadHistory()
        {
            HistoryPanel.Children.Clear();
            var brainstormPath = Path.Combine(_folderPath, ".brainstorm");
            if (!File.Exists(brainstormPath)) return;

            try
            {
                var entries = JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(brainstormPath));
                if (entries == null) return;

                // 결과 파일이 존재하지 않는 항목 제거
                var valid = entries.Where(e =>
                    File.Exists(Path.Combine(_folderPath, e.Result))).ToList();

                // 정리된 내용 다시 저장
                if (valid.Count != entries.Count)
                    File.WriteAllText(brainstormPath, JsonSerializer.Serialize(valid));

                foreach (var entry in valid)
                    AddHistory(entry.Sources, entry.Result, entry.Date);
            }
            catch { }
        }

        private void AddHistory(List<string> sourceFiles, string resultFile, string? date = null)
        {
            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(10)
            };

            var stack = new StackPanel();

            var resultText = new TextBlock
            {
                Text = resultFile,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(35, 131, 226)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            };

            var sourcesText = new TextBlock
            {
                Text = string.Join(" + ", sourceFiles),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(97, 93, 89)),
                TextWrapping = TextWrapping.Wrap
            };

            var timeText = new TextBlock
            {
                Text = date ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
                Margin = new Thickness(0, 4, 0, 0)
            };

            stack.Children.Add(resultText);
            stack.Children.Add(sourcesText);
            stack.Children.Add(timeText);
            border.Child = stack;

            HistoryPanel.Children.Insert(0, border);
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (e.Delta > 0) ZoomIn();
                else ZoomOut();
                e.Handled = true;
            }
        }

        private void ZoomIn()
        {
            _zoom = Math.Min(_zoom + 0.1, 3.0);
            _scaleTransform.ScaleX = _zoom;
            _scaleTransform.ScaleY = _zoom;
        }

        private void ZoomOut()
        {
            _zoom = Math.Max(_zoom - 0.1, 0.3);
            _scaleTransform.ScaleX = _zoom;
            _scaleTransform.ScaleY = _zoom;
        }

        public void RemoveHistoryByFile(string fileName)
        {
            var brainstormPath = Path.Combine(_folderPath, ".brainstorm");
            if (!File.Exists(brainstormPath)) return;

            try
            {
                var entries = JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(brainstormPath));
                if (entries == null) return;

                // 삭제된 파일이 포함된 항목 제거
                entries.RemoveAll(e => e.Sources.Contains(fileName) || e.Result == fileName);

                File.WriteAllText(brainstormPath, JsonSerializer.Serialize(entries));
                LoadHistory();
            }
            catch { }
        }
    }
}