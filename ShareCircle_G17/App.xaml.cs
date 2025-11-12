using Microsoft.Maui.Controls;

namespace ShareCircle_G17
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Ensure the Shell is the application's MainPage
            MainPage = new AppShell();
        }
    }
}
