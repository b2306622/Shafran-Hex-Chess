using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;

public partial class ChessAgent : Agent
{
    [Header("AI Configuration")]
    [SerializeField]
    private EPlayer agentPlayer = EPlayer.WHITE;

    /// <summary>
    /// Public read-only identity used by experiment controllers to distinguish
    /// the PPO side from an external Minimax opponent without duplicating or
    /// changing the private training configuration.
    /// </summary>
    public EPlayer AgentPlayer => agentPlayer;

    [Header("Episode")]
    [SerializeField]
    private bool resetBoardOnlyForWhiteAgent = true;

    // =========================================================
    // BASIC REWARD
    // =========================================================

    [Header("Reward")]
    [SerializeField]
    private float invalidMoveReward = -0.50f;

    [SerializeField]
    private float legalMoveReward = 0.0002f;

    [SerializeField]
    private float captureRewardScale = 0.002f;

    // =========================================================
    // TERMINAL REWARD
    // =========================================================

    [Header("Terminal Reward")]
    [SerializeField]
    private float lossReward = -1.0f;

    [SerializeField]
    private float normalDrawReward = 0.0f;

    [SerializeField]
    private float repetitionDrawReward = -0.03f;

    [SerializeField]
    private float drawWhileAheadPenalty = -0.04f;

    [SerializeField]
    private float drawMaterialLeadPenaltyPerPoint = -0.008f;

    [SerializeField]
    private float drawMaterialLeadPenaltyCap = -0.08f;

    [SerializeField]
    private float drawMaterialLeadThreshold = 2.5f;

    [SerializeField]
    private float repetitionPenaltyAheadMultiplier = 1.5f;

    [SerializeField]
    private float maxPlyDrawReward = -0.05f;

    [SerializeField]
    private float perPlyPenalty = -0.0015f;

    // =========================================================
    // TRAINING GAME LENGTH
    // =========================================================

    [Header("Training Game Length")]
    [SerializeField]
    private int trainingMaxGamePlies = 500;

    // =========================================================
    // FAST WIN
    // =========================================================

    [Header("Fast Win Reward")]


    // =========================================================
    // MATERIAL
    // =========================================================

    [Header("Material Values")]
    [SerializeField]
    private float pawnValue = 1.0f;

    [SerializeField]
    private float knightValue = 4.25f;

    [SerializeField]
    private float bishopValue = 4.50f;

    [SerializeField]
    private float rookValue = 7.0f;

    [SerializeField]
    private float queenValue = 12.5f;

    [SerializeField]
    private float kingValue = 100.0f;

    // =========================================================
    // EVALUATION
    // =========================================================

    [Header("Evaluation Reward")]
    [SerializeField]
    private float evaluationRewardScale = 0.00004f;

    [SerializeField]
    private float strategicRewardPerComponentCap = 0.050f;

    // =========================================================
    // SHAPING BUDGET
    // =========================================================

    // Tổng shaping tối đa trong một episode.
    //
    // Terminal:
    // WIN  +1.00 -> +0.25
    // LOSS -1.00
    // DRAW  0.00
    //
    // Shaping chỉ đóng vai trò hướng dẫn,
    // không được phép lấn át kết quả trận đấu.
    [Header("Shaping Budget")]
    [SerializeField]
    private float shapingRewardEpisodeCap = 0.50f;

    // =========================================================
    // MATERIAL LOSS / GAIN
    // =========================================================

    [Header("Material Reward")]
    [SerializeField]
    private float materialDeltaRewardScale = 0.010f;

    [SerializeField]
    private float materialLossRewardCap = 0.08f;

    [SerializeField]
    private float materialGainRewardCap = 0.06f;

    [SerializeField]
    private float exposedPiecePenaltyScale = 0.015f;

    [SerializeField]
    private float exposedPiecePenaltyCap = 0.08f;

    // =========================================================
    // ANTI REPEAT
    // =========================================================

    [Header("Anti Repeat")]
    [SerializeField]
    private float repetitionPenaltySecondOccurrence = 0.010f;

    [SerializeField]
    private float repetitionPenaltyThirdOccurrence = 0.020f;

    [SerializeField]
    private float repetitionPenaltyFourthOccurrence = 0.035f;

