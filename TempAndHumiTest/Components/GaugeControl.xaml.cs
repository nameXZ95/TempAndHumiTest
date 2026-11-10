using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TempAndHumiTest.Models
{
    public partial class GaugeControl : UserControl
    {
        // 依赖属性定义
        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register("Minimum", typeof(double), typeof(GaugeControl), new PropertyMetadata(0.0, OnPropertyChanged));
        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register("Maximum", typeof(double), typeof(GaugeControl), new PropertyMetadata(100.0, OnPropertyChanged));
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(double), typeof(GaugeControl), new PropertyMetadata(0.0, OnPropertyChanged));
        public static readonly DependencyProperty UnitProperty = DependencyProperty.Register("Unit", typeof(string), typeof(GaugeControl), new PropertyMetadata("", OnPropertyChanged));
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(GaugeControl), new PropertyMetadata("", OnPropertyChanged));
        public static readonly DependencyProperty ProgressBrushProperty = DependencyProperty.Register("ProgressBrush", typeof(Brush), typeof(GaugeControl), new PropertyMetadata(Brushes.DodgerBlue, OnPropertyChanged));
        public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register("TrackBrush", typeof(Brush), typeof(GaugeControl), new PropertyMetadata(Brushes.LightGray, OnPropertyChanged));
        public static readonly DependencyProperty TickStepProperty = DependencyProperty.Register("TickStep", typeof(double), typeof(GaugeControl), new PropertyMetadata(10.0, OnPropertyChanged));

        public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
        public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
        public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
        public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
        public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        public Brush ProgressBrush { get => (Brush)GetValue(ProgressBrushProperty); set => SetValue(ProgressBrushProperty, value); }
        public Brush TrackBrush { get => (Brush)GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

        /// <summary>
        /// 主刻度的步进，比如温度是 10，湿度是 10
        /// </summary>
        public double TickStep { get => (double)GetValue(TickStepProperty); set => SetValue(TickStepProperty, value); }

        public GaugeControl()
        {
            InitializeComponent();
        }

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GaugeControl gauge)
            {
                gauge.DrawGauge();
            }
        }

        private void MainCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawGauge();
        }

        private void DrawGauge()
        {
            if (MainCanvas.ActualWidth == 0 || MainCanvas.ActualHeight == 0) return;

            MainCanvas.Children.Clear();

            // 基础参数计算
            double width = MainCanvas.ActualWidth;
            double height = MainCanvas.ActualHeight;
            double radius = Math.Min(width, height) / 2 - 15; // 留出边距
            if (radius <= 8) return; // 尺寸太小，画出来只是一团重叠

            Point center = new Point(width / 2, height / 2);

            // 弧度范围：从 135度(左下) 到 405度(右下) = 270度
            double startAngle = 135;
            double sweepAngle = 270;

            // 1. 绘制背景底弧
            DrawArc(center, radius, startAngle, startAngle + sweepAngle, TrackBrush, 12, false);

            // 2. 绘制进度弧
            double val = Math.Max(Minimum, Math.Min(Value, Maximum)); // 限制范围
            double ratio = (val - Minimum) / (Maximum - Minimum);
            double currentSweep = ratio * sweepAngle;

            if (currentSweep > 0)
            {
                // 进度弧两头是圆的，所以线条宽度稍微加粗一点点
                DrawArc(center, radius, startAngle, startAngle + currentSweep, ProgressBrush, 6, true);
            }

            // 3. 绘制刻度和文本
            DrawTicksAndLabels(center, radius);
        }

        private void DrawTicksAndLabels(Point center, double radius)
        {
            // 计算需要画多少个主刻度
            double range = Maximum - Minimum;
            if (range <= 0) return;

            // 用局部变量算步长，不要写 TickStep：
            // TickStep 是依赖属性，在它的变更回调里再赋值会重新触发 DrawGauge，形成重入。
            double step = TickStep;
            if (step <= 0 || double.IsNaN(step)) step = 10; // 默认防错

            // 动态计算步长，避免刻度太密集或太稀疏
            if (range / step > 15) step = range / 10; // 如果刻度多于15个，强制分成10份

            // 用整数下标推刻度值，避免 val += step 的浮点漂移（会多画或漏画最后一格）
            int tickCount = (int)Math.Floor(range / step);
            for (int i = 0; i <= tickCount; i++)
            {
                double val = Minimum + i * step;
                double ratio = (val - Minimum) / (Maximum - Minimum);
                double angle = 135 + ratio * 270;
                double rad = angle * Math.PI / 180;

                // 刻度线（从外向内）
                double outerRadius = radius - 2;
                double innerRadius = radius - 10;

                Point p1 = new Point(center.X + outerRadius * Math.Cos(rad), center.Y + outerRadius * Math.Sin(rad));
                Point p2 = new Point(center.X + innerRadius * Math.Cos(rad), center.Y + innerRadius * Math.Sin(rad));

                Line line = new Line
                {
                    X1 = p1.X,
                    Y1 = p1.Y,
                    X2 = p2.X,
                    Y2 = p2.Y,
                    Stroke = TrackBrush,
                    StrokeThickness = 1.5,
                    SnapsToDevicePixels = true
                };
                MainCanvas.Children.Add(line);

                // 刻度数值文本
                double textRadius = radius - 25;
                Point pt = new Point(center.X + textRadius * Math.Cos(rad), center.Y + textRadius * Math.Sin(rad));

                TextBlock txt = new TextBlock
                {
                    Text = val.ToString("0"),
                    FontSize = 11,
                    Foreground = Brushes.Gray
                };

                // 测量文本大小，使其居中对齐在极坐标点上
                txt.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(txt, pt.X - txt.DesiredSize.Width / 2);
                Canvas.SetTop(txt, pt.Y - txt.DesiredSize.Height / 2);
                MainCanvas.Children.Add(txt);
            }
        }

        private void DrawArc(Point center, double radius, double startAngle, double endAngle, Brush stroke, double thickness, bool roundCap)
        {
            // 如果是完整的圆，稍微偏移一点避免 WPF 渲染问题。
            // 必须在算端点坐标之前修正，否则端点仍按 360° 计算，圆弧会绕回起点。
            if (Math.Abs(endAngle - startAngle) >= 360) endAngle = startAngle + 359.99;

            // 处理极坐标到直角坐标的转换
            double rad1 = startAngle * Math.PI / 180;
            double rad2 = endAngle * Math.PI / 180;

            Point startPoint = new Point(center.X + radius * Math.Cos(rad1), center.Y + radius * Math.Sin(rad1));
            Point endPoint = new Point(center.X + radius * Math.Cos(rad2), center.Y + radius * Math.Sin(rad2));

            bool isLargeArc = (endAngle - startAngle) > 180;

            PathFigure figure = new PathFigure { StartPoint = startPoint };
            figure.Segments.Add(new ArcSegment
            {
                Point = endPoint,
                Size = new Size(radius, radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = isLargeArc
            });

            PathGeometry geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            Path path = new Path
            {
                Stroke = stroke,
                StrokeThickness = thickness,
                Data = geometry,
                // 设置两端圆角，看起来更现代
                StrokeStartLineCap = roundCap ? PenLineCap.Round : PenLineCap.Flat,
                StrokeEndLineCap = roundCap ? PenLineCap.Round : PenLineCap.Flat
            };

            MainCanvas.Children.Add(path);
        }
    }
}
