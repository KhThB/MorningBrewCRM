namespace MorningBrew.Pos
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // [BUG-02] App POS chạy trên máy tính tại quầy: cửa sổ rộng.
            return new Window(new MainPage()) { Title = "Morning Brew POS", Width = 1366, Height = 860 };
        }
    }
}
