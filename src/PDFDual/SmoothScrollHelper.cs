using System;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PDFDual
{
    public class SmoothScrollHelper
    {
        private readonly ScrollViewer _scrollViewer;
        private double _targetOffset;
        private bool _isAnimating;

        private SmoothScrollHelper(ScrollViewer scrollViewer)
        {
            _scrollViewer = scrollViewer;
            _targetOffset = scrollViewer.VerticalOffset;

            _scrollViewer.PreviewMouseWheel += OnPreviewMouseWheel;
            _scrollViewer.ScrollChanged += OnScrollChanged;
        }

        public static void Register(ScrollViewer scrollViewer)
        {
            new SmoothScrollHelper(scrollViewer);
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            double delta = -e.Delta * 0.75;
            double max = _scrollViewer.ScrollableHeight;
            _targetOffset = Math.Clamp(_targetOffset + delta, 0, max);

            if (!_isAnimating)
            {
                _isAnimating = true;
                CompositionTarget.Rendering += OnRendering;
            }
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0 && Math.Abs(_scrollViewer.VerticalOffset - _targetOffset) > 80 && !_isAnimating)
            {
                _targetOffset = _scrollViewer.VerticalOffset;
            }
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            double current = _scrollViewer.VerticalOffset;
            double diff = _targetOffset - current;

            if (Math.Abs(diff) < 0.5)
            {
                _scrollViewer.ScrollToVerticalOffset(_targetOffset);
                _isAnimating = false;
                CompositionTarget.Rendering -= OnRendering;
                return;
            }

            double step = diff * 0.32;
            _scrollViewer.ScrollToVerticalOffset(current + step);
        }
    }
}

