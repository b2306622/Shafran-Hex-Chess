using System;
using System.Collections.Generic;

[Serializable]
public struct PolicyValueOutput
{
    public float[] policy;
    public float value;

    public PolicyValueOutput(float[] policy, float value)
    {
        this.policy = policy;
        this.value = value;
    }
}

public interface IPolicyValueEvaluator
{
    PolicyValueOutput Evaluate(
        GameState state,
        EPlayer perspective,
        IReadOnlyList<StateMove> legalMoves);
}
