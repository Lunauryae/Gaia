using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.IO;
using Gaia.Helpers;
using Gaia.Manager;
using Gaia.Windows;

namespace Gaia;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;

    private const string CommandName = "/gaia";

    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("Gaia");

    // --- Our Farming Managers! ---
    public FarmingManager Farming { get; private set; }
    public LocationManager LocationManager { get; private set; }
    public GardenContextManager GardenContext { get; private set; }
    public CrossbreedManager CrossbreedManager { get; private set; }
    public AutoReplantOrchestrator AutoReplant { get; private set; }


    // Windows
    private MainWindow MainWindow { get; init; }


    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);  // ← NEW: bind config to Dalamud for Save()

        // Initialize Managers
        Farming = new FarmingManager(this);
        Farming.Initialize();
        LocationManager = new LocationManager(this);
        GardenContext = new GardenContextManager(this);
        AutoReplant = new AutoReplantOrchestrator(this);


        GardenData.Initialize(DataManager);
        var csvPath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "crosses.csv");

        MainWindow = new MainWindow(this);
        CrossbreedManager = new CrossbreedManager(csvPath);

        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Gaia garden manager. Use /gaia water, harvest, stop."
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Farming.Dispose();

        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();

        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
        Framework.Update -= OnFrameworkUpdate;
    }

    private void OnCommand(string command, string args)
    {
        string subCommand = args.Trim().ToLower();

        if (subCommand == "water")
        {
            Farming.WaterNearestBed();
        }
        else if (subCommand == "harvest")
        {
            Farming.HarvestNearestBed();
        }
        else if (subCommand == "stop")
        {
            AutoReplant.Abort();
            Farming.Stop();
            ChatGui.Print("Emergency Stop Activated! Bot loop disabled.");
        }
        // --- The Step Command ---
        else if (subCommand == "yes" || subCommand == "y")
        {
            if (Farming.WaitingForUser)
            {
                Farming.WaitingForUser = false;
                ChatGui.Print("[FarmingBot] Resuming...");
            }
            else
            {
                ChatGui.Print("[FarmingBot] Bot is not currently paused.");
            }
        }
        else if (subCommand == "debug")
        {
            if (ObjectTable.LocalPlayer == null) return;
            ChatGui.Print("Scanning for Event Objects (plants/furniture) within 5 yalms...");

            foreach (var obj in ObjectTable)
            {
                if (obj.ObjectKind == ObjectKind.EventObj)
                {
                    float dist = Vector3.Distance(ObjectTable.LocalPlayer.Position, obj.Position);
                    if (dist < 5.0f)
                    {
                        ChatGui.Print($"Name: {obj.Name} | DataID: {obj.BaseId} | Dist: {dist:F2}");
                    }
                }
            }
        }
        else if (subCommand == "ui" || subCommand == "")
        {
            MainWindow.Toggle();
        }
    }

    public void PrintDebug(string message)
    {
        if (Configuration.ShowDebugMessages)
        {
            ChatGui.Print(message);
        }
    }

    public CharacterProfile GetCurrentCharacterProfile()
    {
        // Make sure we are fully loaded in-game
        if (ObjectTable.LocalPlayer == null || PlayerState.ContentId == 0)
            return null!;

        ulong currentId = PlayerState.ContentId;

        // If this character is brand new to the plugin, create a blank profile for them
        if (!Configuration.Characters.ContainsKey(currentId))
        {
            Configuration.Characters[currentId] = new CharacterProfile();
            Configuration.Save();
        }

        return Configuration.Characters[currentId];
    }


    private void OnFrameworkUpdate(IFramework framework)
    {
        // Just call our manager's update loop! Everything is handled neatly inside here now.
        Farming.Update();
        LocationManager.Update();
    }

    public void ToggleConfigUi() => MainWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
}
