namespace PitWall.Core.Models;

public sealed record RaceCarSnapshot(
    string Code,
    string Driver,
    int Position,
    int Lap,
    double LapProgress,
    string Tyre,
    int TyreAge,
    double GapToLeaderSeconds,
    string Status,
    int SpeedKph,
    string Team);
