namespace Gaia.Manager;

public enum LocationContext
{
    Unknown = 0,
    Outdoors = 1,
    House = 2,
    FCApartment = 3,
    PersonalApartment = 4
}

public class LocationManager
{
    private readonly Plugin _plugin;

    public LocationContext CurrentLocation { get; private set; } = LocationContext.Unknown;
    public int LastHouseViewMode { get; private set; } = 0; // 0 = Personal, 1 = FC

    private long nextScanTime = 0;
    private uint lastTerritory = 0;
    private long zoneTransitionTime = 0;

    public LocationManager(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void Update()
    {
        // Use ObjectTable to check player status (Prevents Obsolete Warning)
        if (Plugin.ObjectTable.LocalPlayer == null) return;

        uint terr = Plugin.ClientState.TerritoryType;
        if (terr == 0) return;

        if (terr != lastTerritory)
        {
            lastTerritory = terr;
            zoneTransitionTime = Environment.TickCount64 + 3000;
            CurrentLocation = LocationContext.Unknown;
            return;
        }

        if (Environment.TickCount64 < zoneTransitionTime) return;
        if (Environment.TickCount64 < nextScanTime) return;
        nextScanTime = Environment.TickCount64 + 2000;

        var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>();
        if (sheet != null && sheet.TryGetRow(terr, out var row))
        {
            uint useId = row.TerritoryIntendedUse.RowId;

            if (useId == 13) { CurrentLocation = LocationContext.Outdoors; return; }
            if (useId == 14)
            {
                uint[] fcChambers = { 384, 385, 386, 652, 987 };
                uint[] apartments = { 574, 575, 608, 609, 655, 988 };

                if (fcChambers.Contains(terr)) { CurrentLocation = LocationContext.FCApartment; return; }
                if (apartments.Contains(terr)) { CurrentLocation = LocationContext.PersonalApartment; return; }

                CurrentLocation = LocationContext.House;
                LastHouseViewMode = 0;

                string[] fcDoorNames = { "Entrance to Additional Chambers", "個室への扉", "Zugang zu den privaten Zimmern", "Accès aux chambres privées" };
                foreach (var obj in Plugin.ObjectTable)
                {
                    if (obj == null || obj.ObjectKind != ObjectKind.EventObj) continue;
                    if (fcDoorNames.Contains(obj.Name.ToString(), StringComparer.OrdinalIgnoreCase))
                    {
                        if (obj.IsTargetable) { LastHouseViewMode = 1; break; }
                    }
                }
                return;
            }
        }
        CurrentLocation = LocationContext.Outdoors;
    }
}