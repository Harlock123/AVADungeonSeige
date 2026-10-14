using DungeonSiege.Core.Content;

namespace DungeonSiege.Core.Dialogue;

/// <summary>Walks a dialogue tree. While active, the simulation is frozen.</summary>
public sealed class DialogueRunner
{
    public DialogueDefinition? Current { get; private set; }
    public DialogueNode? Node { get; private set; }
    public string? NpcId { get; private set; }
    public bool IsActive => Node is not null;

    public event Action? Changed;

    internal void Begin(DialogueDefinition dialogue, string? npcId)
    {
        Current = dialogue;
        NpcId = npcId;
        Node = dialogue.Node(dialogue.Start);
        Changed?.Invoke();
    }

    /// <summary>
    /// Selects a choice. A node with no authored choices offers a single implicit "Farewell".
    /// </summary>
    /// <returns>The script actions to run, and whether the conversation ended.</returns>
    internal (IReadOnlyList<string> Actions, bool Ended) Choose(int index)
    {
        if (Node is null || Current is null) return ([], true);

        if (Node.Choices.Count == 0)
        {
            End();
            return ([], true);
        }

        var choice = Node.Choices[Math.Clamp(index, 0, Node.Choices.Count - 1)];
        Node = choice.Next is null ? null : Current.Node(choice.Next);
        var ended = Node is null;
        if (ended) Current = null;
        Changed?.Invoke();
        return (choice.Actions, ended);
    }

    internal void End()
    {
        Current = null;
        Node = null;
        Changed?.Invoke();
    }
}
