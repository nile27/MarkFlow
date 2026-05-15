using System.Windows;
using MarkFlow.ViewModels;
using MarkFlow.Views;
using Application = System.Windows.Application;

namespace MarkFlow
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var mainWindow = new MainWindow
            {
                DataContext = new EditorViewModel()
            };
            mainWindow.Show();
        }
    }
}