    [SerializeField]
    private float immediateReversePenalty = 0.008f;

    // =========================================================
    // NO PROGRESS
    // =========================================================

    [Header("No Progress")]
    [SerializeField]
    private int noProgressGraceMoves = 8;

    [SerializeField]
    private float noProgressPenaltyPerMove = 0.002f;

    [SerializeField]
    private float noProgressPenaltyCap = 0.020f;

    [SerializeField]
    private float progressEvaluationThreshold = 2.0f;

    // =========================================================
    // ENDGAME CONVERSION
    // =========================================================

    [Header("Endgame Conversion")]
    [SerializeField]
    private int endgameOpponentNonKingThreshold = 4;

    [SerializeField]
    private float endgameMaterialLeadThreshold = 2.5f;

    [SerializeField]
    private float endgamePawnAdvanceReward = 0.018f;

    [SerializeField]
    private float endgamePawnNearPromotionReward = 0.025f;

    [SerializeField]
    private float endgamePassedPawnReward = 0.016f;

    [SerializeField]
    private float endgameProtectedPawnReward = 0.015f;

    [SerializeField]
    private float endgamePawnBlockerCaptureBonus = 0.032f;

    [SerializeField]
    private float endgameQuietPawnFailurePenalty = 0.006f;

    [SerializeField]
    private int endgameMajorPieceCountThreshold = 4;

    [SerializeField]
    private float endgameMajorPieceActionReward = 0.012f;

    [SerializeField]
    private float endgameMajorPiecePriorityReward = 0.010f;

    [SerializeField]
    private float endgameMajorPieceCheckBonus = 0.014f;

    [SerializeField]
    private float endgameMajorPieceCaptureBonus = 0.012f;

    [SerializeField]
    private float endgameMajorPiecePressureBonus = 0.010f;

    [SerializeField]
    private float endgameLowerPriorityMovePenalty = 0.006f;

    [SerializeField]
    private float endgameKingSupportReward = 0.010f;

    [SerializeField]
    private float endgameKingWanderingPenalty = 0.035f;

    [SerializeField]
    private float endgameKingMajorWanderingPenalty = 0.040f;

    [SerializeField]
    private float endgamePawnBlockadeBreakBonus = 0.030f;

    [SerializeField]
    private float endgamePawnBreakthroughBonus = 0.022f;

    [SerializeField]
    private float endgameAdvancedPawnProtectionReward = 0.016f;

    [SerializeField]
    private float queenSurvivesReplyBonus = 0.012f;

    [SerializeField]
    private int endgameNoProgressGraceMoves = 3;

    [SerializeField]
    private float endgameNoProgressPenaltyPerMove = 0.004f;

    [SerializeField]
    private float endgameNoProgressPenaltyCap = 0.040f;

    // =========================================================
    // ENDGAME ANTI-BLUNDER / KING CONVERSION
    // =========================================================

    [Header("Endgame Anti-Blunder")]
    [SerializeField]
    private float hangingPiecePenaltyScale = 0.035f;

    [SerializeField]
    private float hangingPiecePenaltyCap = 0.12f;

    [SerializeField]
    private float kingTrapRewardAtOneMove = 0.025f;

    // =========================================================
    // SHAFRAN-SPECIFIC INITIATIVE / CONVERSION
    // =========================================================
    // Shafran pawns have different first-move ranges by file (1/2/3).
    // Reward a purposeful first central advance without overpowering
    // material and terminal signals.
    [Header("Shafran Initiative")]
    [SerializeField]
    private float shafranLongPawnMoveReward = 0.004f;

    [SerializeField]
    private float shafranMiddlegamePawnBreakReward = 0.006f;

    [SerializeField]
    private float endgameDynamicPlyPenaltyMultiplier = 1.75f;

    [SerializeField]
    private float conversionLeadThreshold = 2.5f;

    [SerializeField]
    private float conversionLeadStrongThreshold = 5.0f;

    // =========================================================
    // DEVELOPMENT / POSITION
    // =========================================================

    [Header("Development / Position")]
    [SerializeField]
    private float knightDevelopmentReward = 0.012f;

