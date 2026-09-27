namespace ZaneTask.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // One window only: launching again just brings the open window to the front.
        using var singleInstance = new Mutex(initiallyOwned: true, @"Local\ZaneTask.Desktop", out var isFirst);
        using var activate = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\ZaneTask.Desktop.Activate");
        if (!isFirst)
        {
            activate.Set();
            return;
        }

        ApplicationConfiguration.Initialize();
        using var form = new MainForm();

        var registration = ThreadPool.RegisterWaitForSingleObject(activate, (_, _) =>
        {
            if (form.IsHandleCreated)
                form.BeginInvoke(form.BringToFrontFromAnotherLaunch);
        }, null, Timeout.Infinite, executeOnlyOnce: false);

        Application.Run(form);
        registration.Unregister(null);
    }
}
