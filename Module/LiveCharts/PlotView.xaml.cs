using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Framework.Views
{
    public partial class PlotView : UserControl
    {

        public PlotView()
        {
            InitializeComponent();
        }
    }
  
}