    [SerializeField]
    private float bishopDevelopmentReward = 0.012f;

    // Đổi thành penalty.
    [SerializeField]
    private float earlyRookMovePenalty = 0.010f;

    [SerializeField]
    private float earlyQueenMovePenalty = 0.015f;

    [SerializeField]
    private float centerControlRewardPerCell = 0.003f;

    [SerializeField]
    private float positionDeltaRewardScale = 0.008f;

    [SerializeField]
    private float kingPressurePerMove = 0.003f;

    [SerializeField]
    private float kingIntrusionRewardScale = 0.006f;

    [SerializeField]
    private float defensiveRetreatReward = 0.008f;

    // =========================================================
    // OPENING / MIDDLEGAME TACTICAL PRIORITY
    // =========================================================

    [Header("Opening / Middlegame Tactical Priority")]
    [SerializeField]
    private float criticalDefenseReward = 0.028f;

    [SerializeField]
    private float criticalDefensePenalty = 0.030f;

    [SerializeField]
    private float freeCaptureReward = 0.022f;

    [SerializeField]
    private float freeCaptureMissPenalty = 0.026f;

    [SerializeField]
    private float recaptureReward = 0.018f;

    [SerializeField]
    private float valuableAttackReward = 0.010f;

    [SerializeField]
    private float openingDevelopmentQualityBonus = 0.010f;

    [SerializeField]
    private float tacticalPhaseWeight = 1.0f;

    // =========================================================
    // V8 TACTICAL EXCHANGE / THREAT AWARENESS
    // =========================================================
    // The policy must evaluate a capture together with the opponent's
    // immediate recapture(s), not just ask whether the target is
    // "defended". This teaches real exchange safety:
    //
    //   R x R  Q x R  -> neutral exchange, not a free win
    //   R x P  Q x R  -> bad trade, strong negative signal
    //   B x Q  R x B  -> good trade, positive signal
    //
    // It also evaluates whether a move leaves a high-value piece
    // tactically capturable on the opponent reply.
    [Header("V8 Tactical Exchange / Threat Awareness")]
    [SerializeField]
    private int tacticalExchangeDepth = 2;

    [SerializeField]
    private int tacticalOpportunityDepth = 1;

    [SerializeField]
    private float favorableExchangeReward = 0.018f;

    [SerializeField]
    private float badExchangePenalty = 0.045f;

    [SerializeField]
    private float severeExchangePenalty = 0.050f;

    [SerializeField]
    private float protectedNeutralCapturePenalty = 0.008f;

    [SerializeField]
    private float immediateThreatPenalty = 0.040f;

    [SerializeField]
    private float threatRemovalReward = 0.020f;

    [SerializeField]
    private float missedRecapturePenalty = 0.040f;

    [SerializeField]
    private float accurateRecaptureReward = 0.020f;

    [SerializeField]
    private float tacticalCheckSacrificeDiscount = 0.70f;

    [SerializeField]
    private bool logTacticalExchangeDiagnostics = false;

    // =========================================================
    // TACTICAL
    // =========================================================

    [Header("Tactical Reward")]
    [SerializeField]
    private float checkReward = 0.015f;

    [SerializeField]
    private float promotionReward = 0.12f;

    [SerializeField]
    private float castlingReward = 0.004f;

    [SerializeField]
    private float majorCaptureTacticalReward = 0.008f;

    [SerializeField]
    private float minorCaptureTacticalReward = 0.004f;

    [SerializeField]
    private float pawnCaptureTacticalReward = 0.001f;

    // =========================================================
    // QUEEN SURVIVAL / CONVERSION
    // =========================================================
    // A promoted Queen is the main conversion asset. Losing it on the
    // opponent reply must produce a clear negative PPO signal.
    [Header("Queen Survival")]
    [SerializeField]
    private float queenLossPenalty = 0.18f;

    [SerializeField]
    private float queenHangingPenalty = 0.10f;

    [SerializeField]
    private float majorPieceLossPenalty = 0.06f;

    [SerializeField]
    private bool logQueenDiagnostics = false;


