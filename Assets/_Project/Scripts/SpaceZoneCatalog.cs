/// <summary>
/// Named space zones and their short "radio message" entry lines, purely for the on-screen
/// zone-transition announcement (ScoreUIBinder). Deliberately a separate, standalone table from
/// RushTrackEnvironment's SpaceZones (rock/vein/debris colors) and SceneMoodController's
/// MoodStage (light/fog colors) -- those two already share the same 0/400/900/1600 thresholds
/// and are tuned/committed visual systems; duplicating the thresholds here for a text label
/// avoids any risk of touching that tuned code while adding the lightweight "story" layer.
/// </summary>
public static class SpaceZoneCatalog
{
    public readonly struct ZoneInfo
    {
        public readonly int ScoreThreshold;
        public readonly string Name;
        public readonly string EntryMessage;

        public ZoneInfo(int scoreThreshold, string name, string entryMessage)
        {
            ScoreThreshold = scoreThreshold;
            Name = name;
            EntryMessage = entryMessage;
        }
    }

    public static readonly ZoneInfo[] Zones =
    {
        new ZoneInfo(0, "DEEP VOID", "SIGNAL LOST // ENTERING THE DEEP VOID"),
        new ZoneInfo(400, "ASTEROID BELT", "WARNING // ASTEROID BELT AHEAD"),
        new ZoneInfo(900, "ICE COMET FIELD", "HULL FROST WARNING // ICE COMET FIELD"),
        new ZoneInfo(1600, "NEBULA CORE", "ENERGY SPIKE DETECTED // NEBULA CORE"),
    };

    public static int ResolveIndex(int score)
    {
        int index = 0;

        for (int i = 0; i < Zones.Length; i++)
        {
            if (score >= Zones[i].ScoreThreshold)
            {
                index = i;
            }
        }

        return index;
    }
}
