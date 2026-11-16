using System.Collections.Frozen;

namespace Caffeine;

/// <summary>Kinds of caffeinated drinks the utility knows about.</summary>
public enum CaffeineType
{
    Espresso,
    BrewedCoffee,
    ColdBrew,
    InstantCoffee,
    DecafCoffee,
    BlackTea,
    GreenTea,
    Matcha,
    EnergyDrink,
    Cola
}

/// <summary>Static metadata for a caffeine type. Values are approximate averages.</summary>
public sealed record CaffeineInfo(
    CaffeineType Type,
    string DisplayName,
    double MgPer100Ml,
    int DefaultServingMl,
    params string[] Aliases);

/// <summary>Result of checking intake against a daily limit.</summary>
public readonly record struct IntakeStatus(double TotalMg, double LimitMg)
{
    public double RemainingMg => Math.Max(0, LimitMg - TotalMg);
    public bool IsOverLimit => TotalMg > LimitMg;
}

public static class CaffeineTypeUtility
{
    /// <summary>Commonly cited safe daily limit for healthy adults (mg).</summary>
    public const double DefaultDailyLimitMg = 400;

    /// <summary>Approximate caffeine half-life in healthy adults (hours).</summary>
    public const double DefaultHalfLifeHours = 5;

    private static readonly FrozenDictionary<CaffeineType, CaffeineInfo> Registry =
        new CaffeineInfo[]
        {
            new(CaffeineType.Espresso,      "Espresso",       212, 30,  "shot", "espresso shot"),
            new(CaffeineType.BrewedCoffee,  "Brewed Coffee",   40, 240, "coffee", "drip", "filter coffee"),
            new(CaffeineType.ColdBrew,      "Cold Brew",       50, 355, "cold brew", "coldbrew"),
            new(CaffeineType.InstantCoffee, "Instant Coffee",  30, 240, "instant", "nescafe"),
            new(CaffeineType.DecafCoffee,   "Decaf Coffee",     2, 240, "decaf"),
            new(CaffeineType.BlackTea,      "Black Tea",       20, 240, "tea", "chai", "black tea"),
            new(CaffeineType.GreenTea,      "Green Tea",       12, 240, "green tea"),
            new(CaffeineType.Matcha,        "Matcha",          30, 240, "matcha"),
            new(CaffeineType.EnergyDrink,   "Energy Drink",    32, 250, "energy", "energy drink", "redbull"),
            new(CaffeineType.Cola,          "Cola",            10, 330, "cola", "soda", "coke"),
        }.ToFrozenDictionary(i => i.Type);

    private static readonly FrozenDictionary<string, CaffeineType> AliasMap = BuildAliasMap();

    private static FrozenDictionary<string, CaffeineType> BuildAliasMap()
    {
        var map = new Dictionary<string, CaffeineType>(StringComparer.OrdinalIgnoreCase);
        foreach (var info in Registry.Values)
        {
            map[Normalize(info.Type.ToString())] = info.Type;
            map[Normalize(info.DisplayName)] = info.Type;
            foreach (var alias in info.Aliases)
                map[Normalize(alias)] = info.Type;
        }
        return map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static string Normalize(string s) =>
        new(s.Where(char.IsLetterOrDigit).ToArray());

    // ---------- Lookup ----------

    public static CaffeineInfo GetInfo(this CaffeineType type) =>
        Registry.TryGetValue(type, out var info)
            ? info
            : throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown caffeine type.");

    public static IReadOnlyCollection<CaffeineInfo> All() => Registry.Values;

    public static string GetDisplayName(this CaffeineType type) => type.GetInfo().DisplayName;

    // ---------- Parsing ----------

    /// <summary>Parses names, display names and aliases ("Cold Brew", "chai", "shot").</summary>
    public static bool TryParse(string? text, out CaffeineType type)
    {
        type = default;
        return !string.IsNullOrWhiteSpace(text) && AliasMap.TryGetValue(Normalize(text), out type);
    }

    public static CaffeineType Parse(string text) =>
        TryParse(text, out var t)
            ? t
            : throw new FormatException($"'{text}' is not a recognised caffeine type.");

    // ---------- Calculations ----------

    /// <summary>Caffeine (mg) for an explicit volume in millilitres.</summary>
    public static double GetCaffeineMg(this CaffeineType type, double volumeMl)
    {
        if (volumeMl < 0) throw new ArgumentOutOfRangeException(nameof(volumeMl));
        return type.GetInfo().MgPer100Ml * volumeMl / 100.0;
    }

    /// <summary>Caffeine (mg) for the default serving size.</summary>
    public static double GetCaffeineMgPerServing(this CaffeineType type) =>
        type.GetCaffeineMg(type.GetInfo().DefaultServingMl);

    /// <summary>Total caffeine across servings, e.g. [(Espresso, 2), (Cola, 1)].</summary>
    public static double TotalMg(IEnumerable<(CaffeineType Type, int Servings)> intake) =>
        intake.Sum(i => i.Type.GetCaffeineMgPerServing() * i.Servings);

    public static IntakeStatus CheckIntake(
        IEnumerable<(CaffeineType Type, int Servings)> intake,
        double dailyLimitMg = DefaultDailyLimitMg) =>
        new(TotalMg(intake), dailyLimitMg);

    /// <summary>Caffeine still in the body after the given hours (first-order decay).</summary>
    public static double RemainingAfter(
        double doseMg, double hoursElapsed, double halfLifeHours = DefaultHalfLifeHours)
    {
        if (halfLifeHours <= 0) throw new ArgumentOutOfRangeException(nameof(halfLifeHours));
        return doseMg * Math.Pow(0.5, hoursElapsed / halfLifeHours);
    }
}