    // =========================================================
    // V10 MATE / FORCING PLAY
    // =========================================================
    // These values are intentionally small compared with terminal
    // checkmate reward. They shape the policy toward forcing play:
    // reduce enemy King mobility, create mate threats, and prefer
    // concrete tactical progress over passive wandering.
    [Header("V10 Mate / Forcing")]
    [SerializeField]
    private float v10MobilityReductionSmall = 0.003f;

    [SerializeField]
    private float v10MobilityReductionMedium = 0.008f;

    [SerializeField]
    private float v10MobilityReductionStrong = 0.012f;

    [SerializeField]
    private float v10MobilityReductionCritical = 0.020f;

    [SerializeField]
    private float v10KingOneEscapeBonus = 0.020f;

    [SerializeField]
    private float v10CheckBonus = 0.015f;

    [SerializeField]
    private float v10MateThreatBonus = 0.035f;

    [SerializeField]
    private float v10ForcingCaptureBonus = 0.008f;

    [SerializeField]
    private float v10BishopColorBonus = 0.010f;

    [SerializeField]
    private float v10ThreeColorBishopBonus = 0.015f;

    // =========================================================
    // MINIMAX GUIDANCE
    // =========================================================

    [Header("Minimax Guidance")]
    [SerializeField]
    private bool enableMinimaxGuidance = false;

    // Model C: use only one Minimax teacher.
    // The teacher runs at fixed depth 3 every N ML-Agent decisions.
    [SerializeField]
    [Min(1)]
    private int minimaxGuidanceFrequency = 5;

    [SerializeField]
    [Min(1)]
    private int minimaxGuidanceDepth = 3;

    [SerializeField]
    [Min(1)]
    private int minimaxGuidanceTimeLimitMs = 10;

    [SerializeField]
    private float minimaxGuidanceNearBestThreshold = 50.0f;

    [SerializeField]
    private float minimaxGuidanceBadThreshold = 200.0f;

    [SerializeField]
    private float minimaxGuidanceExactReward = 0.015f;

    [SerializeField]
    private float minimaxGuidanceNearBestReward = 0.006f;

    [SerializeField]
    private float minimaxGuidancePenalty = 0.025f;

    // =========================================================
    // INTERNAL
    // =========================================================

    private const int CELL_COUNT =
        HexChessRules.CELL_COUNT;

    private const int ACTION_COUNT =
        CELL_COUNT * CELL_COUNT;

    private const int PIECE_STATE_COUNT = 13;

    private bool _episodeResultProcessed;

    // Set when this agent just promoted a pawn. The next opponent reply
    // is used to give a small survival bonus if the promoted Queen remains.
    private bool _lastAgentMoveWasPromotion;

    private float _evaluationBeforeMove;

    private GameState _stateBeforeMove;

    private StateMove _selectedStateMove;

    // Snapshot immediately after this agent's previous move. It lets us
    // detect material lost on the opponent's reply, which is otherwise
    // invisible to our own before/after move comparison.
    private GameState _lastAgentMoveState;

    private ulong _lastOpponentResponseHash =
        ulong.MaxValue;

    private float _episodeRewardAccumulator;

    private float _episodeShapingRewardAccumulator;

    private MoveGenerator _moveGenerator;

    private Evaluation _evaluation;

    private readonly bool[] _legalActionTable =
        new bool[ACTION_COUNT];

    private readonly StateMove[] _cachedActionMoves =
        new StateMove[ACTION_COUNT];

    // Reused observation buffer; avoid a new managed array on every decision.
    private readonly float[] _observationSafetyBuffer =
        new float[CELL_COUNT];

    private List<StateMove> _cachedLegalMoves =
        new List<StateMove>();

    private GameState _cachedGameState;

    private ulong _cachedHash =
        ulong.MaxValue;

    private bool _legalCacheValid;

    private readonly Dictionary<ulong, int>
        _positionVisitCounts =
            new Dictionary<ulong, int>(512);

    private MinimaxAI _guidanceAI;

    private StateMove _cachedGuidanceMove;

    private float _cachedGuidanceScore;

    private ulong _cachedGuidanceHash =
        ulong.MaxValue;

    private bool _hasGuidanceMove;

    private int _decisionSequence;

    private bool _turnObserved;

    private EPlayer _observedTurn;

