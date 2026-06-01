namespace AnyDeck.Services
{
    public interface IAppActivator
    {
        void ActivateWindow(string? name);
        void SendKeys(string keys);
        void LaunchApp(string path, string? arguments = null);
    }
}
