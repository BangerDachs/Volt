using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ScottPlot;

namespace Volt
{
    /// <summary>
    /// Interaktionslogik für `FanCurve.xaml`.
    /// </summary>
    public partial class FanCurve : Window
    {
        // Standard-Punkte der Lüfterkurve (Temperatur -> Lüftergeschwindigkeit)
        private readonly List<Coordinates> _points =
        [
            new(0, 20),
            new(25, 35),
            new(50, 60),
            new(75, 80),
            new(100, 100)
        ];

        // Drag-Status für das Verschieben von Punkten
        private bool _isDragging;
        private int _dragIndex = -1;
        private int _pointsMaxCount = 10;

        // Gibt die aktuelle Kurve für den Aufrufer zurück
        public IReadOnlyList<Coordinates> Points => _points;

        public FanCurve(IEnumerable<Coordinates>? initialPoints = null)
        {
            InitializeComponent();

            Graph_FanCurve.UserInputProcessor.Disable();

            // Optional: übergebene Kurvenpunkte übernehmen
            if (initialPoints != null)
            {
                _points.Clear();
                _points.AddRange(initialPoints.OrderBy(p => p.X));
            }   

            // Event-Handler binden
            Loaded += FanCurve_Loaded;
            Graph_FanCurve.MouseDown += Graph_FanCurve_MouseDown;
            Graph_FanCurve.MouseMove += Graph_FanCurve_MouseMove;
            Graph_FanCurve.MouseUp += Graph_FanCurve_MouseUp;
            Graph_FanCurve.MouseLeave += Graph_FanCurve_MouseLeave;
            Graph_FanCurve.MouseDoubleClick += Graph_FanCurve_MouseDoubleClick;
        }

        private void FanCurve_Loaded(object sender, RoutedEventArgs e)
        {
            // Initiales Rendern
            
            RenderPlot();
        }

        private void RenderPlot()
        {
            // Plot leeren und neu zeichnen
            Graph_FanCurve.Plot.Clear();

            double[] xs = _points.Select(p => p.X).ToArray();
            double[] ys = _points.Select(p => p.Y).ToArray();

            var scatter = Graph_FanCurve.Plot.Add.Scatter(xs, ys);
            scatter.MarkerSize = 8;
            scatter.MarkerShape = MarkerShape.FilledCircle;
            scatter.LineWidth = 2;

            // Achsenformatierung
            Graph_FanCurve.Plot.Axes.SetLimits(0, 100, 0, 100);
            Graph_FanCurve.Plot.Axes.Bottom.Label.Text = "Temperatur [°C]";
            Graph_FanCurve.Plot.Axes.Left.Label.Text = "Lüfter [%]";
            // _point 0 linksbündig, _point n rechtsbündig fixieren
            

            Graph_FanCurve.Refresh();
        }

        private void Graph_FanCurve_MouseDown(object? sender, MouseButtonEventArgs e)
        {
            // Drag nur bei gedrückter linker Maustaste starten
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
            // Nur ziehen, wenn ein Punkt ausgewählt ist
            if (!_isDragging || _dragIndex < 0)
                return;

            Coordinates mouse = GetMouseCoordinates(e);
            Coordinates clamped = ClampToBounds(mouse, _dragIndex);
            _points[_dragIndex] = clamped;
            RenderPlot();
        }

        private Coordinates GetMouseCoordinates(MouseEventArgs e)
        {
            // Mausposition in Plot-Koordinaten umrechnen
            var p = e.GetPosition(Graph_FanCurve);
            var pixel = new Pixel(p.X, p.Y);
            return Graph_FanCurve.Plot.GetCoordinates(pixel);
        }

        private void Graph_FanCurve_MouseUp(object? sender, MouseButtonEventArgs e)
        {
            // Drag beenden
            StopDragging();
        }

        private void Graph_FanCurve_MouseLeave(object? sender, MouseEventArgs e)
        {
            // Drag beenden, wenn Maus den Plot verlässt
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
            // Nächstgelegenen Punkt zur Maus finden
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
            if (index <= 0)
                return new Coordinates(0, Math.Clamp(mouse.Y, 0, 100));

            if (index >= _points.Count - 1)
                return new Coordinates(100, Math.Clamp(mouse.Y, 0, 100));

            // X darf nicht an Nachbarpunkten vorbeiziehen
            double minX = _points[index - 1].X + 0.1;
            double maxX = _points[index + 1].X - 0.1;

            double x = Math.Clamp(mouse.X, minX, maxX);
            double y = Math.Clamp(mouse.Y, 0, 100);

            return new Coordinates(x, y);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            // Änderungen übernehmen
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Änderungen verwerfen
            DialogResult = false;
            Close();
        }

        private void Graph_FanCurve_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_points.Count >= _pointsMaxCount)
                return; // Maximale Anzahl an Punkten erreicht

            // Punkt an der geklickten Position hinzufügen
            Coordinates mouse = GetMouseCoordinates(e);
            int insertIndex = _points.FindIndex(p => p.X > mouse.X);
            if (insertIndex < 0)
                insertIndex = _points.Count;

            if (insertIndex <= 0 || insertIndex >= _points.Count)
                return; // Nur zwischen erstem und letztem Punkt einfügen

            double minX = _points[insertIndex - 1].X + 0.1;
            double maxX = _points[insertIndex].X - 0.1;

            if (minX > maxX)
                return; // Kein Platz zwischen den Punkten

            if (mouse.X < minX || mouse.X > maxX)
                return; // Ungültige Position, Punkt nicht hinzufügen

            double x = Math.Clamp(mouse.X, minX, maxX);
            double y = Math.Clamp(mouse.Y, 0, 100);

            _points.Insert(insertIndex, new Coordinates(x, y));
            RenderPlot();
        }

        private void Graph_FanCurve_KeyDown(object sender, KeyEventArgs e)
        {
            // wenn ein Punkt ausgewählt ist, kann er mit der Entf-Taste gelöscht werden
            if (e.Key == Key.Delete && _dragIndex > 0 && _dragIndex < _points.Count - 1 && _points.Count > 2)
            {
                _points.RemoveAt(_dragIndex);
                _dragIndex = -1;
                RenderPlot();
            }
        }
    }
}
