global using static Oracle.Plugin;
using Oracle.Models;
using Oracle.Services;

internal static class Program
{
    private static void Check(bool condition, string label) { if (!condition) throw new Exception(label); }
    private static TimelineDocument Doc(uint zone = 100, uint job = 19, string phase = "") => new() { TerritoryTypeId = zone, ClassJobId = job, AutoLoadPresetId = phase, ReplayEnabled = true };
    public static void Main()
    {
        var a = Doc(); var b = Doc(); b.AutoLoadEnabled = true;
        var zone = Doc(200); zone.AutoLoadEnabled = true;
        var job = Doc(job: 20); job.AutoLoadEnabled = true;
        var phase = Doc(phase: "boss"); phase.AutoLoadEnabled = true;
        var store = new TimelineStore(a,b,zone,job,phase);
        ReplayAutoLoad.Select(store,a,true);
        Check(a.AutoLoadEnabled && !b.AutoLoadEnabled, "Selecting a preset must replace the same slot");
        Check(zone.AutoLoadEnabled && job.AutoLoadEnabled && phase.AutoLoadEnabled, "Other zones, jobs and phases must remain unchanged");
        ReplayAutoLoad.Select(store,a,false);
        Check(!a.AutoLoadEnabled, "User can opt out");
        var invalid = Doc(0); ReplayAutoLoad.Select(store,invalid,true);
        Check(!invalid.AutoLoadEnabled, "Unset zone must not auto-load");
        C.LogMimicAutoLoadMigrated = false;store.ActiveDocument = b;
        ReplayAutoLoad.MigrateOnce(store);
        Check(b.AutoLoadEnabled && !a.AutoLoadEnabled, "Migration should prefer selected replay preset");
        Check(zone.AutoLoadEnabled && phase.AutoLoadEnabled, "Migration must preserve configured slots");
        ReplayAutoLoad.Select(store,b,false);ReplayAutoLoad.MigrateOnce(store);
        Check(!b.AutoLoadEnabled, "Migration must run only once and preserve later opt-out");
        C.LogMimicAutoLoadMigrated=false;
        var manual=Doc();manual.ReplayEnabled=false;manual.AutoLoadEnabled=true;
        var replay=Doc();ReplayAutoLoad.MigrateOnce(new TimelineStore(manual,replay));
        Check(manual.AutoLoadEnabled && !replay.AutoLoadEnabled, "Migration must preserve an existing non-replay preference");
        Console.WriteLine("Auto Load selection, isolation, opt-out and migration checks passed.");
    }
}
namespace Oracle
{
    public static class Plugin { public static Configuration C = new(); }
    public sealed class Configuration { public bool LogMimicAutoLoadMigrated; public void Save() {} }
}
namespace Oracle.Models
{
    internal sealed class TimelineDocument { public uint TerritoryTypeId, ClassJobId; public string AutoLoadPresetId=""; public bool ReplayEnabled, AutoLoadEnabled; }
}
namespace Oracle.Services
{
    internal sealed class TimelineStore(params TimelineDocument[] docs)
    {
        public IReadOnlyList<TimelineDocument> Documents { get; } = docs;
        public TimelineDocument? ActiveDocument { get; set; }
        public void SaveDocument(TimelineDocument doc) {}
    }
}
