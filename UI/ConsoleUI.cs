using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Rendering;
using Tsw6RealtimeWeather.Apis.OpenWeather.Models;
using Tsw6RealtimeWeather.Apis.Tsw6.Models;

namespace Tsw6RealtimeWeather.UI;

/// <summary>
/// Manages the Spectre.Console UI for displaying application status
/// </summary>
public class ConsoleUI
{
    private Panel? _distancePanel;
    private Panel? _realWeatherPanel;
    private Panel? _gameWeatherPanel;
    private Rows? _mainDisplay;
    
    private double _accumulatedDistance = 0.0;
    private double _distanceThreshold = 10.0;
    private string _currentLocation = "Unknown";
    private string _weatherInfo = "Waiting for first update...";
    private OpenWeatherResponse? _currentWeather = null;
    private Tsw6WeatherData? _gameWeatherState = null;
    
    private bool _tsw6Connected = false;
    private bool _apiKeysFound = false;
    private bool _subscriptionActive = false;

    private Task? _liveDisplayTask = null;
    private LiveDisplayContext? _liveContext = null;

    public void Initialize(double distanceThreshold)
    {
        _distanceThreshold = distanceThreshold;
        
        // Create initial display
        _mainDisplay = new Rows(new Text("Initializing..."));
        
        // Start live display in background
        _liveDisplayTask = Task.Run(() =>
        {
            AnsiConsole.Live(_mainDisplay)
                .AutoClear(false)
                .Overflow(VerticalOverflow.Visible)
                .Start(ctx =>
                {
                    _liveContext = ctx;
                    UpdateDisplay();
                    
                    // Keep the live display running
                    while (true)
                    {
                        Task.Delay(100).Wait();
                    }
                });
        });
        
        // Give it a moment to start
        Task.Delay(100).Wait();
    }

    public void UpdateStatusChecks(bool tsw6Connected, bool apiKeysFound, bool subscriptionActive)
    {
        _tsw6Connected = tsw6Connected;
        _apiKeysFound = apiKeysFound;
        _subscriptionActive = subscriptionActive;
        UpdateDisplay();
    }

    public void UpdateDistance(double accumulatedKm, string location)
    {
        _accumulatedDistance = accumulatedKm;
        _currentLocation = location;
        UpdateDisplay();
    }

    public void UpdateWeather(string weatherInfo)
    {
        _weatherInfo = weatherInfo;
        _currentWeather = null;
        UpdateDisplay();
    }

    public void UpdateWeather(OpenWeatherResponse? weather)
    {
        _currentWeather = weather;
        if (weather == null)
        {
            _weatherInfo = "⚠ Failed to fetch weather data";
        }
        else
        {
            _weatherInfo = "";
        }
        UpdateDisplay();
    }

    public void UpdateGameWeather(Tsw6WeatherData? gameWeather)
    {
        _gameWeatherState = gameWeather;
        UpdateDisplay();
    }

    public void ShowError(string message)
    {
        AnsiConsole.MarkupLine($"[red]✗[/] {Markup.Escape(message)}");
    }