    private bool _decisionRequestedForTurn;

    private ulong _decisionRequestedHash =
        ulong.MaxValue;

    private EPlayer _decisionRequestedPlayer =
        EPlayer.WHITE;

    // Terminal checks are event-driven rather than executed every Unity frame.
    private bool _terminalStateDirty = true;

    // =========================================================
    // SHARED GAME PLY
    // =========================================================
    //
    // White và Black phải sử dụng CÙNG một bộ đếm.
    //
    // Không dùng:
    //
    // private int _gamePlies;
    //
    // vì mỗi ChessAgent sẽ có một counter riêng.
    //
    // Static giúp hai Agent cùng nhìn thấy số ply hiện tại.
    //
    private static int _sharedGamePlies = 0;

    // Diagnostic only: one ID per self-play environment reset.
    private static int _sharedDebugEpisodeId = 0;

    public static int DebugEpisodeId => _sharedDebugEpisodeId;

    // Số ply hiện tại của ván.
    public int GamePlies =>
        _sharedGamePlies;

    // Alias dễ đọc hơn nếu các file khác cần dùng.
    public int CurrentGamePly =>
        _sharedGamePlies;

    // =========================================================
    // PUBLIC
    // =========================================================

    public int ActionCount =>
        ACTION_COUNT;

    public int ObservationSize =>
        CELL_COUNT * 14 +
        4 +
        1 +
        1;

    // =========================================================
    // UNITY
    // =========================================================

    protected override void Awake()
    {
        base.Awake();

        // Agent tự gọi RequestDecision().
        //
        // Không cho DecisionRequester tự request
        // ngoài lượt.
        Unity.MLAgents.DecisionRequester requester =
            GetComponent<Unity.MLAgents.DecisionRequester>();

        if (requester != null)
        {
            requester.enabled = false;
        }
    }

    public override void Initialize()
    {
        _moveGenerator =
            new MoveGenerator();

        _evaluation =
            new Evaluation(
                agentPlayer
            );

        ResetRuntimeState();
    }

    private void Update()
    {
        TrackTurnChange();

        // Terminal/material-response work is only needed after a real board or
        // turn change. Rebuilding GameState every frame starves the communicator.
        if (_terminalStateDirty)
        {
            CheckOpponentResponseMaterialLoss();

            CheckCapturedKingTerminalState();

            if (IsGameRunning())
                CheckMLAgentTerminalState();

            if (IsGameRunning())
                CheckInsufficientMaterialDraw();

            if (IsGameRunning())
                CheckFiftyMoveRuleDraw();

            // ---------------------------------------------------------
            // MAX-PLY TERMINAL -- SAME UPDATE, NOT LateUpdate
            // ---------------------------------------------------------
            // RegisterMove() marks the 500th ply as pending. Resolve it
            // here only after checkmate/captured-king/stalemate/other
            // normal draw rules above have had first priority. This is
            // critical for ML-Agents: EndEpisode() must happen in the
            // same Unity frame as the terminal move, otherwise the next
            // training step can arrive while GameState is already
            // GAME_OVER but the episode is still open.
            if (IsGameRunning() &&
                IsAgentTurn() &&
                BaseGameController.Current != null)
            {
                // Only the agent whose turn it is may resolve the artificial
                // horizon. That agent is also the one that can evaluate
                // checkmate/stalemate for the side to move. The other agent
                // must not close the game first merely because Unity called
                // its Update() earlier in the frame.
                BaseGameController.Current
                    .TryResolvePendingMaxPlyDraw();
            }

            _terminalStateDirty = false;
            ResolveGameResult();
        }

        TryRequestDecision();
    }

    // =========================================================
    // TURN TRACKING
    // =========================================================

    private void TrackTurnChange()
    {
        if (BaseGameController.Current == null)
        {
            return;
        }

        EPlayer currentTurn =
            BaseGameController.Current.CurrentPlayer;

        if (!_turnObserved)
        {
            _turnObserved = true;

            _observedTurn =
                currentTurn;

            _decisionRequestedForTurn =
                false;

            _decisionRequestedHash =
                ulong.MaxValue;

            _decisionRequestedPlayer =
                currentTurn;

            InvalidateLegalMoveCache();
            _terminalStateDirty = true;

            return;
        }

        if (_observedTurn != currentTurn)
        {
            _observedTurn =
                currentTurn;

            _decisionRequestedForTurn =
                false;

            _decisionRequestedHash =
                ulong.MaxValue;

            _decisionRequestedPlayer =
                currentTurn;

            InvalidateLegalMoveCache();
            _terminalStateDirty = true;
        }
    }

