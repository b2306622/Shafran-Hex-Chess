using System;

[Serializable]
public sealed class AlphaZeroTrainingSample
{
    public float[] observation;
    public int[] actionIndices;
    public float[] policy;
    public float value;

    // Metadata is optional but makes train/validation splitting by GAME possible.
    public int gameIndex;
    public int plyIndex;

    public AlphaZeroTrainingSample(
        float[] observation,
        int[] actionIndices,
        float[] policy,
        float value)
        : this(
            observation,
            actionIndices,
            policy,
            value,
            0,
            0)
    {
    }

    public AlphaZeroTrainingSample(
        float[] observation,
        int[] actionIndices,
        float[] policy,
        float value,
        int gameIndex,
        int plyIndex)
    {
        this.observation = observation;
        this.actionIndices = actionIndices;
        this.policy = policy;
        this.value = value;
        this.gameIndex = gameIndex;
        this.plyIndex = plyIndex;
    }
}