    public void ShowSuccess(string message)
    {
        AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(message)}");
    }

    public void ShowInfo(string message)
    {
        AnsiConsole.MarkupLine($"[blue]ℹ[/] {Markup.Escape(message)}");
    }

    private void UpdateDisplay()
    {
        List<string> warnings = [];
        
        if (!_tsw6Connected)
            warnings.Add("⚠ TSW6 not connected - Start TSW6 with -HTTPAPI flag");
        
        if (!_apiKeysFound)
            warnings.Add("⚠ API keys not found - Check config.json");
        
        if (!_subscriptionActive && _tsw6Connected)
            warnings.Add("⚠ Subscription not active - Drive the train to activate");

        var distancePercentage = Math.Min((_accumulatedDistance / _distanceThreshold) * 100.0, 100.0);
        
        var progressChart = new BreakdownChart()
            .Width(100)
            .ShowPercentage()
            .UseValueFormatter(value => $"{value:F1}%")
            .AddItem("Travelled", distancePercentage, Color.Green)
            .AddItem("Remaining", 100.0 - distancePercentage, Color.Grey);

        var distanceContent = new Rows(
            new Markup($"[bold]Location:[/] {Markup.Escape(_currentLocation)}"),
            progressChart
        );

        _distancePanel = new Panel(distanceContent)
            .Header("[yellow]Distance Progress[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Yellow)
            .Padding(1, 1, 1, 1);

        IRenderable realWeatherContent;
        
        if (_currentWeather != null)
        {
            realWeatherContent = CreateWeatherDisplay(_currentWeather);
        }
        else if (!string.IsNullOrEmpty(_weatherInfo))
        {
            realWeatherContent = new Markup($"[dim]{Markup.Escape(_weatherInfo)}[/]");
        }
        else
        {
            realWeatherContent = new Markup("[dim]Waiting for first update...[/]");
        }
        
        _realWeatherPanel = new Panel(realWeatherContent)
            .Header("[cyan]Real-World Weather[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .Padding(1, 1, 1, 1);

        IRenderable gameWeatherContent;
        
        if (_gameWeatherState != null)
        {
            gameWeatherContent = CreateGameWeatherDisplay(_gameWeatherState);
        }
        else
        {
            gameWeatherContent = new Markup("[dim]No game weather data yet...[/]");
        }

        _gameWeatherPanel = new Panel(gameWeatherContent)
            .Header("[green]In-Game Weather (TSW6)[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Green)
            .Padding(1, 1, 1, 1);
        
        var weatherTable = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn(new TableColumn("Real").Width(50))
            .AddColumn(new TableColumn("Game").Width(50));
        
        weatherTable.AddRow(_realWeatherPanel, _gameWeatherPanel);

        // Build the complete display
        var rows = new List<IRenderable>();
        
        if (warnings.Count > 0)
        {
            foreach (var warning in warnings)
            {
                rows.Add(new Markup($"[yellow]{Markup.Escape(warning)}[/]"));
            }
            rows.Add(new Text("")); // Empty line
        }
        
        rows.Add(_distancePanel);
        rows.Add(new Text("")); // Empty line
        rows.Add(weatherTable);
        rows.Add(new Text("")); // Empty line
        rows.Add(new Markup("[dim]Ctrl+C to exit[/]"));
        
        var fullDisplay = new Rows(rows);
        
        // Update the live display
        _liveContext?.UpdateTarget(fullDisplay);
    }

    private static Grid CreateWeatherDisplay(OpenWeatherResponse weather)
    {
        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap().PadRight(1).Width(16))
            .AddColumn(new GridColumn().Width(30));

        if (!string.IsNullOrEmpty(weather.Name))
        {
            grid.AddRow(
                new Markup("[bold]Location:[/]"),
                new Markup($"{Markup.Escape(weather.Name)}, {weather.Sys?.Country ?? "??"}")
            );
        }

        if (weather.Weather != null && weather.Weather.Count > 0)
        {
            var condition = weather.Weather[0];
            grid.AddRow(
                new Markup($"[bold]Conditions:[/]"),
                new Markup($"{Markup.Escape(condition.Main ?? "Unknown")} - {Markup.Escape(condition.Description ?? "")}")
            );
        }

        if (weather.Main != null)
        {
            var tempC = weather.Main.Temp - 273.15;
            var tempColor = GetTemperatureColor(tempC);
            
            grid.AddRow(
                new Markup("[bold]Temperature[/]"),
                new Markup($"[{tempColor}]{tempC:F1}°C[/]")
            );
        }

        if (weather.Clouds != null)
        {
            var cloudColor = weather.Clouds.All switch
            {
                >= 80 => "grey",
                >= 50 => "silver",
                >= 20 => "white",
                _ => "cyan"
            };
            
            grid.AddRow(
                new Markup("[bold]Cloud cover:[/]"),
                new Markup($"[{cloudColor}]{weather.Clouds.All}%[/]")
            );
        }

        if (weather.Rain != null && weather.Rain.OneHour.HasValue && weather.Rain.OneHour.Value > 0)
        {
            grid.AddRow(
                new Markup("[bold]Rain:[/]"),
                new Markup($"[blue]{weather.Rain.OneHour.Value:F1} mm/h[/]")
            );
        }
        else if (weather.Snow != null && weather.Snow.OneHour.HasValue && weather.Snow.OneHour.Value > 0)
        {
            grid.AddRow(
                new Markup("[bold]Snow:[/]"),
                new Markup($"[white]{weather.Snow.OneHour.Value:F1} mm/h[/]")
            );
        }

        if (weather.Main != null)
        {
            var humidity = weather.Main.Humidity;
            var humidityColor = humidity switch
            {
                >= 80 => "blue",
                >= 60 => "cyan",
                >= 40 => "green",
                >= 20 => "yellow",
                _ => "orange1"
            };
            
            grid.AddRow(
                new Markup("[bold]Humidity:[/]"),
                new Markup($"[{humidityColor}]{humidity}%[/]")
            );
        }

        if (weather.Main != null)
        {
           
            if (weather.Visibility.HasValue)
            {
                var visibilityKm = weather.Visibility.Value / 1000.0;
                var infoText = $"{visibilityKm:F1}km";

                grid.AddRow(
                new Markup("[bold]Visibility:[/]"),
                new Markup($"[dim]{infoText}[/]")
            );
            }
        }

        // Last Update - compact
        var lastUpdate = DateTimeOffset.FromUnixTimeSeconds(weather.Dt).ToLocalTime();
        grid.AddRow(
            new Markup("[bold]Last updated:[/]"),
            new Markup($"{lastUpdate:HH:mm:ss}")
        );

        return grid;
    }

    private static Grid CreateGameWeatherDisplay(Tsw6WeatherData gameWeather)
    {
        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap().PadRight(1).Width(16))
            .AddColumn(new GridColumn().Width(10));

        // Temperature
        var tempColor = GetTemperatureColor(gameWeather.Temperature);
        grid.AddRow(
            new Markup("[bold]Temperature:[/]"),
            new Markup($"[{tempColor}]{gameWeather.Temperature:F1}°C[/]")
        );

        // Cloudiness (0-1 converted to percentage)
        var cloudPercentage = gameWeather.Cloudiness * 100.0;
        var cloudColor = cloudPercentage switch
        {
            >= 80 => "grey",
            >= 50 => "silver",
            >= 20 => "white",
            _ => "cyan"
        };
        grid.AddRow(
            new Markup("[bold]Cloud cover:[/]"),
            new Markup($"[{cloudColor}]{cloudPercentage:F0}%[/]")
        );

        // Precipitation (0-1 scale) - always show
        var precipPercentage = gameWeather.Precipitation * 100.0;
        grid.AddRow(
            new Markup("[bold]Precipitation:[/]"),
            new Markup($"[blue]{precipPercentage:F0}%[/]")
        );

        // Wetness (0-1 scale)
        var wetnessPercentage = gameWeather.Wetness * (100.0 / 0.15); // hide the scaling clamp from the user
        grid.AddRow(
            new Markup("[bold]Wetness:[/]"),
            new Markup($"[aqua]{wetnessPercentage:F0}%[/]")
        );

        // Ground Snow (0-1 scale) - always show
        var snowPercentage = gameWeather.GroundSnow * 100.0;
        grid.AddRow(
            new Markup("[bold]Ground snow:[/]"),
            new Markup($"[white]{snowPercentage:F0}%[/]")
        );

        // Fog Density (0-0.25 scale, display as 0-100%) - always show
        var fogPercentage = (gameWeather.FogDensity / 0.025) * 100.0;
        grid.AddRow(
            new Markup("[bold]Fog density:[/]"),
            new Markup($"[grey]{fogPercentage:F0}%[/]")
        );

        // Current time
        grid.AddRow(
            new Markup("[bold]Last updated:[/]"),
            new Markup($"{DateTime.Now:HH:mm:ss}")
        );

        return grid;
    }

    private static string GetTemperatureColor(double tempC)
    {
        return tempC switch
        {
            // Freezing and below - blue shades
            < -10 => "blue",           // Very cold: < -10°C
            < 0 => "dodgerblue1",      // Cold: -10°C to 0°C
            
            // Cool - cyan/teal shades
            < 10 => "cyan",            // Cool: 0°C to 10°C
            < 15 => "aqua",            // Mild cool: 10°C to 15°C
            
            // Comfortable - green shades
            < 20 => "green",           // Comfortable: 15°C to 20°C
            < 25 => "greenyellow",     // Warm: 20°C to 25°C
            
            // Hot - yellow/orange shades
            < 30 => "yellow",          // Hot: 25°C to 30°C
            < 35 => "orange1",         // Very hot: 30°C to 35°C
            
            // Extreme heat - red shades
            < 40 => "red",             // Extremely hot: 35°C to 40°C
            _ => "red1"                // Dangerously hot: 40°C+
        };
    }

    public static async Task ShowStartupProgress(Func<Task<bool>> tsw6Check, Func<Task<bool>> subscriptionSetup)
    {
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("green bold"))
            .StartAsync("Starting up...", async ctx =>
            {
                ctx.Status("Checking for TSW6 API...");
                await Task.Delay(500);
                var tsw6Result = await tsw6Check();
                
                if (tsw6Result)
                {
                    AnsiConsole.MarkupLine("[green]✓[/] TSW6 API connected");
                    
                    ctx.Status("Setting up subscription...");
                    await Task.Delay(500);
                    var subResult = await subscriptionSetup();
                    
                    if (subResult)
                    {
                        AnsiConsole.MarkupLine("[green]✓[/] Subscription registered");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[red]✗[/] Failed to register subscription");
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]✗[/] TSW6 API not available");
                }
                
                await Task.Delay(1000);
            });
    }
}