    // =========================================================
    // RESET
    // =========================================================

    private void ResetRuntimeState()
    {
        _episodeResultProcessed =
            false;

        _lastAgentMoveWasPromotion =
            false;

        _evaluationBeforeMove =
            0f;

        _stateBeforeMove =
            null;

        _selectedStateMove =
            default;

        _lastAgentMoveState =
            null;

        _lastOpponentResponseHash =
            ulong.MaxValue;

        _episodeRewardAccumulator =
            0f;

        _episodeShapingRewardAccumulator =
            0f;

        _cachedLegalMoves =
            new List<StateMove>();

        _cachedGameState =
            null;

        _cachedHash =
            ulong.MaxValue;

        _legalCacheValid =
            false;

        _positionVisitCounts.Clear();

        // Do NOT recreate the Minimax guidance engines every episode.
        // Their per-search heuristics/TT are reset by MinimaxAI itself;
        // keeping the instances avoids large repeated allocations and GC.
        _cachedGuidanceMove =
            default;

        _cachedGuidanceScore =
            0f;

        _cachedGuidanceHash =
            ulong.MaxValue;

        _hasGuidanceMove =
            false;

        _decisionSequence =
            0;

        _turnObserved =
            false;

        _decisionRequestedForTurn =
            false;

        _terminalStateDirty = true;

        _decisionRequestedHash =
            ulong.MaxValue;

        _decisionRequestedPlayer =
            EPlayer.WHITE;

        // =====================================================
        // RESET GAME PLY
        // =====================================================
        //
        // Chỉ Agent WHITE reset counter.
        //
        // Vì cả hai Agent gọi Initialize/ResetRuntimeState,
        // nếu cả hai cùng reset thì Black có thể reset counter
        // giữa episode.
        //
        if (agentPlayer == EPlayer.WHITE)
        {
            _sharedGamePlies = 0;
        }
    }

    // =========================================================
    // REGISTER SUCCESSFUL MOVE
    // =========================================================
    //
    // Gọi hàm này ĐÚNG MỘT LẦN sau khi move đã thực sự
    // được thực hiện thành công.
    //
    // KHÔNG gọi trong Update().
    // KHÔNG gọi trong WriteDiscreteActionMask().
    // KHÔNG gọi trước khi xác nhận move hợp lệ.
    //
    private void RegisterSuccessfulMove()
    {
        _sharedGamePlies =
            Mathf.Max(
                0,
                _sharedGamePlies + 1
            );
    }

    // Public wrapper để Actions.cs hoặc partial class khác
    // có thể gọi nếu cần.
    private void RegisterGamePly()
    {
        RegisterSuccessfulMove();
    }

    // =========================================================
    // FAST WIN REWARD
    // =========================================================
    // V10.1: terminal win remains close to +1.00. A small speed
    // preference is retained only to break ties between otherwise
    // successful checkmates.
    //
    private float CalculateWinReward()
    {
        // V10.1: terminal outcome is the dominant signal.
        // The previous +3.00 fast-win reward was larger than checkmate
        // itself and made PPO optimize episode length instead of the
        // actual game result. Keep only a tiny conversion-speed bonus.
        int currentPly =
            BaseGameController.Current != null
                ? Mathf.Max(0, BaseGameController.Current.GamePlyCount)
                : 0;

        if (currentPly <= 10) return 1.10f;
        if (currentPly <= 20) return 1.07f;
        if (currentPly <= 30) return 1.04f;
        if (currentPly <= 50) return 1.02f;
        return 1.00f;
    }

    // Cho các file Reward.cs gọi.
    private float GetCurrentWinReward()
    {
        return CalculateWinReward();
    }

    // =========================================================
    // TRACK REWARD
    // =========================================================

