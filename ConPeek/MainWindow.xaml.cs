using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace ConPeek
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Console Console { get; set; } = new ();
        public MainWindow()
        {
            InitializeComponent();
            Console.Show();
            Close();
        }
    }
}