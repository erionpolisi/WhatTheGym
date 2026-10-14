using System.Diagnostics;

namespace Gym.CatalogCli;

internal interface IBrowserLauncher
{
    void Open(string url);
}

internal sealed class BrowserLauncher : IBrowserLauncher
{
    public void Open(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
