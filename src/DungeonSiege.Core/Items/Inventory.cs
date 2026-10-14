namespace DungeonSiege.Core.Items;

/// <summary>Shared party pack (the pack mule comes later in the chapter...).</summary>
public sealed class Inventory
{
    private readonly Dictionary<string, int> _counts = [];

    public IReadOnlyDictionary<string, int> Counts => _counts;

    public int Count(string itemId) => _counts.GetValueOrDefault(itemId);

    public void Add(string itemId, int amount = 1)
    {
        if (amount <= 0) return;
        _counts[itemId] = Count(itemId) + amount;
    }

    public bool TryConsume(string itemId)
    {
        var count = Count(itemId);
        if (count == 0) return false;
        _counts[itemId] = count - 1;
        return true;
    }
}
