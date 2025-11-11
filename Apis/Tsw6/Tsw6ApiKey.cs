using System;
using System.IO;
using GameFinder.RegistryUtils;
using GameFinder.StoreHandlers.Steam;
using GameFinder.StoreHandlers.Steam.Models.ValueTypes;
using System.Runtime.InteropServices;

namespace Tsw6RealtimeWeather.Apis.Tsw6;

public class Tsw6ApiKey
{
    private const string tsw6ApiKeyFileName = "CommAPIKey.txt";
    private const uint tsw6AppId = 3656800;

    private static string apiKey = string.Empty;

    public static string Get()
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            apiKey = RetrieveKey();
        }
        return apiKey;
    }
  
  private static string RetrieveKey()
  {

        // Search for the Steam title instead as we are in game mode.
        var steamHandler = new SteamHandler(NexusMods.Paths.FileSystem.Shared, OperatingSystem.IsWindows() ? WindowsRegistry.Shared : null);
        var tsw6Game = steamHandler.FindOneGameById(AppId.From(tsw6AppId), out var errors);

        if (errors.Length > 0)
        {
            foreach (var error in errors)
            {
                Logger.LogError($"Error in finding TSW6: {error}");
            }

            return string.Empty;
        }

        if (tsw6Game == null)
        {
            Logger.LogError("Error: TSW6 install cannot be found");
            return string.Empty;
        }

        // Game path is not platform specific to us.
        var gamePath = Path.Join(tsw6Game.Path.ToString(),
                                    "WindowsNoEditor",
                                    "TS2Prototype",
                                    "Saved",
                                    "Config",
                                    tsw6ApiKeyFileName);

        // Need to find documents folder based on platform.
        var docPath = string.Empty;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            ProtonWinePrefix? protonPrefix = tsw6Game.GetProtonPrefix();
            if (protonPrefix == null)
            {
                Logger.LogError("Error: Proton Prefix is not set.");
                return string.Empty;
            }

            if (!NexusMods.Paths.FileSystem.Shared.DirectoryExists(protonPrefix.ProtonDirectory))
            {
                Logger.LogError("Error: Proton Prefix is set but does not exist.");
                return string.Empty;
            }
            docPath = Path.Join(protonPrefix.ProtonDirectory.ToString(),
                                    "pfx",
                                    "drive_c",
                                    "users",
                                    "steamuser",
                                    "Documents");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            docPath = Environment.GetFolderPath(
                        Environment.SpecialFolder.MyDocuments);
        }
        else
        {
            Logger.LogWarning("Warning: Unsupported Platform, unable to find user documents.");
        }

        // from player documents folder
        docPath = Path.Join(docPath,
                            "My Games",
                            "TrainSimWorld6",
                            "Saved",
                            "Config",
                            tsw6ApiKeyFileName);

        if (File.Exists(docPath))
        {
            return File.ReadAllText(docPath);
        }
        else if (File.Exists(gamePath))
        {
            return File.ReadAllText(gamePath);
        }
        else
        {
            Logger.LogError("Error: Unable to find TSW 6 API key");
            return string.Empty;
        }
    }
}

