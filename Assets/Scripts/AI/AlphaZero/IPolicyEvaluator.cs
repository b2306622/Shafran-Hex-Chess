using System.Collections.Generic;

public interface IPolicyEvaluator
{
    float[] EvaluatePolicy(
        GameState state,
        EPlayer perspective,
        IReadOnlyList<StateMove> legalMoves);
}
