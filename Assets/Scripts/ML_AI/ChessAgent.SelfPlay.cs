public partial class ChessAgent
{
    private bool IsAgentWinner(
        EPlayer winner)
    {
        return winner == agentPlayer;
    }

    private bool IsAgentLoser(
        EPlayer winner)
    {
        return winner != agentPlayer;
    }
}
