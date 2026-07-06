using System.Diagnostics;
using PitWall.Core.Models;
using PitWall.Core.Storage;

namespace PitWall.Core.State;

public sealed class PitWallStateMachine
{
    private readonly object _sync = new();
    private readonly SqliteEventStore _eventStore;
    private readonly Stopwatch _raceClock = Stopwatch.StartNew();

    private const double CircuitLengthKm = 5.891;
    private const double ReferenceLapTimeSeconds = 92.0;
    private const double RaceSimulationTimeScale = 3.5;
    private const double PitStopLossSeconds = 22.0;

    private DateTimeOffset _lastRaceModelUpdate = DateTimeOffset.UtcNow;

    private readonly Dictionary<string, ServiceState> _services;
    private readonly Dictionary<string, Incident> _incidents = new();
    private readonly Dictionary<string, CarRuntimeState> _carRuntime;

    private DeploymentState _deployment;
    private int _incidentCounter;

    private readonly List<(string Code, string Driver, string Team, string Tyre, double BaseSpeed)> _cars = new()
    {
        ("ALP1", "Alpine A", "Alpine", "Medium", 1.000),
        ("ALP2", "Alpine B", "Alpine", "Hard", 0.987),
        ("RBR", "Red Bull", "Rival", "Soft", 1.018),
        ("FER", "Ferrari", "Rival", "Medium", 1.009),
        ("MCL", "McLaren", "Rival", "Hard", 1.004),
        ("MER", "Mercedes", "Rival", "Medium", 0.999),
        ("WIL", "Williams", "Rival", "Soft", 0.981),
        ("AST", "Aston", "Rival", "Hard", 0.976)
    };

    private sealed class CarRuntimeState
    {
        public string Tyre { get; set; } = "Medium";
        public int LastPitLap { get; set; }
        public double GapPenaltySeconds { get; set; }
        public DateTimeOffset? PitUntil { get; set; }

