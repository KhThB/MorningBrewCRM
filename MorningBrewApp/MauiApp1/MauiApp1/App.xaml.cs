namespace MauiApp1
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Kích thước gần điện thoại khi chạy demo trên Windows.
            return new Window(new MainPage()) { Title = "Morning Brew", Width = 430, Height = 900 };
        }
    }
}
