using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DungeonSiege.Core.Content;

/// <summary>Immutable, read-optimised lookup of all chapter content.</summary>
public sealed class ContentDatabase
{
    public ChapterDefinition Chapter { get; }
    public FrozenDictionary<string, ItemDefinition> Items { get; }
    public FrozenDictionary<string, CharacterTemplate> Characters { get; }
    public FrozenDictionary<string, EnemyTemplate> Enemies { get; }
    public FrozenDictionary<string, DialogueDefinition> Dialogues { get; }
    public FrozenDictionary<string, QuestDefinition> Quests { get; }
    public IReadOnlyList<RegionDefinition> Regions { get; }

    public ContentDatabase(
        ChapterDefinition chapter,
        IEnumerable<ItemDefinition> items,
        IEnumerable<CharacterTemplate> characters,
        IEnumerable<EnemyTemplate> enemies,
        IEnumerable<RegionDefinition> regions,
        IEnumerable<DialogueDefinition> dialogues,
        IEnumerable<QuestDefinition> quests)
    {
        Chapter = chapter;
        Items = items.ToFrozenDictionary(i => i.Id);
        Characters = characters.ToFrozenDictionary(c => c.Id);
        Enemies = enemies.ToFrozenDictionary(e => e.Id);
        Regions = regions.ToArray();
        Dialogues = dialogues.ToFrozenDictionary(d => d.Id);
        Quests = quests.ToFrozenDictionary(q => q.Id);
        Validate();
    }

    public RegionDefinition Region(string id) => Regions.First(r => r.Id == id);

    /// <summary>Loads Chapter 1 from the JSON embedded in this assembly.</summary>
    public static ContentDatabase LoadEmbedded() => Load(name =>
    {
        var asm = typeof(ContentDatabase).Assembly;
        return asm.GetManifestResourceStream($"DungeonSiege.Content.{name}")
               ?? throw new InvalidOperationException($"Missing embedded content '{name}'.");
    });

    /// <summary>Loads content from a folder on disk (mod / designer override).</summary>
    public static ContentDatabase LoadFromDirectory(string directory) =>
        Load(name => File.OpenRead(Path.Combine(directory, name)));

    private static ContentDatabase Load(Func<string, Stream> open)
    {
        return new ContentDatabase(
            Read<ChapterDefinition>(open, "chapter1.json"),
            Read<List<ItemDefinition>>(open, "items.json"),
            Read<List<CharacterTemplate>>(open, "characters.json"),
            Read<List<EnemyTemplate>>(open, "enemies.json"),
            Read<List<RegionDefinition>>(open, "regions.json"),
            Read<List<DialogueDefinition>>(open, "dialogue.json"),
            Read<List<QuestDefinition>>(open, "quests.json"));
    }

    // Reflection-based on purpose: the source generator assigns default(T) to absent
    // init-only members, which would wipe the defaults declared on the content records.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static T Read<T>(Func<string, Stream> open, string name)
    {
        using var stream = open(name);
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
               ?? throw new InvalidDataException($"Content file '{name}' is empty.");
    }

    /// <summary>Fail fast on broken cross references rather than at runtime mid-chapter.</summary>
    private void Validate()
    {
        var errors = new List<string>();
        if (!Characters.ContainsKey(Chapter.Protagonist)) errors.Add($"chapter: unknown protagonist '{Chapter.Protagonist}'");
        foreach (var c in Characters.Values)
            foreach (var item in c.Loadout.Values.Where(i => !Items.ContainsKey(i)))
                errors.Add($"character {c.Id}: unknown item '{item}'");
        foreach (var r in Regions)
        {
            foreach (var s in r.Spawns.Where(s => !Enemies.ContainsKey(s.Enemy)))
                errors.Add($"region {r.Id}: spawn {s.Id} references unknown enemy '{s.Enemy}'");
            foreach (var n in r.Npcs.Where(n => n.Dialogue is not null && !Dialogues.ContainsKey(n.Dialogue)))
                errors.Add($"region {r.Id}: npc {n.Id} references unknown dialogue '{n.Dialogue}'");
            foreach (var t in r.Triggers.Where(t => t.Dialogue is not null && !Dialogues.ContainsKey(t.Dialogue)))
                errors.Add($"region {r.Id}: trigger {t.Id} references unknown dialogue '{t.Dialogue}'");
        }
        foreach (var d in Dialogues.Values)
        {
            if (d.Node(d.Start) is null) errors.Add($"dialogue {d.Id}: missing start node '{d.Start}'");
            foreach (var c in d.Nodes.SelectMany(n => n.Choices).Where(c => c.Next is not null && d.Node(c.Next) is null))
                errors.Add($"dialogue {d.Id}: choice '{c.Text}' points at missing node '{c.Next}'");
        }
        if (errors.Count > 0)
            throw new InvalidDataException("Content validation failed:\n  " + string.Join("\n  ", errors));
    }
}