    private void AddTrackedReward(
        float reward)
    {
        AddReward(reward);

        _episodeRewardAccumulator +=
            reward;
    }

    // =========================================================
    // SHAPING REWARD
    // =========================================================

    private void AddShapingReward(
        float reward)
    {
        if (Mathf.Approximately(
                reward,
                0f))
        {
            return;
        }

        // =====================================================
        // POSITIVE BUDGET
        // =====================================================

        float remainingPositive =
            Mathf.Max(
                0f,
                shapingRewardEpisodeCap -
                _episodeShapingRewardAccumulator
            );

        // =====================================================
        // NEGATIVE BUDGET
        // =====================================================

        float remainingNegative =
            Mathf.Max(
                0f,
                shapingRewardEpisodeCap +
                _episodeShapingRewardAccumulator
            );

        float applied =
            reward;

        if (reward > 0f)
        {
            applied =
                Mathf.Min(
                    reward,
                    remainingPositive
                );
        }
        else
        {
            applied =
                Mathf.Max(
                    reward,
                    -remainingNegative
                );
        }

        if (Mathf.Approximately(
                applied,
                0f))
        {
            return;
        }

        _episodeShapingRewardAccumulator +=
            applied;

        AddTrackedReward(
            applied
        );
    }

    // =========================================================
    // V10 INSUFFICIENT MATERIAL
    // =========================================================
    //
    // Shafran terminal rule used by this training build:
    //   K vs K       -> draw
    //   K+B vs K     -> draw
    //   K+N vs K     -> draw
    //   K+2B vs K    -> draw
    //
    // IMPORTANT:
    //   K+Q/R/B/N/P vs K is NOT an automatic win.
    //   The agent must still produce checkmate.
    // =========================================================

    private void CheckInsufficientMaterialDraw()
    {
        if (!IsActionEnvironmentReady())
        {
            return;
        }

        if (BaseGameController.Current.GameState !=
            EGameState.PLAYING)
        {
            return;
        }

        GameState state =
            GameState.FromUnityBoard();

        if (state == null)
        {
            return;
        }

        if (HexChessRules.IsInsufficientMaterial(
                state,
                out bool draw) &&
            draw)
        {
            BaseGameController.Current.DrawGame(
                EDrawReason.InsufficientMaterial
            );
        }
    }

    private string DescribeMaterial(GameState state)
    {
        if (state == null) return "null";

        int own = CountNonKingPiecesForReward(state, agentPlayer);
        EPlayer opponent = HexChessRules.GetOpponent(agentPlayer);
        int enemy = CountNonKingPiecesForReward(state, opponent);
        return $"{agentPlayer}: {own} non-king pieces | {opponent}: {enemy} non-king pieces";
    }

    // Detailed diagnostic used only to trace promotion/insufficient-material
    // cases. This does not affect rewards or game rules.
    private string DescribeMaterialDetailed(GameState state)
    {
        if (state == null) return "Material=null";

        return
            $"WHITE: {DescribePlayerMaterialDetailed(state, EPlayer.WHITE)} | " +
            $"BLACK: {DescribePlayerMaterialDetailed(state, EPlayer.BLACK)}";
    }

    private string DescribePlayerMaterialDetailed(GameState state, EPlayer player)
    {
        int king = 0;
        int queen = 0;
        int rook = 0;
        int bishop = 0;
        int knight = 0;
        int pawn = 0;

        StatePiece?[,] board = state.Board;

        foreach (Vector2Int position in HexChessRules.CellCoordinates)
        {
            StatePiece? piece = board[position.x, position.y];

            if (!piece.HasValue ||
                !piece.Value.IsValid ||
                piece.Value.Player != player)
            {
                continue;
            }

            switch (piece.Value.Type)
            {
                case StatePieceType.KING:
                    king++;
                    break;
                case StatePieceType.QUEEN:
                    queen++;
                    break;
                case StatePieceType.ROOK:
                    rook++;
                    break;
                case StatePieceType.BISHOP:
                    bishop++;
                    break;
                case StatePieceType.KNIGHT:
                    knight++;
                    break;
                case StatePieceType.PAWN:
                    pawn++;
                    break;
            }
        }

        return $"K={king} Q={queen} R={rook} B={bishop} N={knight} P={pawn}";
    }