        public double DistanceLaps { get; set; }
        public double LastSpeedKph { get; set; }
    }
private sealed record DriverRaceProfile(
    double LaunchFactor,
    double EarlyPaceFactor,
    double AttackPaceFactor,
    double LatePaceFactor,
    double TyreManagementFactor,
    double RaceCraftFactor,
    int AttackLapStart,
    int AttackLapEnd);
private sealed record DemoCarState(
    string Code,
    double GapToLeaderSeconds,
    string Tyre,
    int TyreAge);


private readonly Dictionary<string, DriverRaceProfile> _driverProfiles =
    new(StringComparer.OrdinalIgnoreCase)
{
    // ALP1: measured start, strong middle-stint attack.
    ["ALP1"] = new(0.996, 0.992, 1.026, 1.014, 1.03, 1.08, 3, 8),

    // ALP2: conservative early, better tyre life, improves later.
    ["ALP2"] = new(0.985, 0.990, 1.010, 1.018, 1.08, 0.98, 6, 11),

    // RBR: strong start, fast early, fades once soft tyres age.
    ["RBR"] = new(1.030, 1.012, 1.006, 0.990, 0.94, 1.10, 1, 3),

    // FER: aggressive opening stint.
    ["FER"] = new(1.010, 1.012, 1.016, 1.002, 1.00, 1.02, 2, 5),

    // MCL: hard-tyre car, slower early, strong late.
    ["MCL"] = new(0.992, 0.998, 1.020, 1.026, 1.07, 1.05, 5, 10),

    // MER: consistent, better as tyres age.
    ["MER"] = new(0.998, 0.999, 1.006, 1.012, 1.04, 1.01, 5, 9),

    // WIL: quick launch on softs, then fades badly.
    ["WIL"] = new(1.025, 1.006, 0.995, 0.970, 0.90, 0.95, 1, 2),

    // AST: slow early, mild late improvement.
    ["AST"] = new(0.990, 0.992, 1.002, 1.008, 1.05, 0.94, 7, 12)
};
    public PitWallStateMachine(SqliteEventStore eventStore)
    {
        _eventStore = eventStore;

        var now = DateTimeOffset.UtcNow;

        _services = new[]
        {
            new ServiceState("Telemetry Ingest", ServiceStatus.Healthy, 42, 0.0, "Receiving car packets normally", now),
            new ServiceState("Timing Feed Parser", ServiceStatus.Healthy, 38, 0.0, "Timing packets parsed", now),
            new ServiceState("Strategy Engine", ServiceStatus.Healthy, 64, 0.0, "Strategy model online", now),
            new ServiceState("Incident Store", ServiceStatus.Healthy, 18, 0.0, "SQLite event log writable", now),
            new ServiceState("Track Map Stream", ServiceStatus.Healthy, 51, 0.0, "Broadcasting car snapshots", now)
        }.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

        _deployment = new DeploymentState(
            "Strategy Engine",
            "v1.4.2",
            "v1.5.0",
            DeploymentStatus.Idle,
            0,
            "No deployment in progress",
            now);

        _carRuntime = _cars
            .Select(car => new
            {
                car.Code,
                State = new CarRuntimeState
                {
                    Tyre = car.Tyre,
                    LastPitLap = 1,
                    GapPenaltySeconds = 0,
                    PitUntil = null,
                    DistanceLaps = 0,
                    LastSpeedKph = 0
                }
            })
            .ToDictionary(x => x.Code, x => x.State, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ServiceState> GetServices()
    {
        lock (_sync)
        {
            return _services.Values.OrderBy(s => s.Name).ToList();
        }
    }

    public IReadOnlyList<Incident> GetIncidents()
    {
        lock (_sync)
        {
            return _incidents.Values.OrderByDescending(i => i.CreatedAt).ToList();
        }
    }

    public DeploymentState GetDeployment()
    {
        lock (_sync)
        {
            return _deployment;
        }
    }

    public SystemSnapshot GetSnapshot()
    {
        return new SystemSnapshot(
            GetServices(),
            GetIncidents(),
            GetDeployment(),
            GetRaceCars(),
            GetCurrentLap(),
            GetMode());
    }

    public IReadOnlyList<RaceCarSnapshot> GetRaceCars()
    {
        lock (_sync)
        {
            var now = DateTimeOffset.UtcNow;

            AdvanceRaceModel(now);

            var telemetrySlow = _services.Values.Any(service =>
                service.Name.Contains("Telemetry", StringComparison.OrdinalIgnoreCase) &&
                service.Status == ServiceStatus.Degraded);

            var timingUncertain = _services.Values.Any(service =>
                service.Name.Contains("Timing", StringComparison.OrdinalIgnoreCase) &&
                service.Status != ServiceStatus.Healthy);

            var strategyDown = _services.Values.Any(service =>
                service.Name.Contains("Strategy", StringComparison.OrdinalIgnoreCase) &&
                service.Status == ServiceStatus.Failed);

            var raceEntries = _cars.Select((car, index) =>
            {
                var runtime = _carRuntime[car.Code];

                var lap = Math.Max(1, (int)Math.Floor(runtime.DistanceLaps) + 1);
                var tyreAge = Math.Max(0, lap - runtime.LastPitLap);

                var status =
                    runtime.PitUntil is not null && runtime.PitUntil > now
                        ? "Pit stop"
                    : timingUncertain
                        ? "Timing uncertain"
                    : strategyDown && car.Team.Equals("Alpine", StringComparison.OrdinalIgnoreCase)
                        ? "Strategy unavailable"
                    : telemetrySlow
                        ? "Telemetry delayed"
                    : tyreAge > 18
                        ? "Tyre risk"
                    : "Normal";

                return new
                {
                    Car = car,
                    Index = index,
                    Runtime = runtime,
                    Lap = lap,
                    TyreAge = tyreAge,
                    Status = status,
                    DistanceLaps = runtime.DistanceLaps,
                    SpeedKph = Math.Max(0, (int)Math.Round(runtime.LastSpeedKph))
                };
            })
            .OrderByDescending(entry => entry.DistanceLaps)
            .ThenBy(entry => entry.Index)
            .ToList();

            if (!raceEntries.Any())
            {
                return Array.Empty<RaceCarSnapshot>();
            }

            var leader = raceEntries[0];
            var leaderDistance = leader.DistanceLaps;

            var leaderLapsPerSecond = Math.Max(
                0.000001,
                (leader.SpeedKph / 3600.0) / CircuitLengthKm);

            return raceEntries.Select((entry, position) =>
            {
                var gapSeconds = position == 0
                    ? 0
                    : Math.Round((leaderDistance - entry.DistanceLaps) / leaderLapsPerSecond, 1);

                return new RaceCarSnapshot(
                    entry.Car.Code,
                    entry.Car.Driver,
                    position + 1,
                    entry.Lap,
                    NormalizeProgress(entry.DistanceLaps),
                    entry.Runtime.Tyre,
                    entry.TyreAge,
                    gapSeconds,
                    entry.Status,
                    entry.SpeedKph,
                    entry.Car.Team);
            }).ToList();
        }
    }

    public IReadOnlyList<StrategyRecommendation> GetStrategyRecommendations()
{
    lock (_sync)
    {
        var allCars = GetRaceCars()
            .OrderBy(car => car.Position)
            .ToList();

        var alpineCars = allCars
            .Where(car => car.Team.Equals("Alpine", StringComparison.OrdinalIgnoreCase))
            .OrderBy(car => car.Position)
            .ToList();

        var strategyDown = _services.Values.Any(service =>
            service.Name.Contains("Strategy", StringComparison.OrdinalIgnoreCase) &&
            service.Status == ServiceStatus.Failed);

        if (strategyDown)
        {
            return alpineCars
                .Select(car => BuildUnavailableStrategyRecommendation(car, allCars))
                .ToList();
        }

        return alpineCars
            .Select(car => BuildStrategyRecommendation(car, allCars))
            .ToList();
    }
}
public async Task<OperationResult> ForceDemoScenarioAsync(
    string scenario,
    CancellationToken ct = default)
{
    OperationsEvent evt;
    string scenarioName;

    lock (_sync)
    {
        var normalized = scenario.Trim().ToLowerInvariant();

        _raceClock.Restart();
        _lastRaceModelUpdate = DateTimeOffset.UtcNow;

        scenarioName = normalized switch
        {
            "undercut" => "Force undercut scenario",
            "overcut" => "Force overcut scenario",
            "cover-rival" => "Force cover-rival scenario",
            "bad-pit-window" => "Force bad pit-window scenario",
            "tyre-cliff" => "Force tyre-cliff scenario",
            _ => throw new InvalidOperationException($"Unknown demo scenario '{scenario}'.")
        };

        var demoCars = normalized switch
        {
            "undercut" => new[]
            {
                new DemoCarState("RBR", 0.0, "Medium", 8),
                new DemoCarState("FER", 1.8, "Medium", 10),
                new DemoCarState("ALP1", 3.2, "Medium", 14),
                new DemoCarState("ALP2", 30.0, "Hard", 8),
                new DemoCarState("MCL", 32.0, "Hard", 6),
                new DemoCarState("MER", 34.0, "Medium", 9),
                new DemoCarState("WIL", 36.0, "Soft", 10),
                new DemoCarState("AST", 38.0, "Hard", 7)
            },

            "overcut" => new[]
            {
                new DemoCarState("RBR", 0.0, "Soft", 11),
                new DemoCarState("ALP1", 2.0, "Hard", 13),
                new DemoCarState("FER", 26.0, "Medium", 9),
                new DemoCarState("ALP2", 28.0, "Hard", 8),
                new DemoCarState("MCL", 31.0, "Hard", 7),
                new DemoCarState("MER", 34.0, "Medium", 8),
                new DemoCarState("WIL", 37.0, "Soft", 10),
                new DemoCarState("AST", 40.0, "Hard", 8)
            },

            "cover-rival" => new[]
            {
                new DemoCarState("RBR", 0.0, "Medium", 8),
                new DemoCarState("ALP1", 2.0, "Medium", 14),
                new DemoCarState("MER", 3.0, "Medium", 12),
                new DemoCarState("FER", 28.0, "Medium", 9),
                new DemoCarState("ALP2", 31.0, "Hard", 8),
                new DemoCarState("MCL", 34.0, "Hard", 7),
                new DemoCarState("WIL", 37.0, "Soft", 10),
                new DemoCarState("AST", 40.0, "Hard", 8)
            },

            "bad-pit-window" => new[]
            {
                new DemoCarState("RBR", 0.0, "Medium", 8),
                new DemoCarState("FER", 1.0, "Medium", 9),
                new DemoCarState("MCL", 2.0, "Hard", 7),
                new DemoCarState("MER", 3.0, "Medium", 10),
                new DemoCarState("ALP1", 4.0, "Medium", 18),
                new DemoCarState("ALP2", 5.0, "Hard", 9),
                new DemoCarState("WIL", 6.0, "Soft", 10),
                new DemoCarState("AST", 7.0, "Hard", 8)
            },

            "tyre-cliff" => new[]
            {
                new DemoCarState("RBR", 0.0, "Medium", 8),
                new DemoCarState("ALP1", 3.0, "Medium", 19),
                new DemoCarState("FER", 30.0, "Medium", 8),
                new DemoCarState("ALP2", 32.0, "Hard", 8),
                new DemoCarState("MCL", 34.0, "Hard", 7),
                new DemoCarState("MER", 36.0, "Medium", 8),
                new DemoCarState("WIL", 38.0, "Soft", 10),
                new DemoCarState("AST", 40.0, "Hard", 8)
            },

            _ => throw new InvalidOperationException($"Unknown demo scenario '{scenario}'.")
        };

        ApplyDemoRaceState(demoCars);
    }

    evt = await _eventStore.AppendEventAsync(
        EventType.RaceSnapshotUpdated,
        "Demo Scenario",
        null,
        scenarioName,
        $"Demo scenario forced: {scenarioName}",
        ct);

    return Result(new[] { evt }, Array.Empty<Alert>());
}
private sealed record StrategyContext(
    RaceCarSnapshot Car,
    RaceCarSnapshot? CarAhead,
    RaceCarSnapshot? CarBehind,
    string RacePhase,
    string RecommendedPitTyre,
    int ProjectedRejoinPosition,
    int PositionsLostIfPit,
    double ProjectedGapAfterPit,
    double? GapToCarAheadSeconds,
    double? GapToCarBehindSeconds,
    double TyreLifePressure,
    double TyreTimeLossPerLap,
    double PitWindowScore,
    double UndercutScore,
    double OvercutScore,
    double CoverThreatScore,
    double Confidence);

private static StrategyRecommendation BuildStrategyRecommendation(
    RaceCarSnapshot car,
    IReadOnlyList<RaceCarSnapshot> allCars)
{
    var context = BuildStrategyContext(car, allCars);

    if (car.Status == "Pit stop")
    {
        return CreateRecommendation(
            context,
            "PITTING",
            "Medium",
            car.Tyre,
            0.96,
            "Car is already committed to the pit-stop sequence.",
            new[]
            {
                "Pit stop already active",
                $"Current rejoin projection: P{context.ProjectedRejoinPosition}",
                "No additional strategy call required"
            });
    }

    if (context.TyreLifePressure >= 0.94 && context.PositionsLostIfPit <= 3)
    {
        return CreateRecommendation(
            context,
            "PIT NOW",
            "Critical",
            context.RecommendedPitTyre,
            0.92,
            $"Tyre cliff risk is high and the pit window is acceptable. Expected rejoin is P{context.ProjectedRejoinPosition}, losing {context.PositionsLostIfPit} position(s).",
            new[]
            {
                $"Tyre pressure: {FormatPercent(context.TyreLifePressure)}",
                $"Estimated tyre loss: {context.TyreTimeLossPerLap:F2}s/lap",
                $"Projected rejoin: P{context.ProjectedRejoinPosition}",
                $"Pit loss model: {PitStopLossSeconds:F0}s"
            });
    }

    if (context.TyreLifePressure >= 0.94 && context.PositionsLostIfPit > 3)
    {
        return CreateRecommendation(
            context,
            "EXTEND STINT",
            "High",
            car.Tyre,
            0.84,
            $"Tyres are near the cliff, but pitting now would drop the car to P{context.ProjectedRejoinPosition}. Extend until the pit window improves or a safety-car-style opportunity appears.",
            new[]
            {
                $"Tyre pressure: {FormatPercent(context.TyreLifePressure)}",
                $"Projected positions lost: {context.PositionsLostIfPit}",
                "Pit window currently closed",
                "Track position risk outweighs immediate tyre benefit"
            });
    }

    if (context.UndercutScore >= 0.72 && context.PositionsLostIfPit <= 2)
    {
        return CreateRecommendation(
            context,
            "UNDERCUT",
            "High",
            context.RecommendedPitTyre,
            0.88,
            $"Car ahead is vulnerable. Pit now to use fresh tyres and attempt an undercut. Expected rejoin is P{context.ProjectedRejoinPosition}.",
            new[]
            {
                $"Gap to car ahead: {FormatGap(context.GapToCarAheadSeconds)}",
                $"Undercut score: {FormatPercent(context.UndercutScore)}",
                $"Projected rejoin: P{context.ProjectedRejoinPosition}",
                "Fresh-tyre delta can attack the car ahead"
            });
    }

    if (context.CoverThreatScore >= 0.70 && context.PositionsLostIfPit <= 2)
    {
        return CreateRecommendation(
            context,
            "COVER RIVAL",
            "High",
            context.RecommendedPitTyre,
            0.86,
            $"Car behind is close enough to threaten an undercut. Pit now to cover the rival and protect track position.",
            new[]
            {
                $"Gap to car behind: {FormatGap(context.GapToCarBehindSeconds)}",
                $"Cover threat: {FormatPercent(context.CoverThreatScore)}",
                $"Projected rejoin: P{context.ProjectedRejoinPosition}",
                "Defensive stop protects position"
            });
    }

    if (context.OvercutScore >= 0.68)
    {
        return CreateRecommendation(
            context,
            "OVERCUT",
            "Medium",
            car.Tyre,
            0.78,
            $"Stay out while the car ahead is suffering tyre loss. Current tyres are still usable, so extending could gain track position after the rival stops.",
            new[]
            {
                $"Gap to car ahead: {FormatGap(context.GapToCarAheadSeconds)}",
                $"Overcut score: {FormatPercent(context.OvercutScore)}",
                "Current tyre condition is still manageable",
                "Use clean air and opponent degradation"
            });
    }

    if (context.TyreLifePressure >= 0.76 && context.PitWindowScore >= 0.58)
    {
        return CreateRecommendation(
            context,
            "PIT SOON",
            "High",
            context.RecommendedPitTyre,
            0.82,
            $"Tyres are degrading and the pit window is open. Prepare to stop within the next two laps.",
            new[]
            {
                $"Tyre pressure: {FormatPercent(context.TyreLifePressure)}",
                $"Pit-window score: {FormatPercent(context.PitWindowScore)}",
                $"Projected rejoin: P{context.ProjectedRejoinPosition}",
                $"Positions at risk: {context.PositionsLostIfPit}"
            });
    }

    if (context.TyreLifePressure >= 0.76 && context.PitWindowScore < 0.58)
    {
        return CreateRecommendation(
            context,
            "EXTEND STINT",
            "High",
            car.Tyre,
            0.76,
            $"Tyres are degrading, but the pit window is poor. Stay out temporarily to avoid dropping to P{context.ProjectedRejoinPosition}.",
            new[]
            {
                $"Tyre pressure: {FormatPercent(context.TyreLifePressure)}",
                $"Pit-window score: {FormatPercent(context.PitWindowScore)}",
                $"Projected positions lost: {context.PositionsLostIfPit}",
                "Wait for a cleaner rejoin window"
            });
    }

    if (context.TyreLifePressure >= 0.52)
    {
        return CreateRecommendation(
            context,
            "MONITOR",
            "Medium",
            context.RecommendedPitTyre,
            0.70,
            $"Tyres are approaching the operating limit. Keep monitoring gaps before committing to a stop.",
            new[]
            {
                $"Tyre pressure: {FormatPercent(context.TyreLifePressure)}",
                $"Estimated tyre loss: {context.TyreTimeLossPerLap:F2}s/lap",
                $"Projected rejoin if stopping now: P{context.ProjectedRejoinPosition}",
                "No immediate pit trigger yet"
            });
    }

    if (context.RacePhase == "Opening phase")
    {
        return CreateRecommendation(
            context,
            "MANAGE TYRES",
            "Low",
            car.Tyre,
            0.72,
            "Opening phase. Keep tyre temperatures and track position stable before the first strategy window.",
            new[]
            {
                "Race still in opening phase",
                $"Current position: P{car.Position}",
                "Avoid unnecessary early pit loss",
                "Build gap data before strategy call"
            });
    }

    return CreateRecommendation(
        context,
        "STAY OUT",
        "Low",
        car.Tyre,
        0.74,
        $"Tyres are still inside the usable window and pitting now would rejoin P{context.ProjectedRejoinPosition}. Staying out protects track position.",
        new[]
        {
            $"Tyre pressure: {FormatPercent(context.TyreLifePressure)}",
            $"Projected rejoin: P{context.ProjectedRejoinPosition}",
            $"Positions lost if pit: {context.PositionsLostIfPit}",
            "Track position currently more valuable than fresh tyres"
        });
}

private static StrategyRecommendation BuildUnavailableStrategyRecommendation(
    RaceCarSnapshot car,
    IReadOnlyList<RaceCarSnapshot> allCars)
{
    var context = BuildStrategyContext(car, allCars);

    return CreateRecommendation(
        context,
        "NO ADVICE",
        "Unavailable",
        "-",
        1.0,
        "Strategy Engine is unavailable. Recover the service before issuing pit, undercut, overcut or cover recommendations.",
        new[]
        {
            "Strategy Engine service failed",
            "Recommendation intentionally disabled",
            "Manual fallback required",
            "Recover services or rollback deployment"
        });
}

private static StrategyContext BuildStrategyContext(
    RaceCarSnapshot car,
    IReadOnlyList<RaceCarSnapshot> allCars)
{
    var carAhead = allCars.FirstOrDefault(other =>
        other.Position == car.Position - 1);

    var carBehind = allCars.FirstOrDefault(other =>
        other.Position == car.Position + 1);

    double? gapToCarAhead = carAhead is null
        ? null
        : Math.Max(0, car.GapToLeaderSeconds - carAhead.GapToLeaderSeconds);

    double? gapToCarBehind = carBehind is null
        ? null
        : Math.Max(0, carBehind.GapToLeaderSeconds - car.GapToLeaderSeconds);

    var projectedGapAfterPit = car.GapToLeaderSeconds + PitStopLossSeconds;

    var projectedRejoinPosition = allCars.Count(other =>
        other.Code != car.Code &&
        other.GapToLeaderSeconds < projectedGapAfterPit) + 1;

    var positionsLostIfPit = Math.Max(0, projectedRejoinPosition - car.Position);

    var tyreLifePressure = EstimateTyreLifePressure(car);
    var tyreTimeLossPerLap = EstimateTyreTimeLossPerLap(car);
    var recommendedPitTyre = RecommendPitTyre(car);

    var pitWindowScore = EstimatePitWindowScore(positionsLostIfPit);

    var undercutScore = EstimateUndercutScore(
        car,
        carAhead,
        gapToCarAhead,
        tyreLifePressure,
        pitWindowScore);

    var overcutScore = EstimateOvercutScore(
        car,
        carAhead,
        gapToCarAhead,
        tyreLifePressure);

    var coverThreatScore = EstimateCoverThreatScore(
        carBehind,
        gapToCarBehind,
        tyreLifePressure,
        pitWindowScore);

    var confidence = EstimateStrategyConfidence(
        tyreLifePressure,
        pitWindowScore,
        undercutScore,
        overcutScore,
        coverThreatScore);

    return new StrategyContext(
        car,
        carAhead,
        carBehind,
        GetRacePhase(car.Lap),
        recommendedPitTyre,
        projectedRejoinPosition,
        positionsLostIfPit,
        projectedGapAfterPit,
        gapToCarAhead,
        gapToCarBehind,
        tyreLifePressure,
        tyreTimeLossPerLap,
        pitWindowScore,
        undercutScore,
        overcutScore,
        coverThreatScore,
        confidence);
}

private static StrategyRecommendation CreateRecommendation(
    StrategyContext context,
    string action,
    string urgency,
    string recommendedTyre,
    double confidenceOverride,
    string reason,
    IReadOnlyList<string> factors)
{
    var confidence = Math.Clamp(
        (context.Confidence + confidenceOverride) / 2.0,
        0,
        1);

    return new StrategyRecommendation(
        context.Car.Code,
        context.Car.Driver,
        action,
        urgency,
        context.Car.Lap,
        context.RacePhase,
        context.Car.Tyre,
        context.Car.TyreAge,
        context.Car.GapToLeaderSeconds,
        recommendedTyre,
        context.ProjectedRejoinPosition,
        context.PositionsLostIfPit,
        PitStopLossSeconds,
        context.GapToCarAheadSeconds,
        context.GapToCarBehindSeconds,
        Math.Round(context.TyreLifePressure, 2),
        Math.Round(context.UndercutScore, 2),
        Math.Round(context.OvercutScore, 2),
        Math.Round(context.CoverThreatScore, 2),
        Math.Round(confidence, 2),
        reason,
        factors);
}

private static double EstimateTyreLifePressure(RaceCarSnapshot car)
{
    var riskStart = car.Tyre.ToLowerInvariant() switch
    {
        "soft" => 4,
        "medium" => 8,
        "hard" => 12,
        _ => 8
    };

    var cliffAge = car.Tyre.ToLowerInvariant() switch
    {
        "soft" => 12,
        "medium" => 18,
        "hard" => 24,
        _ => 18
    };

    if (car.TyreAge <= riskStart)
    {
        return 0;
    }

    return Math.Clamp(
        (double)(car.TyreAge - riskStart) / Math.Max(1, cliffAge - riskStart),
        0,
        1);
}

private static double EstimateTyreTimeLossPerLap(RaceCarSnapshot car)
{
    var pressure = EstimateTyreLifePressure(car);

    var maxLoss = car.Tyre.ToLowerInvariant() switch
    {
        "soft" => 2.4,
        "medium" => 1.8,
        "hard" => 1.3,
        _ => 1.6
    };

    return pressure * maxLoss;
}

private static double EstimatePitWindowScore(int positionsLostIfPit)
{
    return positionsLostIfPit switch
    {
        <= 0 => 1.00,
        1 => 0.86,
        2 => 0.68,
        3 => 0.46,
        4 => 0.28,
        _ => 0.14
    };
}

private static double EstimateUndercutScore(
    RaceCarSnapshot car,
    RaceCarSnapshot? carAhead,
    double? gapToCarAhead,
    double tyreLifePressure,
    double pitWindowScore)
{
    if (carAhead is null || !gapToCarAhead.HasValue)
    {
        return 0;
    }

    var gap = gapToCarAhead.Value;

    if (gap <= 0 || gap > 4.0)
    {
        return 0;
    }

    var gapScore = 1.0 - Math.Clamp(gap / 4.0, 0, 1);
    var tyreDeltaScore = Math.Clamp(tyreLifePressure + 0.15, 0, 1);

    return Math.Clamp(
        gapScore * 0.46 +
        tyreDeltaScore * 0.34 +
        pitWindowScore * 0.20,
        0,
        1);
}

private static double EstimateOvercutScore(
    RaceCarSnapshot car,
    RaceCarSnapshot? carAhead,
    double? gapToCarAhead,
    double tyreLifePressure)
{
    if (carAhead is null || !gapToCarAhead.HasValue)
    {
        return 0;
    }

    var gap = gapToCarAhead.Value;

    if (gap <= 0 || gap > 5.0)
    {
        return 0;
    }

    var ownTyrePressure = tyreLifePressure;
    var rivalTyrePressure = EstimateTyreLifePressure(carAhead);

    var tyreAdvantage = Math.Clamp(rivalTyrePressure - ownTyrePressure, 0, 1);
    var gapScore = 1.0 - Math.Clamp(gap / 5.0, 0, 1);

    return Math.Clamp(
        tyreAdvantage * 0.58 +
        gapScore * 0.32 +
        CompoundOvercutBias(car.Tyre) * 0.10,
        0,
        1);
}

private static double EstimateCoverThreatScore(
    RaceCarSnapshot? carBehind,
    double? gapToCarBehind,
    double tyreLifePressure,
    double pitWindowScore)
{
    if (carBehind is null || !gapToCarBehind.HasValue)
    {
        return 0;
    }

    var gap = gapToCarBehind.Value;

    if (gap <= 0 || gap > 3.5)
    {
        return 0;
    }

    var rivalThreat = 1.0 - Math.Clamp(gap / 3.5, 0, 1);

    return Math.Clamp(
        rivalThreat * 0.48 +
        tyreLifePressure * 0.32 +
        pitWindowScore * 0.20,
        0,
        1);
}

private static double EstimateStrategyConfidence(
    double tyreLifePressure,
    double pitWindowScore,
    double undercutScore,
    double overcutScore,
    double coverThreatScore)
{
    var strongestTacticalSignal = Math.Max(
        undercutScore,
        Math.Max(overcutScore, coverThreatScore));

    return Math.Clamp(
        0.50 +
        tyreLifePressure * 0.18 +
        pitWindowScore * 0.12 +
        strongestTacticalSignal * 0.20,
        0.35,
        0.96);
}

private static string RecommendPitTyre(RaceCarSnapshot car)
{
    var phase = GetRacePhase(car.Lap);

    if (phase == "Late race" && !car.Tyre.Equals("Soft", StringComparison.OrdinalIgnoreCase))
    {
        return "Soft";
    }

    return car.Tyre.ToLowerInvariant() switch
    {
        "soft" => "Medium",
        "medium" => car.Lap <= 10 ? "Hard" : "Soft",
        "hard" => car.Lap <= 12 ? "Medium" : "Soft",
        _ => NextTyre(car.Tyre)
    };
}

private static string GetRacePhase(int lap)
{
    return lap switch
    {
        <= 3 => "Opening phase",
        <= 8 => "First stint",
        <= 15 => "Pit window",
        <= 22 => "Second stint",
        _ => "Late race"
    };
}

private static double CompoundOvercutBias(string tyre)
{
    return tyre.ToLowerInvariant() switch
    {
        "hard" => 0.85,
        "medium" => 0.55,
        "soft" => 0.20,
        _ => 0.45
    };
}

private static string FormatPercent(double value)
{
    return $"{Math.Clamp(value, 0, 1) * 100:F0}%";
}

private static string FormatGap(double? gap)
{
    return gap.HasValue ? $"{gap.Value:F1}s" : "n/a";
}

    public async Task<OperationResult> PitCarAsync(string code, CancellationToken ct = default)
    {
        var events = new List<OperationsEvent>();
        var alerts = new List<Alert>();

        string carCode;
        string oldTyre;
        string newTyre;
        int pitLap;

        lock (_sync)
        {
            var now = DateTimeOffset.UtcNow;
            AdvanceRaceModel(now);

            var car = _cars.FirstOrDefault(c =>
                string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(car.Code))
            {
                throw new InvalidOperationException($"Unknown car code '{code}'.");
            }

            var runtime = _carRuntime[car.Code];

            carCode = car.Code;
            oldTyre = runtime.Tyre;
            newTyre = NextTyre(runtime.Tyre);
            pitLap = Math.Max(1, (int)Math.Floor(runtime.DistanceLaps) + 1);

            runtime.Tyre = newTyre;
runtime.LastPitLap = pitLap;

// PitStopLossSeconds is the full race-time loss.
// Because the simulator runs faster than real time, convert it into real seconds.
runtime.PitUntil = now.AddSeconds(PitStopLossSeconds / RaceSimulationTimeScale);

runtime.GapPenaltySeconds += PitStopLossSeconds;
runtime.LastSpeedKph = 0;

            alerts.Add(new Alert(
                Guid.NewGuid().ToString("N"),
                DateTimeOffset.UtcNow,
                AlertSeverity.Warning,
                "Pit stop",
                $"{carCode} pitted for {newTyre} tyres on lap {pitLap}.",
                "Tyre age reset and pit-loss applied to race gap."));
        }

        events.Add(await _eventStore.AppendEventAsync(
            EventType.AlertRaised,
            carCode,
            oldTyre,
            newTyre,
            $"{carCode} pit stop: {oldTyre} → {newTyre}; tyre age reset on lap {pitLap}",
            ct));

        return Result(events, alerts);
    }

    public async Task<OperationsEvent> LogRaceSnapshotAsync(CancellationToken ct = default)
    {
        return await _eventStore.AppendEventAsync(
            EventType.RaceSnapshotUpdated,
            "Track Map Stream",
            null,
            null,
            "Backend emitted race-car telemetry snapshot",
            ct);
    }

    public async Task<OperationResult> InjectTelemetryDelayAsync(CancellationToken ct = default)
    {
        var events = new List<OperationsEvent>();
        var alerts = new List<Alert>();
        Incident incident;

        lock (_sync)
        {
            TransitionServiceInMemory(
                "Telemetry Ingest",
                ServiceStatus.Degraded,
                850,
                1.5,
                "Telemetry packets delayed; car data may be stale");

            incident = CreateIncidentInMemory(
                "Telemetry Ingest",
                "Telemetry delay",
                "Track map and strategy inputs may lag behind the live session.");

            alerts.Add(new Alert(
                Guid.NewGuid().ToString("N"),
                DateTimeOffset.UtcNow,
                AlertSeverity.Warning,
                "Telemetry delay",
                "Car telemetry is delayed. Strategy advice should be treated as stale.",
                "Use timing feed as fallback until telemetry recovers."));
        }

        events.Add(await _eventStore.AppendEventAsync(
            EventType.ServiceHealthChanged,
            "Telemetry Ingest",
            ServiceStatus.Healthy.ToString(),
            ServiceStatus.Degraded.ToString(),
            "Telemetry Ingest changed Healthy → Degraded",
            ct));

        events.Add(await _eventStore.AppendEventAsync(
            EventType.IncidentCreated,
            incident.Service,
            null,
            IncidentStatus.Open.ToString(),
            $"Incident {incident.Id} created: {incident.Title}",
            ct));

        events.Add(await _eventStore.AppendEventAsync(
            EventType.AlertRaised,
            incident.Service,
            null,
            AlertSeverity.Warning.ToString(),
            "Telemetry delay alert raised",
            ct));

        await _eventStore.SaveIncidentAsync(incident, ct);

        return Result(events, alerts);
    }

    public async Task<OperationResult> InjectTimingPacketLossAsync(CancellationToken ct = default)
    {
        var events = new List<OperationsEvent>();
        var alerts = new List<Alert>();
        Incident incident;

        lock (_sync)
        {
            TransitionServiceInMemory(
                "Timing Feed Parser",
                ServiceStatus.Degraded,
                320,
                8.0,
                "Timing packets dropped; intervals are uncertain");

            incident = CreateIncidentInMemory(
                "Timing Feed Parser",
                "Timing packet loss",
                "Race gaps and ordering may contain temporary uncertainty.");

            alerts.Add(new Alert(
                Guid.NewGuid().ToString("N"),
                DateTimeOffset.UtcNow,
                AlertSeverity.Warning,
                "Timing uncertainty",
                "Timing packet loss detected. The timing tower is marked uncertain.",
                "Hold deployment changes and wait for feed recovery."));
        }

        events.Add(await _eventStore.AppendEventAsync(
            EventType.ServiceHealthChanged,
            "Timing Feed Parser",
            ServiceStatus.Healthy.ToString(),
            ServiceStatus.Degraded.ToString(),
            "Timing Feed Parser changed Healthy → Degraded",
            ct));

        events.Add(await _eventStore.AppendEventAsync(
            EventType.IncidentCreated,
            incident.Service,
            null,
            IncidentStatus.Open.ToString(),
            $"Incident {incident.Id} created: {incident.Title}",
            ct));

        events.Add(await _eventStore.AppendEventAsync(
            EventType.AlertRaised,
            incident.Service,
            null,
            AlertSeverity.Warning.ToString(),
            "Timing packet-loss alert raised",
            ct));

        await _eventStore.SaveIncidentAsync(incident, ct);

        return Result(events, alerts);
    }

    public async Task<OperationResult> FailStrategyEngineAsync(CancellationToken ct = default)
    {
        var events = new List<OperationsEvent>();
        var alerts = new List<Alert>();
        Incident incident;

        lock (_sync)
        {
            TransitionServiceInMemory(
                "Strategy Engine",
                ServiceStatus.Failed,
                0,
                100.0,
                "Strategy recommendations unavailable");

            incident = CreateIncidentInMemory(
                "Strategy Engine",
                "Strategy engine unavailable",
                "Pit-window and risk alerts are disabled for Alpine cars.");

            alerts.Add(new Alert(
                Guid.NewGuid().ToString("N"),
                DateTimeOffset.UtcNow,
                AlertSeverity.Critical,
                "Strategy engine down",
                "Strategy recommendations are unavailable.",
                "Rollback recent strategy deployment or switch to manual fallback."));
        }

        events.Add(await _eventStore.AppendEventAsync(
            EventType.ServiceHealthChanged,
            "Strategy Engine",
            ServiceStatus.Healthy.ToString(),
            ServiceStatus.Failed.ToString(),
            "Strategy Engine changed Healthy → Failed",
            ct));

        events.Add(await _eventStore.AppendEventAsync(
            EventType.IncidentCreated,
            incident.Service,
            null,
            IncidentStatus.Open.ToString(),
            $"Incident {incident.Id} created: {incident.Title}",
            ct));

        events.Add(await _eventStore.AppendEventAsync(
            EventType.AlertRaised,
            incident.Service,
            null,
            AlertSeverity.Critical.ToString(),
            "Strategy engine failure alert raised",
            ct));

        await _eventStore.SaveIncidentAsync(incident, ct);

        return Result(events, alerts);
    }

    public async Task<OperationResult> RecoverAllAsync(CancellationToken ct = default)
    {
        var events = new List<OperationsEvent>();
        var resolved = new List<Incident>();

        lock (_sync)
        {
            foreach (var serviceName in _services.Keys.ToList())
            {
                var service = _services[serviceName];

                if (service.Status == ServiceStatus.Healthy)
                {
                    continue;
                }

                _services[serviceName] = service with
                {
                    Status = ServiceStatus.Healthy,
                    LatencyMs = serviceName.Contains("Strategy") ? 64 : 42,
                    PacketLossPercent = 0,
                    Message = "Recovered and healthy",
                    UpdatedAt = DateTimeOffset.UtcNow
                };
            }

            foreach (var incident in _incidents.Values.Where(i => i.Status != IncidentStatus.Resolved).ToList())
            {
                var updated = incident with
                {
                    Status = IncidentStatus.Resolved,
                    ResolvedAt = DateTimeOffset.UtcNow
                };

                _incidents[incident.Id] = updated;
                resolved.Add(updated);
            }
        }

        events.Add(await _eventStore.AppendEventAsync(
            EventType.ServiceHealthChanged,
            "All services",
            null,
            ServiceStatus.Healthy.ToString(),
            "All degraded or failed services recovered",
            ct));

        foreach (var incident in resolved)
        {
            await _eventStore.SaveIncidentAsync(incident, ct);

            events.Add(await _eventStore.AppendEventAsync(
                EventType.IncidentResolved,
                incident.Service,
                IncidentStatus.Open.ToString(),
                IncidentStatus.Resolved.ToString(),
                $"Incident {incident.Id} resolved",
                ct));
        }

        return Result(events, Array.Empty<Alert>());
    }

    public async Task<OperationResult> ResetRaceAsync(CancellationToken ct = default)
    {
        OperationsEvent evt;

        lock (_sync)
        {
            var now = DateTimeOffset.UtcNow;

            _raceClock.Restart();
            _lastRaceModelUpdate = now;

            for (var index = 0; index < _cars.Count; index++)
            {
                var car = _cars[index];
                var runtime = _carRuntime[car.Code];

                runtime.Tyre = car.Tyre;
                runtime.LastPitLap = 1;
                runtime.GapPenaltySeconds = 0;
                runtime.PitUntil = null;
                runtime.DistanceLaps = 0;
                runtime.LastSpeedKph = 0;
            }

            foreach (var serviceName in _services.Keys.ToList())
            {
                var service = _services[serviceName];

                _services[serviceName] = service with
                {
                    Status = ServiceStatus.Healthy,
                    LatencyMs = serviceName.Contains("Strategy", StringComparison.OrdinalIgnoreCase) ? 64 : 42,
                    PacketLossPercent = 0,
                    Message = "Reset and healthy",
                    UpdatedAt = now
                };
            }

            _incidents.Clear();

            _deployment = _deployment with
            {
                Status = DeploymentStatus.Idle,
                CanaryPercent = 0,
                Message = "No deployment in progress",
                UpdatedAt = now
            };
        }

        evt = await _eventStore.AppendEventAsync(
            EventType.RaceSnapshotUpdated,
            "Race Session",
            null,
            null,
            "Race session reset",
            ct);

        return Result(new[] { evt }, Array.Empty<Alert>());
    }

    public async Task<OperationResult> StartCanaryAsync(CancellationToken ct = default)
    {
        OperationsEvent evt;
        string service;

        lock (_sync)
        {
            if (_deployment.Status == DeploymentStatus.Canary)
            {
                throw new InvalidOperationException("Canary deployment is already running.");
            }

            _deployment = _deployment with
            {
                Status = DeploymentStatus.Canary,
                CanaryPercent = 20,
                Message = "20% canary receiving traffic",
                UpdatedAt = DateTimeOffset.UtcNow
            };

            service = _deployment.Service;
        }

        evt = await _eventStore.AppendEventAsync(
            EventType.DeploymentStarted,
            service,
            DeploymentStatus.Idle.ToString(),
            DeploymentStatus.Canary.ToString(),
            "Canary deployment started at 20%",
            ct);

        return Result(new[] { evt }, Array.Empty<Alert>());
    }

    public async Task<OperationResult> PromoteDeploymentAsync(CancellationToken ct = default)
    {
        OperationsEvent evt;
        string service;

        lock (_sync)
        {
            if (_deployment.Status != DeploymentStatus.Canary)
            {
                throw new InvalidOperationException("Cannot promote because no canary deployment is active.");
            }

            _deployment = _deployment with
            {
                Status = DeploymentStatus.Promoted,
                CanaryPercent = 100,
                CurrentVersion = _deployment.CandidateVersion,
                Message = "Candidate promoted to 100%",
                UpdatedAt = DateTimeOffset.UtcNow
            };

            service = _deployment.Service;
        }

        evt = await _eventStore.AppendEventAsync(
            EventType.DeploymentPromoted,
            service,
            DeploymentStatus.Canary.ToString(),
            DeploymentStatus.Promoted.ToString(),
            "Canary deployment promoted",
            ct);

        return Result(new[] { evt }, Array.Empty<Alert>());
    }

    public async Task<OperationResult> RollbackDeploymentAsync(CancellationToken ct = default)
    {
        OperationsEvent evt;
        string service;

        lock (_sync)
        {
            if (_deployment.Status != DeploymentStatus.Canary && _deployment.Status != DeploymentStatus.Promoted)
            {
                throw new InvalidOperationException("Cannot rollback because no active or promoted deployment exists.");
            }

            _deployment = _deployment with
            {
                Status = DeploymentStatus.RolledBack,
                CanaryPercent = 0,
                Message = "Rolled back to stable version",
                UpdatedAt = DateTimeOffset.UtcNow
            };

            service = _deployment.Service;
        }

        evt = await _eventStore.AppendEventAsync(
            EventType.DeploymentRolledBack,
            service,
            null,
            DeploymentStatus.RolledBack.ToString(),
            "Deployment rolled back",
            ct);

        return Result(new[] { evt }, Array.Empty<Alert>());
    }

    private OperationResult Result(IEnumerable<OperationsEvent> events, IEnumerable<Alert> alerts)
    {
        return new OperationResult(
            GetServices(),
            GetIncidents(),
            GetDeployment(),
            events.ToList(),
            alerts.ToList());
    }

  private void AdvanceRaceModel(DateTimeOffset now)
{
    var elapsedSeconds = Math.Clamp(
        (now - _lastRaceModelUpdate).TotalSeconds,
        0,
        2);

    _lastRaceModelUpdate = now;

    if (elapsedSeconds <= 0)
    {
        return;
    }

    var modelElapsedSeconds = elapsedSeconds * RaceSimulationTimeScale;

    var telemetrySlow = _services.Values.Any(service =>
        service.Name.Contains("Telemetry", StringComparison.OrdinalIgnoreCase) &&
        service.Status == ServiceStatus.Degraded);

    var strategyDown = _services.Values.Any(service =>
        service.Name.Contains("Strategy", StringComparison.OrdinalIgnoreCase) &&
        service.Status == ServiceStatus.Failed);

    var orderedBeforeAdvance = _cars
        .Select((car, index) => new
        {
            Car = car,
            Index = index,
            Runtime = _carRuntime[car.Code],
            DistanceLaps = _carRuntime[car.Code].DistanceLaps
        })
        .OrderByDescending(entry => entry.DistanceLaps)
        .ThenBy(entry => entry.Index)
        .ToList();

    var raceContext = new Dictionary<string, (int Position, double? GapAhead, double? GapBehind)>(
        StringComparer.OrdinalIgnoreCase);

    for (var index = 0; index < orderedBeforeAdvance.Count; index++)
    {
        var entry = orderedBeforeAdvance[index];

        var ahead = index > 0
            ? orderedBeforeAdvance[index - 1]
            : null;

        var behind = index < orderedBeforeAdvance.Count - 1
            ? orderedBeforeAdvance[index + 1]
            : null;

        double? gapAhead = ahead is null
    ? null
    : EstimateGapSeconds(ahead.DistanceLaps, entry.DistanceLaps);

double? gapBehind = behind is null
    ? null
    : EstimateGapSeconds(entry.DistanceLaps, behind.DistanceLaps);

        raceContext[entry.Car.Code] = (index + 1, gapAhead, gapBehind);
    }

    foreach (var car in _cars)
    {
        var runtime = _carRuntime[car.Code];

        var lap = Math.Max(1, (int)Math.Floor(runtime.DistanceLaps) + 1);
        var tyreAge = Math.Max(0, lap - runtime.LastPitLap);

        var context = raceContext[car.Code];

        var performanceFactor = BuildPerformanceFactor(
            car.Team,
            runtime,
            tyreAge,
            now,
            telemetrySlow,
            strategyDown);

        var scenarioFactor = BuildRaceScenarioFactor(
            car.Code,
            lap,
            tyreAge,
            context.Position,
            context.GapAhead,
            context.GapBehind);

        var lapsPerSecond =
            car.BaseSpeed *
            performanceFactor *
            scenarioFactor /
            ReferenceLapTimeSeconds;

        runtime.DistanceLaps = Math.Max(
            0,
            runtime.DistanceLaps + lapsPerSecond * modelElapsedSeconds);

        runtime.LastSpeedKph = Math.Round(lapsPerSecond * CircuitLengthKm * 3600);
    }
}

    private static double BuildPerformanceFactor(
        string team,
        CarRuntimeState runtime,
        int tyreAge,
        DateTimeOffset now,
        bool telemetrySlow,
        bool strategyDown)
    {
        if (runtime.PitUntil is not null && runtime.PitUntil > now)
        {
            return 0;
        }

        var compoundFactor = runtime.Tyre.ToLowerInvariant() switch
        {
            "soft" => 1.018,
            "medium" => 1.000,
            "hard" => 0.986,
            _ => 1.000
        };

        var degradationStart = runtime.Tyre.ToLowerInvariant() switch
        {
            "soft" => 6,
            "medium" => 10,
            "hard" => 14,
            _ => 10
        };

        var degradationRate = runtime.Tyre.ToLowerInvariant() switch
        {
            "soft" => 0.008,
            "medium" => 0.005,
            "hard" => 0.003,
            _ => 0.005
        };

        var tyreDegradation = Math.Max(0, tyreAge - degradationStart) * degradationRate;
        var tyreFactor = Math.Max(0.86, 1.0 - tyreDegradation);

        var reliabilityFactor = 1.0;

        if (strategyDown && team.Equals("Alpine", StringComparison.OrdinalIgnoreCase))
        {
            reliabilityFactor *= 0.975;
        }

        if (telemetrySlow && team.Equals("Alpine", StringComparison.OrdinalIgnoreCase))
        {
            reliabilityFactor *= 0.992;
        }

        return compoundFactor * tyreFactor * reliabilityFactor;
    }
private double BuildRaceScenarioFactor(
    string code,
    int lap,
    int tyreAge,
    int position,
    double? gapAheadSeconds,
    double? gapBehindSeconds)
{
    var profile = _driverProfiles.TryGetValue(code, out var foundProfile)
        ? foundProfile
        : new DriverRaceProfile(1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 4, 8);

    var factor = 1.0;

    if (lap <= 1)
    {
        factor *= profile.LaunchFactor;
    }
    else if (lap <= 3)
    {
        factor *= profile.EarlyPaceFactor;
    }

    if (lap >= profile.AttackLapStart && lap <= profile.AttackLapEnd)
    {
        factor *= profile.AttackPaceFactor;
    }
    else if (lap > profile.AttackLapEnd)
    {
        factor *= profile.LatePaceFactor;
    }

    // Better tyre managers lose less pace as the stint gets older.
    var tyreManagementEffect = Math.Max(0, tyreAge - 6) *
                               (profile.TyreManagementFactor - 1.0) *
                               0.18;

    factor *= 1.0 + Math.Clamp(tyreManagementEffect, -0.025, 0.025);

    // DRS/slipstream-style attack if close enough to the car ahead.
    if (lap >= 2 &&
        gapAheadSeconds.HasValue &&
        gapAheadSeconds.Value <= 1.0 &&
        gapAheadSeconds.Value > 0.12)
    {
        factor *= 1.0 + 0.012 * profile.RaceCraftFactor;
    }

    // Dirty air if close but not close enough to pass.
    if (gapAheadSeconds.HasValue &&
        gapAheadSeconds.Value > 1.0 &&
        gapAheadSeconds.Value <= 2.0)
    {
        factor *= 0.997;
    }

    // Stuck behind another car through corners.
    if (gapAheadSeconds.HasValue &&
        gapAheadSeconds.Value <= 0.35)
    {
        factor *= 0.994;
    }

    // Defending from a close car behind costs lap time.
    if (gapBehindSeconds.HasValue &&
        gapBehindSeconds.Value <= 0.8)
    {
        factor *= 0.997;
    }

    factor *= BuildRaceDramaFactor(code, lap, position, tyreAge);

    return Math.Clamp(factor, 0.94, 1.06);
}

private static double BuildRaceDramaFactor(
    string code,
    int lap,
    int position,
    int tyreAge)
{
    // Deterministic race story, not random noise.
    // This creates recognizable F1-style phases:
    // launch, tyre saving, attack windows, fading soft tyres, and late charges.

    if (code.Equals("RBR", StringComparison.OrdinalIgnoreCase) && lap >= 5)
    {
        return tyreAge >= 5 ? 0.982 : 0.990;
    }

    if (code.Equals("WIL", StringComparison.OrdinalIgnoreCase) && lap >= 4)
    {
        return 0.965;
    }

    if (code.Equals("ALP1", StringComparison.OrdinalIgnoreCase) && lap is >= 3 and <= 7)
    {
        return 1.014;
    }

    if (code.Equals("ALP2", StringComparison.OrdinalIgnoreCase) && lap >= 6)
    {
        return 1.012;
    }

    if (code.Equals("MCL", StringComparison.OrdinalIgnoreCase) && lap >= 5)
    {
        return 1.016;
    }

    if (code.Equals("FER", StringComparison.OrdinalIgnoreCase) && lap is >= 2 and <= 5)
    {
        return 1.008;
    }

    if (code.Equals("MER", StringComparison.OrdinalIgnoreCase) && lap >= 5 && position >= 4)
    {
        return 1.010;
    }

    return 1.0;
}

private static double EstimateGapSeconds(double aheadDistanceLaps, double behindDistanceLaps)
{
    return Math.Max(0, aheadDistanceLaps - behindDistanceLaps) * ReferenceLapTimeSeconds;
}
    private void TransitionServiceInMemory(
        string serviceName,

        ServiceStatus status,
        int latencyMs,
        double packetLoss,
        string message)
    {
        var service = _services[serviceName];

        _services[serviceName] = service with
        {
            Status = status,
            LatencyMs = latencyMs,
            PacketLossPercent = packetLoss,
            Message = message,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private Incident CreateIncidentInMemory(string service, string title, string impact)
    {
        var id = $"INC-{++_incidentCounter:000}";
        var incident = new Incident(
            id,
            service,
            title,
            impact,
            IncidentStatus.Open,
            DateTimeOffset.UtcNow,
            null);

        _incidents[id] = incident;

        return incident;
    }

    private int GetCurrentLap()
    {
        lock (_sync)
        {
            if (!_carRuntime.Any())
            {
                return 1;
            }

            return Math.Max(
                1,
                (int)Math.Floor(_carRuntime.Values.Max(car => car.DistanceLaps)) + 1);
        }
    }

    private string GetMode()
    {
        lock (_sync)
        {
            if (_services.Values.Any(s => s.Status == ServiceStatus.Failed))
            {
                return "Failed service fallback";
            }

            if (_services.Values.Any(s => s.Status == ServiceStatus.Degraded))
            {
                return "Degraded operations";
            }

            return "Normal operations";
        }
    }
    

    private static double NormalizeProgress(double value)
    {
        var normalized = value % 1.0;
        return normalized < 0 ? normalized + 1.0 : normalized;
    }
private void ApplyDemoRaceState(IReadOnlyList<DemoCarState> demoCars)
{
    const double leaderDistanceLaps = 18.25;

    foreach (var demoCar in demoCars)
    {
        if (!_carRuntime.TryGetValue(demoCar.Code, out var runtime))
        {
            continue;
        }

        runtime.Tyre = demoCar.Tyre;
        runtime.PitUntil = null;
        runtime.GapPenaltySeconds = 0;

        runtime.DistanceLaps = Math.Max(
            0,
            leaderDistanceLaps - demoCar.GapToLeaderSeconds / ReferenceLapTimeSeconds);

        var currentLap = Math.Max(1, (int)Math.Floor(runtime.DistanceLaps) + 1);

        runtime.LastPitLap = Math.Max(
            1,
            currentLap - Math.Max(0, demoCar.TyreAge));

        runtime.LastSpeedKph = 240;
    }
}
    private static string NextTyre(string tyre)
    {
        return tyre.ToLowerInvariant() switch
        {
            "soft" => "Medium",
            "medium" => "Hard",
            "hard" => "Medium",
            _ => "Medium"
        };
    }
}