namespace GTACoop.Sync;

/// <summary>Sunucu başına senkronizasyon durumunun diskte saklanması.</summary>
public static class SyncStateStore
{
    public static string StateDir(string dataFolder) =>
        Path.Combine(dataFolder, "syncstate");

    public static string StatePath(string dataFolder, string serverId) =>
        Path.Combine(StateDir(dataFolder), AddonApplier.Safe(serverId) + ".json");

    public static SyncState Load(string dataFolder, string serverId)
    {
        var state = SyncJson.Load<SyncState>(StatePath(dataFolder, serverId));
        if (state == null)
            state = new SyncState { ServerId = serverId };
        if (string.IsNullOrEmpty(state.ServerId)) state.ServerId = serverId;
        return state;
    }

    public static void Save(string dataFolder, SyncState state) =>
        SyncJson.Save(StatePath(dataFolder, state.ServerId), state);

    public static List<SyncState> LoadAll(string dataFolder)
    {
        var dir = StateDir(dataFolder);
        var list = new List<SyncState>();
        if (!Directory.Exists(dir)) return list;
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            var s = SyncJson.Load<SyncState>(file);
            if (s != null) list.Add(s);
        }
        return list;
    }
}
