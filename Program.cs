using System.Text.Json;
using System.Text.Json.Serialization;

namespace GroupsShuffle;

internal static class Randomizer
{
    public static readonly Random Random = new Random();
}


// I wanted to publish a single-exe version. That for some reason didn't work w/o source generators and AOT. AOT doesn't work without source generation ¯\_(ツ)_/¯
[JsonSerializable(typeof(Settings))]
partial class MyJsonContext : JsonSerializerContext
{
}

internal class Program
{
    public static void Main()
    {
        var settings = (Settings)JsonSerializer.Deserialize(File.ReadAllText("./settings.json"), typeof(Settings),
            new MyJsonContext(new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() }}))!;

        var groups = settings.GroupSizes.Select(groupSize => new Group{ExpectedSize = groupSize}).ToList();
        var squads = settings.Squads;

        foreach (var group in groups)
        {
            var remainingMembers = squads.Sum(squad => squad.Members.Count);
            if (remainingMembers <= group.ExpectedSize)
            {
                foreach (var person in squads.SelectMany(squad => squad.Members))
                {
                    group.Members.Add(person);
                }
            }
            else
            {
                for (var fillCounter = 0; fillCounter < group.ExpectedSize; fillCounter++)
                {
                    var nextSquad = PickNextSquadIndex(squads, group.SourceGroupIndexes, settings.SameSquadWeightMultiplier);
                    group.SourceGroupIndexes.Add(nextSquad);
                    group.Members.Add(squads[nextSquad].PickRandomMember());
                }
            }
        }

        foreach (var group in groups)
        {
            Printer.Print(group.Members, squads);
        }
    }

    private static int PickNextSquadIndex(List<Squad> squads, List<int> currentPicks, int sameSquadWeightMultiplier) => 
        ChoseRandomSquadBasedOnProportions(GetProportionalRanges(squads, CalculateRelativeWeights(squads, currentPicks, sameSquadWeightMultiplier)));

    private static int ChoseRandomSquadBasedOnProportions(List<(int start, int end, int squadIndex)> ranges)
    {
        var randomNumber = Randomizer.Random.Next(0, ranges.Last().end);
        foreach (var (start, end, squadIndex) in ranges)
        {
            if (start <= randomNumber && randomNumber < end)
            {
                return squadIndex;
            }
        }
        return 0;
    }

    private static List<(int start, int end, int squadIndex)> GetProportionalRanges(List<Squad> squads, double[] weights)
    {
        var currentStart = 0;
        var ranges = new List<(int start, int end, int squadIndex)>();
        for (var squadIndex = 0; squadIndex < squads.Count; squadIndex++)
        {
            // multiplication by 100 is so we could nicely round to ints and use Random.NextInt
            var finalWeight = (int)Math.Round(100 * weights[squadIndex] * squads[squadIndex].Members.Count);
            if (finalWeight == 0)
            {
                continue;
            }

            var range = (start: currentStart, end: (currentStart + finalWeight), squadIndex: squadIndex);
            currentStart = range.end;
            ranges.Add(range);
        }
        return ranges;
    }

    private static double[] CalculateRelativeWeights(List<Squad> squads, List<int> currentPicks, int sameSquadWeightMultiplier)
    {
        var weights = new double[squads.Count];
        for (var squadIndex = 0; squadIndex < squads.Count; squadIndex++)
        {
            var alreadyPickedFromSquad = currentPicks.Count(pickedSquadIndex => pickedSquadIndex == squadIndex);
            weights[squadIndex] = GetWeightRelativeToTimesPicked(alreadyPickedFromSquad, sameSquadWeightMultiplier);
        }
        return weights;
    }

    private static double GetWeightRelativeToTimesPicked(double value, int valueMultiplier) => 
        1/(valueMultiplier * value + 1);
}

class Settings
{
    public List<Squad> Squads { get; set; }

    public List<int> GroupSizes { get; set; }

    public int SameSquadWeightMultiplier { get; set;  }
}

class Squad
{
    private List<string> members;
    public string Name { get; set; }

    public List<string> Members
    {
        get => members;
        set
        {
            InitialMembers = value.ToList();
            members = value;
        }
    }

    [JsonIgnore]
    public List<string> InitialMembers {get; set; }

    public ConsoleColor Color { get; set; }

    public string PickRandomMember()
    {
        if(Members.Count == 0)
            throw new ArgumentException();
        var index = Randomizer.Random.Next(0, Members.Count);
        var member = Members[index];
        Members.RemoveAt(index);
        return member;
    }
}

class Group
{
    public int ExpectedSize { get; set; }
    public List<int> SourceGroupIndexes { get; } = new();
    public List<string> Members { get;} = new();
}


static class Printer
{
    public static void Print(List<string> groupMembers, List<Squad> squads)
    {
        var defaultColor = Console.ForegroundColor;
        for (var i = 0; i < groupMembers.Count - 1; i++)
        {
            Console.ForegroundColor = GetColor(groupMembers[i], squads, defaultColor);
            Console.Write(groupMembers[i]);
            Console.ForegroundColor = defaultColor;
            Console.Write(", ");
        }

        Console.ForegroundColor = GetColor(groupMembers[^1], squads, defaultColor);
        Console.Write(groupMembers[^1]);
        Console.WriteLine();
        Console.ForegroundColor = defaultColor;
    }

    private static ConsoleColor GetColor(string name, List<Squad> squads, ConsoleColor defaultColor)
    {
        return squads.FirstOrDefault(x => x.InitialMembers.Contains(name))?.Color ?? defaultColor;
    }
}