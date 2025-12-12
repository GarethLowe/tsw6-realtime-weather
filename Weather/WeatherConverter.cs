using System;
using Tsw6RealtimeWeather.Apis.OpenWeather.Models;
using Tsw6RealtimeWeather.Apis.Tsw6.Models;

namespace Tsw6RealtimeWeather.Weather;

/// <summary>
/// Converts OpenWeather API data to TSW6 weather format
/// </summary>
public static class WeatherConverter
{
    /// <summary>
    /// Converts OpenWeather API response to TSW6 weather data
    /// </summary>
    public static Tsw6WeatherData ConvertToTsw6Weather(OpenWeatherResponse openWeather)
    {
        var tsw6Weather = new Tsw6WeatherData();

        if (openWeather.Main != null)
        {
            tsw6Weather.Temperature = openWeather.Main.Temp - 273.15;
        }

        if (openWeather.Clouds != null)
        {
            tsw6Weather.Cloudiness = openWeather.Clouds.All / 100.0;
        }

        // Map precipitation intensity from mm/h to 0-1 scale (0mm = 0, 10mm/h+ = 1)
        double precipitationMmh = 0;
        
        if (openWeather.Rain?.OneHour.HasValue == true)
        {
            precipitationMmh += openWeather.Rain.OneHour.Value;
        }
        
        if (openWeather.Snow?.OneHour.HasValue == true)
        {
            precipitationMmh += openWeather.Snow.OneHour.Value;
        }
        
        tsw6Weather.Precipitation = Math.Clamp(precipitationMmh / 10.0, 0, 1);

        // Wetness: weighted combination of precipitation (80%) and humidity (20%)
        double humidityFactor = openWeather.Main?.Humidity / 100.0 ?? 0;

        // rail adhesion seems to get nasty after about 10% dampness, so clamp WAY down
        // up to 15% seems doable
        tsw6Weather.Wetness = Math.Clamp(
            (tsw6Weather.Precipitation * 0.125) + (humidityFactor * 0.075),
            0, 
            0.15
        );

        // Ground snow only appears during active snowfall (weather ID 6xx range)
        bool isSnowing = openWeather.Weather?.Count > 0 && 
                        openWeather.Weather[0].Id >= 600 && 
                        openWeather.Weather[0].Id < 700;
        
        if (isSnowing && openWeather.Snow?.OneHour.HasValue == true)
        {
            // Map snow intensity to ground coverage (0mm = 0, 5mm/h+ = 1)
            tsw6Weather.GroundSnow = Math.Clamp(openWeather.Snow.OneHour.Value / 5.0, 0, 1);
        }
        else
        {
            tsw6Weather.GroundSnow = 0;
        }

        // Fog density based on visibility (under 1km = heavy fog, 10km+ = clear)
        if (openWeather.Visibility.HasValue)
        {
            const double visibilityLowerBound = 0.025;
            double visibilityKm = openWeather.Visibility.Value / 1000.0;
            
            if (visibilityKm >= 10)
            {
                tsw6Weather.FogDensity = 0;
            }
            else if (visibilityKm <= 1)
            {
                tsw6Weather.FogDensity = visibilityLowerBound;
            }
            else
            {
                tsw6Weather.FogDensity = visibilityLowerBound * (1 - ((visibilityKm - 1) / 9.0));
            }
        }
        else
        {
            tsw6Weather.FogDensity = 0;
        }

        Logger.LogDebug($"Weather conversion: Temp={tsw6Weather.Temperature:F1}°C, " +
                       $"Cloud={tsw6Weather.Cloudiness:F2}, Precip={tsw6Weather.Precipitation:F2}, " +
                       $"Wet={tsw6Weather.Wetness:F2}, Snow={tsw6Weather.GroundSnow:F2}, " +
                       $"Fog={tsw6Weather.FogDensity:F3}");

        return tsw6Weather;
    }
}
