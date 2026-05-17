using FFXIVClientStructs.FFXIV.Client.Game;
using System;

namespace Gaia.Helpers; // Adjust namespace as needed

public static unsafe class InventoryHelper
{
    // Define the standard inventory bags we care about to avoid repeating this array
    private static readonly InventoryType[] MainBags = new[]
    {
        InventoryType.Inventory1,
        InventoryType.Inventory2,
        InventoryType.Inventory3,
        InventoryType.Inventory4
    };

    /// <summary>
    /// Gets the total quantity of a specific item ID across all main bags + HQ versions.
    /// </summary>
    public static int GetItemCount(uint itemId)
    {
        var invManager = InventoryManager.Instance();
        if (invManager == null) return 0;

        // FFXIVClientStructs has built-in methods for this, no manual loop needed!
        return invManager->GetInventoryItemCount(itemId, false) + invManager->GetInventoryItemCount(itemId, true);
    }

    /// <summary>
    /// Gets the total quantity of an item by name (relies on your GardenData dictionary).
    /// </summary>
    public static int GetItemCount(string itemName)
    {
        // Faster lookup: check your GardenData first
        if (GardenData.ItemIds != null && GardenData.ItemIds.TryGetValue(itemName, out uint id))
        {
            return GetItemCount(id);
        }
        return 0;
    }

    /// <summary>
    /// Locates the exact Bag, Slot, and ItemId for interacting with an item.
    /// </summary>
    public static (uint InvType, uint Slot, uint ItemId) GetItemLocation(uint itemId)
    {
        var invManager = InventoryManager.Instance();
        if (invManager == null) return (0, 0, 0);

        foreach (var type in MainBags)
        {
            var container = invManager->GetInventoryContainer(type);
            if (container == null) continue;

            for (int i = 0; i < container->Size; i++)
            {
                var slot = container->GetInventorySlot(i);
                if (slot != null && slot->ItemId == itemId && slot->Quantity > 0)
                {
                    return ((uint)type, (uint)i, slot->ItemId);
                }
            }
        }
        return (0, 0, 0);
    }

    /// <summary>
    /// Locates the exact Bag, Slot, and ItemId by searching Lumina sheets for a string match.
    /// </summary>
    public static (uint InvType, uint Slot, uint ItemId) GetItemLocation(string itemName)
    {
        // Fast path: if we know the ID, use the ID lookup instead of string matching
        if (GardenData.ItemIds != null && GardenData.ItemIds.TryGetValue(itemName, out uint knownId))
        {
            return GetItemLocation(knownId);
        }

        // Slow path: Fallback to Lumina sheet string matching (your original logic)
        var inv = InventoryManager.Instance();
        if (inv == null) return (0, 0, 0);

        var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
        string clean = itemName.Trim();

        foreach (var type in MainBags)
        {
            var container = inv->GetInventoryContainer(type);
            if (container == null) continue;

            for (int i = 0; i < container->Size; i++)
            {
                var slot = container->GetInventorySlot(i);
                if (slot->ItemId == 0) continue;
                if (!sheet.TryGetRow(slot->ItemId, out var data)) continue;

                if (data.Name.ToString().Contains(clean, StringComparison.OrdinalIgnoreCase))
                {
                    return ((uint)type, (uint)i, slot->ItemId);
                }
            }
        }
        return (0, 0, 0);
    }
}