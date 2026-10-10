using System;
using Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Photino.Blazor;
using Serilog;

namespace UI;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                path: Path.Combine(AppContext.BaseDirectory, "logs", "app-.log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            Log.Information("Запуск приложения");
            var builder = PhotinoBlazorAppBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("app");
            
            builder.Services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddSerilog(dispose: true);
                logging.AddFilter("Microsoft", LogLevel.Warning);
                logging.AddFilter("System", LogLevel.Warning);
            });
            
            IConfiguration configuration;
            string? configErrorMessage = null;

            try
            {
                configuration = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("config.json", optional: false, reloadOnChange: true)
                    .Build();
            }
            catch (FileNotFoundException ex)
            {
                Log.Error(ex, "Конфигурационный файл config.json не найден");
                configErrorMessage = "Конфигурационный файл config.json не найден.";
                configuration = new ConfigurationBuilder().Build();
            }
            catch (Exception ex) when (ex is InvalidDataException or FormatException)
            {
                Log.Error(ex, "Ошибка чтения конфигурационного файла config.json");
                configErrorMessage = "Конфигурационный файл config.json повреждён или содержит некорректный JSON.";
                configuration = new ConfigurationBuilder().Build();
            }

            builder.Services.AddCoreServices(configuration);

            var app = builder.Build();
            app.MainWindow
                .SetTitle("Photino Blazor App")
                .SetSize(1024, 768)
                .Center();

            if (configErrorMessage is not null)
            {
                app.MainWindow.RegisterWindowCreatedHandler((_, _) =>
                {
                    app.MainWindow.ShowMessage("Ошибка конфигурации", configErrorMessage);
                });
            }
            
            AppDomain.CurrentDomain.UnhandledException += (_, error) =>
            {
                var ex = error.ExceptionObject as Exception;
                Log.Fatal(ex, "Неперехваченное исключение в AppDomain");
                Log.CloseAndFlush();
                app.MainWindow.ShowMessage(
                    "Критическая ошибка",
                    ex?.Message ?? "Произошла критическая ошибка приложения."
                );
            };

            app.Run();
            Log.Information("Завершение работы приложения");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Критическая ошибка при работе приложения");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}