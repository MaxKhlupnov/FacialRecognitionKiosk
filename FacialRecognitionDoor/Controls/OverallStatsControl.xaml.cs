using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using FacialRecognitionDoor.Objects;
using Windows.UI.Core;
using Windows.ApplicationModel.Core;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace FacialRecognitionDoor.Controls
{
    public sealed partial class OverallStatsControl : UserControl
    {
        public static readonly DependencyProperty HeaderTextProperty =
            DependencyProperty.Register(
            "HeaderText",
            typeof(string),
            typeof(OverallStatsControl),
            new PropertyMetadata("Total Faces")
            );

        public string HeaderText
        {
            get { return (string)GetValue(HeaderTextProperty); }
            set { SetValue(HeaderTextProperty, (string)value); }
        }

        public static readonly DependencyProperty SubHeaderTextProperty =
            DependencyProperty.Register(
            "SubHeaderText",
            typeof(string),
            typeof(OverallStatsControl),
            new PropertyMetadata("")
            );

        public string SubHeaderText
        {
            get { return (string)GetValue(SubHeaderTextProperty); }
            set { SetValue(SubHeaderTextProperty, (string)value); }
        }

        public static readonly DependencyProperty SubHeaderVisibilityProperty =
            DependencyProperty.Register(
            "SubHeaderVisibility",
            typeof(Visibility),
            typeof(OverallStatsControl),
            new PropertyMetadata(Visibility.Collapsed)
            );

        public Visibility SubHeaderVisibility
        {
            get { return (Visibility)GetValue(SubHeaderVisibilityProperty); }
            set { SetValue(SubHeaderVisibilityProperty, (Visibility)value); }
        }

        public OverallStatsControl()
        {
            this.InitializeComponent();            
        }

        public void UpdateData(DemographicsData data)
        {

            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
             {
                 this.facesProcessedTextBlock.Text = data.Visitors.Sum(v => v.Count).ToString();
                 this.uniqueFacesCountTextBlock.Text = data.Visitors.Count.ToString();
             });
        }
    }
}
