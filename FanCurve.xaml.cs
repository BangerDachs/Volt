using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ScottPlot;

namespace Volt
{
    /// <summary>
    /// Interaktionslogik für FanCurve.xaml
    /// </summary>
    public partial class FanCurve : Window
    {
        private readonly List<Coordinates> _points =
        [
            new(0, 20),
            new(25, 35),
            new(50, 60),
            new(75, 80),
            new(100, 100)
        ];

        private bool _isDragging;
        private int _dragIndex = -1;

        public IReadOnlyList<Coordinates> Points => _points;

        public FanCurve(IEnumerable<Coordinates>? initialPoints = null)
        {
            InitializeComponent();

            if (initialPoints != null)
            {
                _points.Clear();
                _points.AddRange(initialPoints.OrderBy(p => p.X));
            }

            Loaded += FanCurve_Loaded;
            Graph_FanCurve.MouseDown += Graph_FanCurve_MouseDown;
            Graph_FanCurve.MouseMove += Graph_FanCurve_MouseMove;
            Graph_FanCurve.MouseUp += Graph_FanCurve_MouseUp;
            Graph_FanCurve.MouseLeave += Graph_FanCurve_MouseLeave;
        }

        private void FanCurve_Loaded(object sender, RoutedEventArgs e)
        {
            RenderPlot();
        }

        private void RenderPlot()
        {
            Graph_FanCurve.Plot.Clear();

            double[] xs = _points.Select(p => p.X).ToArray();
            double[] ys = _points.Select(p => p.Y).ToArray();

            var scatter = Graph_FanCurve.Plot.Add.Scatter(xs, ys);
            scatter.MarkerSize = 8;
            scatter.MarkerShape = MarkerShape.FilledCircle;
            scatter.LineWidth = 2;

            Graph_FanCurve.Plot.Axes.SetLimits(0, 100, 0, 100);
            Graph_FanCurve.Plot.Axes.Bottom.Label.Text = "Temperatur [°C]";
            Graph_FanCurve.Plot.Axes.Left.Label.Text = "Lüfter [%]";

            Graph_FanCurve.Refresh();
        }

        private void Graph_FanCurve_MouseDown(object? sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            Coordinates mouse = GetMouseCoordinates(e);
            _dragIndex = FindNearestPoint(mouse, 5);
            _isDragging = _dragIndex >= 0;

            if (_isDragging)
                Graph_FanCurve.CaptureMouse();
        }

        private void Graph_FanCurve_MouseMove(object? sender, MouseEventArgs e)
        {
            if (!_isDragging || _dragIndex < 0)
                return;

            Coordinates mouse = GetMouseCoordinates(e);
            Coordinates clamped = ClampToBounds(mouse, _dragIndex);
            _points[_dragIndex] = clamped;
            RenderPlot();
        }

        private Coordinates GetMouseCoordinates(MouseEventArgs e)
        {
            var p = e.GetPosition(Graph_FanCurve);
            var pixel = new Pixel(p.X, p.Y);
            return Graph_FanCurve.Plot.GetCoordinates(pixel);
        }

        private void Graph_FanCurve_MouseUp(object? sender, MouseButtonEventArgs e)
        {
            StopDragging();
        }

        private void Graph_FanCurve_MouseLeave(object? sender, MouseEventArgs e)
        {
            StopDragging();
        }

        private void StopDragging()
        {
            _isDragging = false;
            _dragIndex = -1;
            if (Graph_FanCurve.IsMouseCaptured)
                Graph_FanCurve.ReleaseMouseCapture();
        }

        private int FindNearestPoint(Coordinates mouse, double maxDistance)
        {
            int bestIndex = -1;
            double bestDistance = double.MaxValue;

            for (int i = 0; i < _points.Count; i++)
            {
                double dx = _points[i].X - mouse.X;
                double dy = _points[i].Y - mouse.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance < bestDistance && distance <= maxDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private Coordinates ClampToBounds(Coordinates mouse, int index)
        {
            double minX = index > 0 ? _points[index - 1].X + 0.1 : 0;
            double maxX = index < _points.Count - 1 ? _points[index + 1].X - 0.1 : 100;

            double x = Math.Clamp(mouse.X, minX, maxX);
            double y = Math.Clamp(mouse.Y, 0, 100);

            return new Coordinates(x, y);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
