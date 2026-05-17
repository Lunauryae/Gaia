using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System;

namespace Gaia.Helpers;

public static unsafe class GardenMemoryHelper
{
    /// <summary>
    /// Checks if a specific plant object in the world is fully mature and ready for harvest.
    /// </summary>
    public static bool IsPlantMature(IGameObject plant)
    {
        if (plant == null || plant.Address == IntPtr.Zero) return false;

        var gameObject = (GameObject*)plant.Address;

        // THE FIX: EventState is a single byte representing the current visual/interaction state of the object.
        byte state = gameObject->EventState;

        // State values usually follow a linear progression (e.g., 0 = Empty, 1 = Seed, 2 = Growing, 3 = Mature, 4 = Wilted).
        // You will need to verify the exact "Ready!" number, but it is usually higher than the growing states.
        return state >= 3;
    }

    /// <summary>
    /// Quick debugger to figure out what the game calls "Mature".
    /// </summary>
    public static unsafe void PrintPlantState(IGameObject plant, Plugin plugin)
    {
        if (plant == null || plant.Address == IntPtr.Zero) return;

        // Cast the memory address directly to a byte pointer so we can read the raw data
        byte* ptr = (byte*)plant.Address;

        // Dump the memory block from 0x190 to 0x19F
        string dump1 = "";
        for (int i = 0x190; i < 0x1A0; i++)
        {
            dump1 += ptr[i].ToString("X2") + " ";
        }

        // Dump the memory block from 0x1A0 to 0x1AF
        string dump2 = "";
        for (int i = 0x1A0; i < 0x1B0; i++)
        {
            dump2 += ptr[i].ToString("X2") + " ";
        }

        Plugin.ChatGui.Print($"[Gaia Debug] 0x190: {dump1}");
        Plugin.ChatGui.Print($"[Gaia Debug] 0x1A0: {dump2}");
    }

    public static bool IsInHousingZone()
    {
        var housingManager = HousingManager.Instance();
        if (housingManager == null) return false;

        return housingManager->OutdoorTerritory != null || housingManager->IndoorTerritory != null;
    }
}