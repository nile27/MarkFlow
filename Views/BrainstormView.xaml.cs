using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Path = System.IO.Path;
using Cursors = System.Windows.Input.Cursors;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace MarkFlow.Views
{
    public partial class BrainstormView : UserControl
    {
        private class BrainstormNode
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string FilePath { get; set; } = "";
            public string FileName { get; set; } = "";
            public double X { get; set; }
            public double Y { get; set; }
            public Border? Control { get; set; }
        }

        private class BrainstormConnection
        {
            public string FromId { get; set; } = "";
            public string ToId { get; set; } = "";
            public Line? Line { get; set; }
            public Polygon? Arrow { get; set; }
        }

        private class SaveData
        {
            public List<NodeData> Nodes { get; set; } = new();
            public List<ConnectionData> Connections { get; set; } = new();
        }

        private class NodeData
        {
            public string Id { get; set; } = "";
            public string FilePath { get; set; } = "";
            public double X { get; set; }
            public double Y { get; set; }
        }

        private class ConnectionData
        {
            public string FromId { get; set; } = "";
            public string ToId { get; set; } = "";
        }

        private List<BrainstormNode> _nodes = new();
        private List<BrainstormConnection> _connections = new();
        private BrainstormNode? _selectedNode = null;
        private BrainstormNode? _draggingNode = null;
        private Point _dragOffset;
        private bool _hasDragged = false;
        private Point _dragStartPos;
        private string _folderPath = "";

        private double _zoom = 1.0;
        private ScaleTransform _scaleTransform = new ScaleTransform(1, 1);

        public event Action? OnNodeCreated;

        public BrainstormView()
        {
            InitializeComponent();
            MainCanvas.LayoutTransform = _scaleTransform;
        }

        public void LoadFolder(string folderPath)
        {
            _folderPath = folderPath;
            ClearAll();
            TryLoadSave();
        }

        private void TryLoadSave()
{
    var savePath = Path.Combine(_folderPath, ".brainstorm_layout");
    if (!File.Exists(savePath))
    {
        LoadDefaultLayout();
        return;
    }

    try
    {
        var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(savePath));
        if (data == null) { LoadDefaultLayout(); return; }

        foreach (var nd in data.Nodes)
        {
            if (!File.Exists(nd.FilePath)) continue;
            var node = new BrainstormNode
            {
                Id = nd.Id,
                FilePath = nd.FilePath,
                FileName = Path.GetFileName(nd.FilePath),
                X = nd.X,
                Y = nd.Y
            };
            CreateNodeControl(node);
            _nodes.Add(node);
        }

        foreach (var cd in data.Connections)
        {
            var from = _nodes.FirstOrDefault(n => n.Id == cd.FromId);
            var to = _nodes.FirstOrDefault(n => n.Id == cd.ToId);
            if (from != null && to != null)
                AddConnection(from, to);
        }

        // 저장된 레이아웃에 없는 새 파일 추가
        var existingPaths = _nodes.Select(n => n.FilePath).ToHashSet();
        var allFiles = Directory.GetFiles(_folderPath, "*.md");
        int col = 0, row = _nodes.Count / 5;
        col = _nodes.Count % 5;

        foreach (var file in allFiles)
        {
            if (existingPaths.Contains(file)) continue;
            var node = new BrainstormNode
            {
                FilePath = file,
                FileName = Path.GetFileName(file),
                X = 80 + col * 240,
                Y = 80 + row * 180
            };
            col++;
            if (col >= 5) { col = 0; row++; }
            CreateNodeControl(node);
            _nodes.Add(node);
        }
    }
    catch { LoadDefaultLayout(); }
}

        private void LoadDefaultLayout()
        {
            var files = Directory.GetFiles(_folderPath, "*.md");
            int col = 0, row = 0;
            foreach (var file in files)
            {
                var node = new BrainstormNode
                {
                    FilePath = file,
                    FileName = Path.GetFileName(file),
                    X = 80 + col * 240,
                    Y = 80 + row * 180
                };
                col++;
                if (col >= 5) { col = 0; row++; }
                CreateNodeControl(node);
                _nodes.Add(node);
            }
        }

        private void CreateNodeControl(BrainstormNode node)
        {
            // H1 제목 읽기
            var h1 = "";
            try
            {
                var firstLine = File.ReadLines(node.FilePath)
                    .FirstOrDefault(l => l.StartsWith("# "));
                if (firstLine != null)
                    h1 = firstLine.Substring(2).Trim();
            }
            catch { }

            var border = new Border
            {
                Width = 180,
                Height = string.IsNullOrEmpty(h1) ? 60 : 80,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(8),
                Cursor = Cursors.Hand,
                Padding = new Thickness(8),
                Tag = node
            };

            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var nameText = new TextBlock
            {
                Text = node.FileName,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(97, 93, 89)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0)
            };

            stack.Children.Add(nameText);

            if (!string.IsNullOrEmpty(h1))
            {
                var h1Text = new TextBlock
                {
                    Text = h1,
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(23, 23, 23)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.NoWrap,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                stack.Children.Add(h1Text);
            }

            border.Child = stack;
            border.MouseLeftButtonDown += Node_MouseLeftButtonDown;
            border.MouseLeftButtonUp += Node_MouseLeftButtonUp;
            border.MouseMove += Node_MouseMove;

            Canvas.SetLeft(border, node.X);
            Canvas.SetTop(border, node.Y);
            Canvas.SetZIndex(border, 10);
            MainCanvas.Children.Add(border);
            node.Control = border;
        }

        private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is BrainstormNode node)
            {
                _draggingNode = node;
                _hasDragged = false;
                _dragStartPos = e.GetPosition(MainCanvas);
                _dragOffset = e.GetPosition(border);
                border.CaptureMouse();
                e.Handled = true;
            }
        }

        private void Node_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is BrainstormNode node)
            {
                border.ReleaseMouseCapture();
                _draggingNode = null;

                if (!_hasDragged)
                    HandleNodeClick(node);

                _hasDragged = false;
                e.Handled = true;
            }
        }

        private void Node_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggingNode != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(MainCanvas);
                if (!_hasDragged)
                {
                    var dist = Math.Sqrt(Math.Pow(pos.X - _dragStartPos.X, 2) + Math.Pow(pos.Y - _dragStartPos.Y, 2));
                    if (dist < 5) return;
                    _hasDragged = true;
                }

                var newX = pos.X - _dragOffset.X;
                var newY = pos.Y - _dragOffset.Y;
                _draggingNode.X = Math.Max(0, Math.Min(newX, MainCanvas.Width - 180));
                _draggingNode.Y = Math.Max(0, Math.Min(newY, MainCanvas.Height - 80));

                Canvas.SetLeft(_draggingNode.Control!, _draggingNode.X);
                Canvas.SetTop(_draggingNode.Control!, _draggingNode.Y);
                UpdateConnections();
            }
        }

        private void HandleNodeClick(BrainstormNode node)
        {
            if (_selectedNode == null)
            {
                // 첫 번째 노드 선택
                _selectedNode = node;
                node.Control!.BorderBrush = new SolidColorBrush(Color.FromRgb(35, 131, 226));
                node.Control!.BorderThickness = new Thickness(2.5);
            }
            else if (_selectedNode == node)
            {
                // 같은 노드 클릭 시 선택 해제
                DeselectNode();
            }
            else
            {
                // 두 번째 노드 클릭 시 연결
                var alreadyConnected = _connections.Any(c =>
                    (c.FromId == _selectedNode.Id && c.ToId == node.Id) ||
                    (c.FromId == node.Id && c.ToId == _selectedNode.Id));

                if (!alreadyConnected)
                    AddConnection(_selectedNode, node);

                DeselectNode();
            }
        }

        private void DeselectNode()
        {
            if (_selectedNode != null)
            {
                _selectedNode.Control!.BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200));
                _selectedNode.Control!.BorderThickness = new Thickness(1.5);
                _selectedNode = null;
            }
        }

        private void AddConnection(BrainstormNode from, BrainstormNode to)
        {
            var line = new Line
            {
                Stroke = new SolidColorBrush(Color.FromRgb(35, 131, 226)),
                StrokeThickness = 2,
                IsHitTestVisible = true,
                Cursor = Cursors.Hand
            };

            var arrow = new Polygon
            {
                Fill = new SolidColorBrush(Color.FromRgb(35, 131, 226)),
                IsHitTestVisible = true,
                Cursor = Cursors.Hand
            };

            var conn = new BrainstormConnection
            {
                FromId = from.Id,
                ToId = to.Id,
                Line = line,
                Arrow = arrow
            };

            // 호버하면 빨간색, 클릭하면 삭제
            line.MouseEnter += (s, e) => { line.Stroke = Brushes.Red; arrow.Fill = Brushes.Red; };
            line.MouseLeave += (s, e) =>
            {
                line.Stroke = new SolidColorBrush(Color.FromRgb(35, 131, 226));
                arrow.Fill = new SolidColorBrush(Color.FromRgb(35, 131, 226));
            };
            line.MouseLeftButtonDown += (s, e) => { RemoveConnection(conn); e.Handled = true; };

            arrow.MouseEnter += (s, e) => { line.Stroke = Brushes.Red; arrow.Fill = Brushes.Red; };
            arrow.MouseLeave += (s, e) =>
            {
                line.Stroke = new SolidColorBrush(Color.FromRgb(35, 131, 226));
                arrow.Fill = new SolidColorBrush(Color.FromRgb(35, 131, 226));
            };
            arrow.MouseLeftButtonDown += (s, e) => { RemoveConnection(conn); e.Handled = true; };

            Canvas.SetZIndex(line, 3);
            Canvas.SetZIndex(arrow, 4);
            MainCanvas.Children.Add(line);
            MainCanvas.Children.Add(arrow);

            _connections.Add(conn);
            UpdateConnection(conn);
        }

        private void RemoveConnection(BrainstormConnection conn)
        {
            MainCanvas.Children.Remove(conn.Line);
            MainCanvas.Children.Remove(conn.Arrow);
            _connections.Remove(conn);
        }

        private void UpdateConnections()
        {
            foreach (var conn in _connections)
                UpdateConnection(conn);
        }

        private void UpdateConnection(BrainstormConnection conn)
        {
            var from = _nodes.FirstOrDefault(n => n.Id == conn.FromId);
            var to = _nodes.FirstOrDefault(n => n.Id == conn.ToId);
            if (from == null || to == null) return;

            var fromPt = GetBorderPoint(from, to.X + 90, to.Y + 40);
            var toPt = GetBorderPoint(to, from.X + 90, from.Y + 40);

            if (conn.Line != null)
            {
                conn.Line.X1 = fromPt.X; conn.Line.Y1 = fromPt.Y;
                conn.Line.X2 = toPt.X; conn.Line.Y2 = toPt.Y;
            }

            if (conn.Arrow != null)
            {
                double angle = Math.Atan2(toPt.Y - fromPt.Y, toPt.X - fromPt.X);
                double size = 10;
                conn.Arrow.Points = new PointCollection
                {
                    new Point(toPt.X, toPt.Y),
                    new Point(toPt.X - Math.Cos(angle) * size * 1.5 - Math.Sin(angle) * size * 0.6,
                              toPt.Y - Math.Sin(angle) * size * 1.5 + Math.Cos(angle) * size * 0.6),
                    new Point(toPt.X - Math.Cos(angle) * size * 1.5 + Math.Sin(angle) * size * 0.6,
                              toPt.Y - Math.Sin(angle) * size * 1.5 - Math.Cos(angle) * size * 0.6)
                };
            }
        }

        private Point GetBorderPoint(BrainstormNode node, double targetX, double targetY)
        {
            double cx = node.X + 90;
            double cy = node.Y + (node.Control?.Height ?? 60) / 2;
            double hw = 90;
            double hh = (node.Control?.Height ?? 60) / 2;
            double dx = targetX - cx;
            double dy = targetY - cy;
            if (dx == 0 && dy == 0) return new Point(cx, cy);
            double scaleX = dx != 0 ? hw / Math.Abs(dx) : double.MaxValue;
            double scaleY = dy != 0 ? hh / Math.Abs(dy) : double.MaxValue;
            double scale = Math.Min(scaleX, scaleY);
            return new Point(cx + dx * scale, cy + dy * scale);
        }

        private void CreateNode_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Markdown Files (*.md)|*.md",
                DefaultExt = ".md",
                InitialDirectory = _folderPath
            };

            if (dialog.ShowDialog() == true)
            {
                File.WriteAllText(dialog.FileName, "", System.Text.Encoding.UTF8);
                var node = new BrainstormNode
                {
                    FilePath = dialog.FileName,
                    FileName = Path.GetFileName(dialog.FileName),
                    X = 100,
                    Y = 100
                };
                CreateNodeControl(node);
                _nodes.Add(node);
                OnNodeCreated?.Invoke();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveLayout();
            MessageBox.Show("저장 완료!", "MarkFlow");
        }

        private void SaveLayout()
        {
            if (string.IsNullOrEmpty(_folderPath)) return;

            var data = new SaveData
            {
                Nodes = _nodes.Select(n => new NodeData
                {
                    Id = n.Id,
                    FilePath = n.FilePath,
                    X = n.X,
                    Y = n.Y
                }).ToList(),
                Connections = _connections.Select(c => new ConnectionData
                {
                    FromId = c.FromId,
                    ToId = c.ToId
                }).ToList()
            };

            File.WriteAllText(
                Path.Combine(_folderPath, ".brainstorm_layout"),
                JsonSerializer.Serialize(data));
        }

        private void ClearConnections_Click(object sender, RoutedEventArgs e)
        {
            foreach (var conn in _connections)
            {
                MainCanvas.Children.Remove(conn.Line);
                MainCanvas.Children.Remove(conn.Arrow);
            }
            _connections.Clear();
            DeselectNode();
        }

        private void ClearAll()
        {
            MainCanvas.Children.Clear();
            _nodes.Clear();
            _connections.Clear();
            _selectedNode = null;
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
    }
}