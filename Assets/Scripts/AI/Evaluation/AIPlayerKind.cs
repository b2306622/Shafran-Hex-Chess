public enum AIPlayerKind
{
    MINIMAX_ALPHA_BETA = 0,
    PPO_BASE = 1,
    PPO_MINIMAX_D1 = 2,
    PPO_MINIMAX_D2 = 3,
    ALPHAZERO_BASE = 4,
    ALPHAZERO_FROM_PPO_D1 = 5,
    ALPHAZERO_FROM_PPO_D2 = 6,

    // Final three-way thesis models.
    MODEL_A_PRIME = 10,
    MODEL_B_PRIME = 11,
    MODEL_C_PRIME = 12
}