    // Backward-compatible wrapper for older partials/scenes.
    // It no longer treats "only King" as a loss.
    private void CheckMaterialElimination()
    {
        CheckInsufficientMaterialDraw();
    }

    // =========================================================
    // LEGACY K vs K WRAPPER
    // =========================================================
    //
    // Kept so older code does not break. V10 uses the generalized
    // insufficient-material detector above.
    // =========================================================

    private void CheckKingVsKingDraw()
    {
        CheckInsufficientMaterialDraw();
    }

    // =========================================================
    // 50-MOVE RULE
    // =========================================================
    //
    // BaseGameController already maintains the authoritative
    // no-progress clock in plies. 100 plies = 50 moves by both sides.
    // Captures, pawn moves and promotions reset that clock there.
    // =========================================================

    private void CheckFiftyMoveRuleDraw()
    {
        if (!IsActionEnvironmentReady())
        {
            return;
        }

        if (BaseGameController.Current.GameState !=
            EGameState.PLAYING)
        {
            return;
        }

        if (BaseGameController.Current.NoProgressPlyCount >= 100)
        {
            BaseGameController.Current.DrawGame(
                EDrawReason.FiftyMove
            );
        }
    }

    // =========================================================
    // CAPTURED KING TERMINAL
    // =========================================================
    // The visual game already ends immediately from P_King.BeAttackedBy().
    // This second guard makes the ML training loop robust if a board/state
    // update ever reaches Update() with one King missing.

    private void CheckCapturedKingTerminalState()
    {
        if (!IsActionEnvironmentReady())
        {
            return;
        }

        if (BaseGameController.Current.GameState !=
            EGameState.PLAYING)
        {
            return;
        }

        GameState state =
            GameState.FromUnityBoard();

        if (state == null)
        {
            return;
        }

        bool whiteKing =
            HexChessRules.HasKing(
                state,
                EPlayer.WHITE
            );

        bool blackKing =
            HexChessRules.HasKing(
                state,
                EPlayer.BLACK
            );

        if (whiteKing && blackKing)
        {
            return;
        }

        if (!whiteKing && !blackKing)
        {
            Debug.LogWarning(
                "[SHAFRAN TERMINAL] Both Kings are missing. " +
                "Keeping the episode unresolved rather than inventing a winner."
            );
            return;
        }

        EPlayer winner =
            whiteKing
                ? EPlayer.WHITE
                : EPlayer.BLACK;

        BaseGameController.Current.GameOver(
            winner
        );
    }

    // =========================================================
    // ML AGENT TERMINAL
    // =========================================================

    private void CheckMLAgentTerminalState()
    {
        if (!IsActionEnvironmentReady())
        {
            return;
        }

        if (BaseGameController.Current.GameState !=
            EGameState.PLAYING)
        {
            return;
        }

        if (BaseGameController.Current.CurrentPlayer !=
            agentPlayer)
        {
            return;
        }

        EnsureLegalMoveCache();

        if (_cachedLegalMoves.Count > 0)
        {
            return;
        }

        GameState state =
            GetCachedGameState();

        if (state == null)
        {
            return;
        }

        bool inCheck =
            _moveGenerator.IsInCheck(
                state,
                agentPlayer
            );

        EPlayer opponent =
            HexChessRules.GetOpponent(
                agentPlayer
            );

        if (inCheck)
        {
            // CHECKMATE
            BaseGameController.Current.GameOver(
                opponent
            );
        }
        else
        {
            // STALEMATE
            BaseGameController.Current.DrawGame(
                EDrawReason.Stalemate
            );
        }
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDestroy()
    {
        _positionVisitCounts.Clear();

        // Không reset _sharedGamePlies ở đây.
        //
        // Có thể còn Agent còn lại đang xử lý terminal state.
    }

    protected override void OnDisable()
    {
        InvalidateLegalMoveCache();

        _decisionRequestedForTurn =
            false;

        _decisionRequestedHash =
            ulong.MaxValue;

        _decisionRequestedPlayer =
            EPlayer.WHITE;

        _turnObserved =
            false;

        base.OnDisable();
    }
}