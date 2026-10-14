using DungeonSiege.Core.Content;

namespace DungeonSiege.Core.Quests;

public enum QuestStatus { Inactive, Active, Completed }

public sealed class QuestState(QuestDefinition definition)
{
    private readonly Dictionary<string, int> _progress = [];

    public QuestDefinition Definition { get; } = definition;
    public QuestStatus Status { get; internal set; }

    public int Progress(ObjectiveDefinition objective) => _progress.GetValueOrDefault(objective.Id);
    public bool IsComplete(ObjectiveDefinition objective) => Progress(objective) >= objective.Count;

    /// <summary>The first unfinished required objective – what the HUD tracker shows.</summary>
    public ObjectiveDefinition? CurrentObjective =>
        Definition.Objectives.FirstOrDefault(o => !o.Optional && !IsComplete(o));

    internal bool Advance(ObjectiveDefinition objective, int amount)
    {
        if (IsComplete(objective)) return false;
        _progress[objective.Id] = Math.Min(objective.Count, Progress(objective) + amount);
        return true;
    }

    internal bool AllRequiredComplete => Definition.Objectives.Where(o => !o.Optional).All(IsComplete);
}

public enum QuestUpdateKind { Started, ObjectiveProgress, ObjectiveComplete, Completed }

public readonly record struct QuestUpdate(QuestUpdateKind Kind, QuestState Quest, ObjectiveDefinition? Objective = null);

/// <summary>Tracks quest state and translates gameplay events into objective progress.</summary>
public sealed class QuestLog(ContentDatabase content)
{
    private readonly Dictionary<string, QuestState> _quests = [];

    public IEnumerable<QuestState> All => _quests.Values;
    public IEnumerable<QuestState> Active => _quests.Values.Where(q => q.Status == QuestStatus.Active);
    public QuestState? Get(string id) => _quests.GetValueOrDefault(id);

    public QuestUpdate? Start(string questId)
    {
        if (_quests.ContainsKey(questId)) return null;
        var quest = new QuestState(content.Quests[questId]) { Status = QuestStatus.Active };
        _quests[questId] = quest;
        return new QuestUpdate(QuestUpdateKind.Started, quest);
    }

    /// <param name="targets">Every identifier the event matches (e.g. enemy id plus its tags).</param>
    public IReadOnlyList<QuestUpdate> Notify(ObjectiveType type, IReadOnlyCollection<string> targets, int amount = 1)
    {
        var updates = new List<QuestUpdate>();
        foreach (var quest in Active.ToList())
        {
            foreach (var objective in quest.Definition.Objectives)
            {
                if (objective.Type != type || !targets.Contains(objective.Target)) continue;
                if (!quest.Advance(objective, amount)) continue;

                updates.Add(new QuestUpdate(
                    quest.IsComplete(objective) ? QuestUpdateKind.ObjectiveComplete : QuestUpdateKind.ObjectiveProgress,
                    quest, objective));
            }
            if (quest.AllRequiredComplete)
            {
                quest.Status = QuestStatus.Completed;
                updates.Add(new QuestUpdate(QuestUpdateKind.Completed, quest));
            }
        }
        return updates;
    }

    public IReadOnlyList<QuestUpdate> Notify(ObjectiveType type, string target, int amount = 1) =>
        Notify(type, [target], amount);
}
