﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Plot.cs" company="OxyPlot">
//   The MIT License (MIT)
//
//   Copyright (c) 2012 Oystein Bjorke
//
//   Permission is hereby granted, free of charge, to any person obtaining a
//   copy of this software and associated documentation files (the
//   "Software"), to deal in the Software without restriction, including
//   without limitation the rights to use, copy, modify, merge, publish,
//   distribute, sublicense, and/or sell copies of the Software, and to
//   permit persons to whom the Software is furnished to do so, subject to
//   the following conditions:
//
//   The above copyright notice and this permission notice shall be included
//   in all copies or substantial portions of the Software.
//
//   THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
//   OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
//   MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
//   IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
//   CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
//   TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
//   SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// </copyright>
// <summary>
//   The Metro Plot control.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
using Windows.ApplicationModel.DataTransfer;

namespace OxyPlot.Metro
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Media.Imaging;
    using Windows.Foundation;
    using Windows.System;

    /// <summary>
    /// The Metro Plot control.
    /// </summary>
    //[ContentProperty("Series")]
    [TemplatePart(Name = PartGrid, Type = typeof(Grid))]
    public partial class Plot : Control, IPlotControl
    {
        /// <summary>
        /// The Grid PART constant.
        /// </summary>
        private const string PartGrid = "PART_Grid";

        /// <summary>
        /// The model lock.
        /// </summary>
        private readonly object modelLock = new object();

        /// <summary>
        /// The tracker definitions.
        /// </summary>
        private readonly ObservableCollection<TrackerDefinition> trackerDefinitions;

        /// <summary>
        /// The canvas.
        /// </summary>
        private Canvas canvas;

        /// <summary>
        /// The current model.
        /// </summary>
        private PlotModel currentModel;

        /// <summary>
        /// The current tracker.
        /// </summary>
        private FrameworkElement currentTracker;

        /// <summary>
        /// The grid.
        /// </summary>
        private Grid grid;

        /// <summary>
        /// The internal model.
        /// </summary>
        private PlotModel internalModel;

        /// <summary>
        /// The is plot invalidated.
        /// </summary>
        private bool isPlotInvalidated;

        /// <summary>
        /// The mouse manipulator.
        /// </summary>
        private ManipulatorBase mouseManipulator;

        /// <summary>
        /// The overlays.
        /// </summary>
        private Canvas overlays;

        /// <summary>
        /// The touch down point.
        /// </summary>
        private Point touchDownPoint;

        /// <summary>
        /// The touch pan manipulator.
        /// </summary>
        private PanManipulator touchPan;

        /// <summary>
        /// The touch zoom manipulator.
        /// </summary>
        private ZoomManipulator touchZoom;

        /// <summary>
        /// Data has been updated.
        /// </summary>
        private bool updateData;

        /// <summary>
        /// The zoom control.
        /// </summary>
        private ContentControl zoomControl;

        /// <summary>
        /// Initializes a new instance of the <see cref = "Plot" /> class.
        /// </summary>
        public Plot()
        {
            this.DefaultStyleKey = typeof(Plot);

            this.trackerDefinitions = new ObservableCollection<TrackerDefinition>();
            this.Loaded += this.OnLoaded;
            this.SizeChanged += this.OnSizeChanged;
            CompositionTarget.Rendering += CompositionTargetRendering;
        }

        /// <summary>
        /// Gets the actual model.
        /// </summary>
        /// <value>The actual model.</value>
        public PlotModel ActualModel
        {
            get
            {
                return this.Model ?? this.internalModel;
            }
        }

        /// <summary>
        /// Gets the tracker definitions.
        /// </summary>
        /// <value>The tracker definitions.</value>
        public ObservableCollection<TrackerDefinition> TrackerDefinitions
        {
            get
            {
                return this.trackerDefinitions;
            }
        }

        /// <summary>
        /// Gets the axes from a point.
        /// </summary>
        /// <param name="pt">
        /// The point.
        /// </param>
        /// <param name="xaxis">
        /// The x-axis.
        /// </param>
        /// <param name="yaxis">
        /// The y-axis.
        /// </param>
        public void GetAxesFromPoint(ScreenPoint pt, out Axis xaxis, out Axis yaxis)
        {
            if (this.ActualModel != null)
            {
                this.ActualModel.GetAxesFromPoint(pt, out xaxis, out yaxis);
            }
            else
            {
                xaxis = null;
                yaxis = null;
            }
        }

        /// <summary>
        /// Gets the series that is nearest the specified point (in screen coordinates).
        /// </summary>
        /// <param name="pt">
        /// The point.
        /// </param>
        /// <param name="limit">
        /// The maximum distance, if this is exceeded the method will return null.
        /// </param>
        /// <returns>
        /// The closest DataSeries
        /// </returns>
        public Series GetSeriesFromPoint(ScreenPoint pt, double limit)
        {
            return this.ActualModel != null ? this.ActualModel.GetSeriesFromPoint(pt, limit) : null;
        }

        /// <summary>
        /// Hides the tracker.
        /// </summary>
        public void HideTracker()
        {
            if (this.currentTracker != null)
            {
                this.overlays.Children.Remove(this.currentTracker);
                this.currentTracker = null;
            }
        }

        /// <summary>
        /// Hides the zoom rectangle.
        /// </summary>
        public void HideZoomRectangle()
        {
            this.zoomControl.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Invalidate the plot (not blocking the UI thread)
        /// </summary>
        /// <param name="updateData">
        /// The update Data.
        /// </param>
        public void InvalidatePlot(bool updateData = true)
        {
            lock (this)
            {
                this.isPlotInvalidated = true;
                this.updateData = this.updateData || updateData;
            }
        }

        /// <summary>
        /// Invoked whenever application code or internal processes (such as a rebuilding layout pass) call ApplyTemplate. In simplest terms, this means the method is called just before a UI element displays in your app. Override this method to influence the default post-template logic of a class.
        /// </summary>
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            this.grid = this.GetTemplateChild(PartGrid) as Grid;
            if (this.grid == null)
            {
                return;
            }

            this.canvas = new Canvas();
            this.grid.Children.Add(this.canvas);
            this.canvas.UpdateLayout();

            this.overlays = new Canvas();
            this.grid.Children.Add(this.overlays);

            this.zoomControl = new ContentControl();
            this.overlays.Children.Add(this.zoomControl);
        }

        /// <summary>
        /// Pans the specified axis.
        /// </summary>
        /// <param name="axis">
        /// The axis.
        /// </param>
        /// <param name="ppt">
        /// The previous point (screen coordinates).
        /// </param>
        /// <param name="cpt">
        /// The current point (screen coordinates).
        /// </param>
        public void Pan(Axis axis, ScreenPoint ppt, ScreenPoint cpt)
        {
            axis.Pan(ppt, cpt);
            this.InvalidatePlot(false);
        }

        /// <summary>
        /// Refresh the plot immediately (not blocking UI thread)
        /// </summary>
        /// <param name="updateData">
        /// if set to <c>true</c>, the data collections will be updated.
        /// </param>
        public void RefreshPlot(bool updateData)
        {
            // don't block ui thread
            this.InvalidatePlot(updateData);
        }

        /// <summary>
        /// Resets the specified axis.
        /// </summary>
        /// <param name="axis">
        /// The axis.
        /// </param>
        public void Reset(Axis axis)
        {
            axis.Reset();
        }

        /// <summary>
        /// Reset all axes.
        /// </summary>
        public void ResetAllAxes()
        {
            foreach (Axis a in this.ActualModel.Axes)
            {
                a.Reset();
            }

            this.InvalidatePlot(false);
        }

        /// <summary>
        /// Saves the plot as a bitmap.
        /// </summary>
        /// <param name="fileName">
        /// Name of the file.
        /// </param>
        public void SaveBitmap(string fileName)
        {
            throw new NotImplementedException();

            // todo: Use imagetools.codeplex.com
            // or http://windowspresentationfoundation.com/Blend4Book/SaveTheImage.txt

            // var bmp = this.ToBitmap();

            // var encoder = new PngBitmapEncoder();
            // encoder.Frames.Add(BitmapFrame.Create(bmp));

            // using (FileStream s = File.Create(fileName))
            // {
            // encoder.Save(s);
            // }
        }

        /// <summary>
        /// Sets the cursor.
        /// </summary>
        /// <param name="cursor">
        /// The cursor.
        /// </param>
        public void SetCursorType(CursorType cursor)
        {
            //switch (cursor)
            //{
            //    case OxyCursor.Arrow:
            //        this.Cursor = Cursors.Arrow;
            //        break;
            //    case OxyCursor.Cross:
            //        this.Cursor = Cursors.Arrow;
            //        break;
            //    case OxyCursor.SizeAll:
            //        this.Cursor = Cursors.Arrow;
            //        break;
            //    case OxyCursor.SizeNWSE:
            //        this.Cursor = Cursors.SizeNWSE;
            //        break;
            //    case OxyCursor.None:
            //        this.Cursor = Cursors.None;
            //        break;
            //}
        }

        /// <summary>
        /// Shows the tracker.
        /// </summary>
        /// <param name="trackerHitResult">
        /// The tracker data.
        /// </param>
        public void ShowTracker(TrackerHitResult trackerHitResult)
        {
            if (trackerHitResult != null)
            {
                var ts = trackerHitResult.Series as ITrackableSeries;
                ControlTemplate trackerTemplate = this.DefaultTrackerTemplate;
                if (ts != null && !string.IsNullOrEmpty(ts.TrackerKey))
                {
                    TrackerDefinition match = this.TrackerDefinitions.FirstOrDefault(t => t.TrackerKey == ts.TrackerKey);
                    if (match != null)
                    {
                        trackerTemplate = match.TrackerTemplate;
                    }
                }

                var tracker = new ContentControl { Template = trackerTemplate };

                if (tracker != this.currentTracker)
                {
                    this.HideTracker();
                    if (trackerTemplate != null)
                    {
                        this.overlays.Children.Add(tracker);
                        this.currentTracker = tracker;
                    }
                }

                if (this.currentTracker != null)
                {
                    this.currentTracker.DataContext = trackerHitResult;
                }
            }
            else
            {
                this.HideTracker();
            }
        }

        /// <summary>
        /// Shows the zoom rectangle.
        /// </summary>
        /// <param name="r">
        /// The rectangle.
        /// </param>
        public void ShowZoomRectangle(OxyRect r)
        {
            this.zoomControl.Width = r.Width;
            this.zoomControl.Height = r.Height;
            Canvas.SetLeft(this.zoomControl, r.Left);
            Canvas.SetTop(this.zoomControl, r.Top);
            this.zoomControl.Template = this.ZoomRectangleTemplate;
            this.zoomControl.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Renders the plot to a bitmap.
        /// </summary>
        /// <returns>
        /// A bitmap.
        /// </returns>
        public WriteableBitmap ToBitmap()
        {
            throw new NotImplementedException();

            // var bmp = new RenderTargetBitmap(
            // (int)this.ActualWidth, (int)this.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            // bmp.Render(this);
            // return bmp;
        }

        /// <summary>
        /// Renders the plot to xaml.
        /// </summary>
        /// <returns>
        /// The to xaml.
        /// </returns>
        public string ToXaml()
        {
            throw new NotImplementedException();

            // var sb = new StringBuilder();
            // var tw = new StringWriter(sb);
            // XmlWriter xw = XmlWriter.Create(tw, new XmlWriterSettings { Indent = true });
            // if (this.canvas != null)
            // {
            // XamlWriter.Save(this.canvas, xw);
            // }

            // xw.Close();
            // return sb.ToString();
        }

        /// <summary>
        /// Zooms the specified axis to the specified values.
        /// </summary>
        /// <param name="axis">
        /// The axis.
        /// </param>
        /// <param name="p1">
        /// The new minimum value.
        /// </param>
        /// <param name="p2">
        /// The new maximum value.
        /// </param>
        public void Zoom(Axis axis, double p1, double p2)
        {
            axis.Zoom(p1, p2);
            this.RefreshPlot(false);
        }

        /// <summary>
        /// Zooms all axes.
        /// </summary>
        /// <param name="delta">
        /// The delta.
        /// </param>
        public void ZoomAllAxes(double delta)
        {
            foreach (var a in this.ActualModel.Axes)
            {
                this.ZoomAt(a, delta);
            }

            this.RefreshPlot(false);
        }

        /// <summary>
        /// Zooms at the specified position.
        /// </summary>
        /// <param name="axis">
        /// The axis.
        /// </param>
        /// <param name="factor">
        /// The zoom factor.
        /// </param>
        /// <param name="x">
        /// The position to zoom at.
        /// </param>
        public void ZoomAt(Axis axis, double factor, double x = double.NaN)
        {
            if (double.IsNaN(x))
            {
                double sx = (axis.Transform(axis.ActualMaximum) + axis.Transform(axis.ActualMinimum)) * 0.5;
                x = axis.InverseTransform(sx);
            }

            axis.ZoomAt(factor, x);
        }

        /// <summary>
        /// Called before the <see cref="E:Windows.UI.Xaml.UIElement.KeyDown"/> event occurs.
        /// </summary>
        /// <param name="e">
        /// The data for the event.
        /// </param>
        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            bool control = IsControlDown();
            bool alt = IsAltDown();

            switch (e.Key)
            {
                case VirtualKey.Home:
                case VirtualKey.A:
                    this.ResetAllAxes();
                    e.Handled = true;
                    break;
            }

            double dx = 0;
            double dy = 0;
            double zoom = 0;
            switch (e.Key)
            {
                case VirtualKey.Up:
                    dy = -1;
                    break;
                case VirtualKey.Down:
                    dy = 1;
                    break;
                case VirtualKey.Left:
                    dx = -1;
                    break;
                case VirtualKey.Right:
                    dx = 1;
                    break;
                case VirtualKey.Add:
                case VirtualKey.PageUp:
                    zoom = 1;
                    break;
                case VirtualKey.Subtract:
                case VirtualKey.PageDown:
                    zoom = -1;
                    break;
            }

            if (dx != 0 || dy != 0)
            {
                dx = dx * this.ActualModel.PlotArea.Width * this.KeyboardPanHorizontalStep;
                dy = dy * this.ActualModel.PlotArea.Height * this.KeyboardPanVerticalStep;

                // small steps if the user is pressing control
                if (control)
                {
                    dx *= 0.2;
                    dy *= 0.2;
                }

                this.PanAll(dx, dy);
                e.Handled = true;
            }

            if (zoom != 0)
            {
                if (control)
                {
                    zoom *= 0.2;
                }

                this.ZoomAllAxes(1 + zoom * 0.12);
                e.Handled = true;
            }

            if (e.Key == VirtualKey.C && control)
            {
                e.Handled = true;

                // todo: Clipboard does not currently support copying image data
            }

            if (control && alt && this.ActualModel != null)
            {
                switch (e.Key)
                {
                    case VirtualKey.R:
                        {
                            var pkg = new DataPackage();
                        // TODO    pkg.SetText(this.ActualModel.CreateTextReport());
                            Clipboard.SetContent(pkg);
                            break;
                        }
                    case VirtualKey.C:
                        {
                            var pkg = new DataPackage();
                            pkg.SetText(this.ActualModel.ToCode());
                            Clipboard.SetContent(pkg);
                            break;
                        }
                    case VirtualKey.X:
                        {
                            var pkg = new DataPackage();
                            pkg.SetText(this.ToXaml());
                            Clipboard.SetContent(pkg);
                            break;
                        }
                }
            }

            base.OnKeyDown(e);
        }

        /// <summary>
        /// Called when the <see cref="E:Windows.UI.Xaml.UIElement.ManipulationCompleted"/> event occurs.
        /// </summary>
        /// <param name="e">
        /// The data for the event.
        /// </param>
        protected override void OnManipulationCompleted(ManipulationCompletedRoutedEventArgs e)
        {
            base.OnManipulationCompleted(e);
            var position = this.Add(this.touchDownPoint, e.Cumulative.Translation);
            this.touchPan.Completed(new ManipulationEventArgs(position.ToScreenPoint()));
            e.Handled = true;
        }

        /// <summary>
        /// Called when the <see cref="E:Windows.UI.Xaml.UIElement.ManipulationDelta"/> event occurs.
        /// </summary>
        /// <param name="e">
        /// The data for the event.
        /// </param>
        protected override void OnManipulationDelta(ManipulationDeltaRoutedEventArgs e)
        {
            base.OnManipulationDelta(e);
            var position = this.Add(this.touchDownPoint, e.Cumulative.Translation);
            this.touchPan.Delta(new ManipulationEventArgs(position.ToScreenPoint()));
            // todo: enabled pinch-zoom again!
            //this.touchZoom.Delta(
            //    new ManipulationEventArgs(position.ToScreenPoint())
            //        {
            //            ScaleX = e.DeltaManipulation.Scale,
            //            ScaleY = e.DeltaManipulation.Scale
            //        });
            e.Handled = true;
        }

        /// <summary>
        /// Called when the <see cref="E:Windows.UI.Xaml.UIElement.ManipulationStarted"/> event occurs.
        /// </summary>
        /// <param name="e">
        /// The data for the event.
        /// </param>
        protected override void OnManipulationStarted(ManipulationStartedRoutedEventArgs e)
        {
            base.OnManipulationStarted(e);
            this.touchPan = new PanManipulator(this);
            this.touchPan.Started(new ManipulationEventArgs(e.Position.ToScreenPoint()));
            this.touchZoom = new ZoomManipulator(this);
            this.touchZoom.Started(new ManipulationEventArgs(e.Position.ToScreenPoint()));
            this.touchDownPoint = e.Position;
            e.Handled = true;

        }

        /// <summary>
        /// Raises the <see cref="E:MouseButtonUp"/> event.
        /// </summary>
        /// <param name="e">
        /// The <see cref="Windows.UI.Xaml.Input.MouseButtonEventArgs"/> instance containing the event data.
        /// </param>
        protected void OnMouseButtonUp(PointerRoutedEventArgs e)
        {
            if (this.mouseManipulator != null)
            {
                this.mouseManipulator.Completed(this.CreateManipulationEventArgs(e));
                e.Handled = true;
            }

            this.mouseManipulator = null;
            this.ReleasePointerCapture(e.Pointer);
        }

        /// <summary>
        /// Called before the <see cref="E:Windows.UI.Xaml.UIElement.MouseLeftButtonDown"/> event occurs.
        /// </summary>
        /// <param name="e">
        /// The data for the event.
        /// </param>
        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);
            OnMouseButtonDown(e);
            e.Handled = true;
        }

        /// <summary>
        /// Called before the <see cref="E:Windows.UI.Xaml.UIElement.MouseLeftButtonUp"/> event occurs.
        /// </summary>
        /// <param name="e">
        /// The data for the event.
        /// </param>
        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            base.OnPointerReleased(e);
            this.OnMouseButtonUp(e);
            e.Handled = true;
        }

        /// <summary>
        /// Called before the <see cref="E:Windows.UI.Xaml.UIElement.MouseMove"/> event occurs.
        /// </summary>
        /// <param name="e">
        /// The data for the event.
        /// </param>
        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            base.OnPointerMoved(e);
            if (this.mouseManipulator != null)
            {
                this.mouseManipulator.Delta(this.CreateManipulationEventArgs(e));
            }
        }

        /// <summary>
        /// Called before the <see cref="E:Windows.UI.Xaml.UIElement.MouseWheel"/> event occurs to provide handling for the event in a derived class without attaching a delegate.
        /// </summary>
        /// <param name="e">
        /// A <see cref="T:Windows.UI.Xaml.Input.MouseWheelEventArgs"/> that contains the event data.
        /// </param>
        protected override void OnPointerWheelChanged(PointerRoutedEventArgs e)
        {
            base.OnPointerWheelChanged(e);

            if (!this.IsMouseWheelEnabled)
            {
                return;
            }

            bool isControlDown = IsControlDown();

            //var m = new ZoomStepManipulator(this, e.Delta * 0.001, isControlDown);
            //m.Started(new ManipulationEventArgs(e.GetPosition(this).ToScreenPoint()));
        }

        /// <summary>
        /// Called when the DataContext is changed.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The <see cref="Windows.UI.Xaml.DependencyPropertyChangedEventArgs"/> instance containing the event data.
        /// </param>
        private static void DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ((Plot)sender).OnDataContextChanged(sender, e);
        }

        /// <summary>
        /// Determines whether the alt key is down.
        /// </summary>
        /// <returns>
        /// <c>true</c> if alt is down; otherwise, <c>false</c>.
        /// </returns>
        private static bool IsAltDown()
        {
            return false;
            //ModifierKeys keys = Keyboard.Modifiers;
            //return (keys & ModifierKeys.Alt) != 0;
        }

        /// <summary>
        /// Determines whether the control key is down.
        /// </summary>
        /// <returns>
        /// <c>true</c> if control is down; otherwise, <c>false</c>.
        /// </returns>
        private static bool IsControlDown()
        {
            return false;
            //ModifierKeys keys = Keyboard.Modifiers;
            //return (keys & ModifierKeys.Control) != 0;
        }

        /// <summary>
        /// Determines whether the shift key is down.
        /// </summary>
        /// <returns>
        /// <c>true</c> if shift is down; otherwise, <c>false</c>.
        /// </returns>
        private static bool IsShiftDown()
        {
            return false;
            //ModifierKeys keys = Keyboard.Modifiers;
            //return (keys & ModifierKeys.Shift) != 0;
        }

        /// <summary>
        /// Called when the Model is changed.
        /// </summary>
        /// <param name="d">
        /// The d.
        /// </param>
        /// <param name="e">
        /// The <see cref="Windows.UI.Xaml.DependencyPropertyChangedEventArgs"/> instance containing the event data.
        /// </param>
        private static void ModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((Plot)d).OnModelChanged();
        }

        /// <summary>
        /// Called when the visual appearance changed.
        /// </summary>
        /// <param name="d">
        /// The d.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private static void VisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((Plot)d).UpdateVisuals();
        }

        /// <summary>
        /// The add.
        /// </summary>
        /// <param name="p1">
        /// The p 1.
        /// </param>
        /// <param name="p2">
        /// The p 2.
        /// </param>
        /// <returns>
        /// </returns>
        private Point Add(Point p1, Point p2)
        {
            return new Point(p1.X + p2.X, p1.Y + p2.Y);
        }

        /// <summary>
        /// Called when the composition target rendering event occurs.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The event arguments.
        /// </param>
        void CompositionTargetRendering(object sender, object e)
        {
            lock (this)
            {
                if (this.isPlotInvalidated)
                {
                    this.isPlotInvalidated = false;
                    if (this.ActualWidth > 0 && this.ActualHeight > 0)
                    {
                        this.UpdateModel(this.updateData);
                        this.updateData = false;
                    }

                    this.UpdateVisuals();
                }
            }
        }

        /// <summary>
        /// Creates the manipulation event args.
        /// </summary>
        /// <param name="e">
        /// The <see cref="Windows.UI.Xaml.Input.MouseEventArgs"/> instance containing the event data.
        /// </param>
        /// <returns>
        /// A manipulation event args object.
        /// </returns>
        private ManipulationEventArgs CreateManipulationEventArgs(PointerRoutedEventArgs e)
        {
            return new ManipulationEventArgs(e.GetCurrentPoint(this).Position.ToScreenPoint());
        }

        /// <summary>
        /// The get manipulator.
        /// </summary>
        /// <param name="button">
        /// The button.
        /// </param>
        /// <param name="clickCount">
        /// The click Count.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        /// <returns>
        /// </returns>
        private ManipulatorBase GetManipulator(MouseButton button, int clickCount, PointerRoutedEventArgs e)
        {
            bool control = IsControlDown();
            bool shift = IsShiftDown();
            bool lmb = button == MouseButton.Left;
            bool rmb = button == MouseButton.Right;
            bool mmb = button == MouseButton.Middle;
            bool xb1 = button == MouseButton.XButton1;
            bool xb2 = button == MouseButton.XButton2;

            // MMB / control RMB
            if (mmb || (control && rmb))
            {
                if (clickCount == 2)
                {
                    return new ResetManipulator(this);
                }

                return new ZoomRectangleManipulator(this);
            }

            // Right mouse button
            if (rmb)
            {
                return new PanManipulator(this);
            }

            // Left mouse button
            if (lmb)
            {
                return new TrackerManipulator(this) { Snap = !control, PointsOnly = shift };
            }

            // XButtons are zoom-stepping
            if (xb1 || xb2)
            {
                double d = xb1 ? 0.05 : -0.05;
                return new ZoomStepManipulator(this, d, control);
            }

            return null;
        }

        /// <summary>
        /// Called when data context is changed.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The <see cref="Windows.UI.Xaml.DependencyPropertyChangedEventArgs"/> instance containing the event data.
        /// </param>
        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            this.OnModelChanged();
        }

        /// <summary>
        /// Called when the control is loaded.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The <see cref="Windows.UI.Xaml.RoutedEventArgs"/> instance containing the event data.
        /// </param>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.OnModelChanged();
        }

        /// <summary>
        /// Called when the model is changed.
        /// </summary>
        private void OnModelChanged()
        {
            lock (this.modelLock)
            {
                if (this.currentModel != null)
                {
                    this.currentModel.AttachPlotControl(null);
                }

                if (this.Model != null)
                {
                    if (this.Model.PlotControl != null)
                    {
                        throw new InvalidOperationException(
                            "This PlotModel is already in use by some other plot control.");
                    }

                    this.Model.AttachPlotControl(this);
                    this.currentModel = this.Model;
                }
            }

            this.InvalidatePlot();
        }

        /// <summary>
        /// Called when a mouse button is pressed down.
        /// </summary>
        /// <param name="button">
        /// The button.
        /// </param>
        /// <param name="e">
        /// The <see cref="Windows.UI.Xaml.Input.MouseButtonEventArgs"/> instance containing the event data.
        /// </param>
        private void OnMouseButtonDown(PointerRoutedEventArgs e)
        {
            if (this.mouseManipulator != null)
            {
                return;
            }

            this.Focus(FocusState.Pointer);
            this.CapturePointer(e.Pointer);

            int clickCount = 1;
            //if (MouseButtonHelper.IsDoubleClick(this, e))
            //{
            //    clickCount = 2;
            //}

            this.mouseManipulator = this.GetManipulator(MouseButton.Left, clickCount, e);

            if (this.mouseManipulator != null)
            {
                this.mouseManipulator.Started(this.CreateManipulationEventArgs(e));
                e.Handled = true;
            }
        }

        /// <summary>
        /// Called when the size of the control is changed.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The <see cref="Windows.UI.Xaml.SizeChangedEventArgs"/> instance containing the event data.
        /// </param>
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.InvalidatePlot();
        }

        /// <summary>
        /// Pans all axes.
        /// </summary>
        /// <param name="dx">
        /// The dx.
        /// </param>
        /// <param name="dy">
        /// The dy.
        /// </param>
        private void PanAll(double dx, double dy)
        {
            foreach (Axis a in this.ActualModel.Axes)
            {
                a.Pan(a.IsHorizontal() ? dx : dy);
            }

            this.RefreshPlot(false);
        }

        /// <summary>
        /// Synchronize properties between the control and the internal PlotModel (only if Model is undefined).
        /// </summary>
        private void SynchronizeProperties()
        {
        }

        /// <summary>
        /// Updates the model.
        /// </summary>
        /// <param name="updateData">
        /// The update Data.
        /// </param>
        private void UpdateModel(bool updateData)
        {
            this.internalModel = this.Model;

            if (this.ActualModel != null)
            {
                this.ActualModel.Update(updateData);
            }
        }

        /// <summary>
        /// Updates the visuals.
        /// </summary>
        private void UpdateVisuals()
        {
            if (this.canvas == null)
            {
                return;
            }

            // Clear the canvas
            this.canvas.Children.Clear();

            if (this.ActualModel != null)
            {
                this.SynchronizeProperties();

                var wrc = new MetroRenderContext(this.canvas);
                this.ActualModel.Render(wrc);
            }
        }

    }

    /// <summary>
    /// The mouse button.
    /// </summary>
    public enum MouseButton
    {
        /// <summary>
        /// The left.
        /// </summary>
        Left,

        /// <summary>
        /// The middle.
        /// </summary>
        Middle,

        /// <summary>
        /// The right.
        /// </summary>
        Right,

        /// <summary>
        /// The x button 1.
        /// </summary>
        XButton1,

        /// <summary>
        /// The x button 2.
        /// </summary>
        XButton2
    }
}