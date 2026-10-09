using System;
using Photino.Blazor;

namespace UI;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var appBuilder = PhotinoBlazorAppBuilder.CreateDefault(args);
        appBuilder.RootComponents.Add<App>("app");

        var app = appBuilder.Build();
        app.MainWindow
            .SetTitle("Photino Blazor App")
            .SetSize(1024, 768)
            .Center();

        AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
        {
            app.MainWindow.ShowMessage("Fatal exception", error.ExceptionObject.ToString());
        };

        app.Run();
    }
}