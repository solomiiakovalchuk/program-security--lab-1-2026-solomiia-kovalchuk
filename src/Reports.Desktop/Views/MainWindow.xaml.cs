using System;
using System.Windows;
using Wpf.Ui.Controls;

namespace Reports.Desktop.Views;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        SizeChanged += (_, _) =>
        {
            // FluentWindow подекуди вмикає підгін розміру під вміст — тримаємо ручний режим.
            if (SizeToContent != SizeToContent.Manual)
                SizeToContent = SizeToContent.Manual;
        };
    }
}
