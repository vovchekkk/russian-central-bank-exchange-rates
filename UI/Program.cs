using System;
using Core;
using Microsoft.Extensions.Configuration;
using Photino.Blazor;

namespace UI;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var builder = PhotinoBlazorAppBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("app");
        
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        
        builder.Services.AddCoreServices(configuration);

        var app = builder.Build();
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