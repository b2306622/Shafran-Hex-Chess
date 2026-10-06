using System.Collections.Generic;

public sealed class MCTSNode
{
    public MCTSNode Parent { get; }
    public StateMove MoveFromParent { get; }
    public float Prior { get; set; }
    public int VisitCount { get; private set; }
    public float ValueSum { get; private set; }
    public bool IsExpanded { get; set; }
    public GameState State { get; private set; }
    public List<MCTSNode> Children { get; } = new List<MCTSNode>(32);

    public float Q => VisitCount > 0 ? ValueSum / VisitCount : 0f;

    public MCTSNode(
        MCTSNode parent,
        StateMove moveFromParent,
        float prior,
        GameState state = null)
    {
        Parent = parent;
        MoveFromParent = moveFromParent;
        Prior = prior;
        State = state;
    }

    public void SetState(GameState state)
    {
        State = state;
    }

    public void Update(float value)
    {
        VisitCount++;
        ValueSum += value;
    }
}
