import Foundation

public enum GameMode: Equatable, Codable, Sendable, Hashable {
    case endless
    case daily(puzzleNumber: Int, moveBudget: Int)
    /// No budget, no pressure ramp — a single spawn per move keeps the board
    /// breathable indefinitely (see docs/balance-report.md).
    case zen
    /// Fixed-budget score attack under constant double-spawn pressure.
    case sprint(moveBudget: Int)
    /// Math Pop: tiles bond when they sum to the stage target and pop off the
    /// board (v1.3 spec §3). No budget, no boulders, no stasis.
    case math

    public static let dailyMoveBudget = 40
    public static let sprintMoveBudget = 60

    public static func daily(puzzleNumber: Int) -> GameMode {
        .daily(puzzleNumber: puzzleNumber, moveBudget: dailyMoveBudget)
    }

    public static var sprint: GameMode { .sprint(moveBudget: sprintMoveBudget) }
}

/// The complete, self-contained state of one game. A value type so snapshots
/// (undo) and persistence are trivial, and fully Codable — including the RNG —
/// so a resumed game continues its exact random stream.
public struct GameState: Codable, Equatable, Sendable {
    public private(set) var board: Board
    public private(set) var gravity: Direction
    public private(set) var score: Int
    public private(set) var bestTile: Int
    public private(set) var moveCount: Int
    public private(set) var undosUsed: Int
    public private(set) var cascadeCount: Int
    /// Deepest cascade round reached this game (×2, ×3… bragging rights).
    public private(set) var bestCascadeRound: Int
    /// Banked stasis charges (earned at milestone crossings; spec v1.2 §3.1).
    public private(set) var stasisCharges: Int
    /// Math Pop progression (v1.3 spec §3.2); all zero outside `.math`.
    public private(set) var mathStage: Int
    public private(set) var bondsThisStage: Int
    public private(set) var bondsCleared: Int
    public let mode: GameMode
    public let seed: UInt64

    static let stasisMilestones = [256, 512, 1024]
    static let stasisBankCap = 2

    private var rng: SplitMix64
    private var nextTileID: Int
    private var history: [Snapshot]
    private static let historyLimit = 20

    private struct Snapshot: Codable, Equatable, Sendable {
        var board: Board
        var gravity: Direction
        var score: Int
        var bestTile: Int
        var moveCount: Int
        var cascadeCount: Int
        var bestCascadeRound: Int
        var stasisCharges: Int
        var mathStage: Int
        var bondsThisStage: Int
        var bondsCleared: Int
        var rng: SplitMix64
        var nextTileID: Int

        init(
            board: Board, gravity: Direction, score: Int, bestTile: Int,
            moveCount: Int, cascadeCount: Int, bestCascadeRound: Int,
            stasisCharges: Int, mathStage: Int, bondsThisStage: Int,
            bondsCleared: Int, rng: SplitMix64, nextTileID: Int
        ) {
            self.board = board
            self.gravity = gravity
            self.score = score
            self.bestTile = bestTile
            self.moveCount = moveCount
            self.cascadeCount = cascadeCount
            self.bestCascadeRound = bestCascadeRound
            self.stasisCharges = stasisCharges
            self.mathStage = mathStage
            self.bondsThisStage = bondsThisStage
            self.bondsCleared = bondsCleared
            self.rng = rng
            self.nextTileID = nextTileID
        }

        /// Snapshots persist inside saved games; fields added after v1.1
        /// default instead of failing the whole decode.
        init(from decoder: Decoder) throws {
            let c = try decoder.container(keyedBy: CodingKeys.self)
            board = try c.decode(Board.self, forKey: .board)
            gravity = try c.decode(Direction.self, forKey: .gravity)
            score = try c.decode(Int.self, forKey: .score)
            bestTile = try c.decode(Int.self, forKey: .bestTile)
            moveCount = try c.decode(Int.self, forKey: .moveCount)
            cascadeCount = try c.decode(Int.self, forKey: .cascadeCount)
            bestCascadeRound = try c.decodeIfPresent(Int.self, forKey: .bestCascadeRound) ?? 0
            stasisCharges = try c.decodeIfPresent(Int.self, forKey: .stasisCharges) ?? 0
            mathStage = try c.decodeIfPresent(Int.self, forKey: .mathStage) ?? 0
            bondsThisStage = try c.decodeIfPresent(Int.self, forKey: .bondsThisStage) ?? 0
            bondsCleared = try c.decodeIfPresent(Int.self, forKey: .bondsCleared) ?? 0
            rng = try c.decode(SplitMix64.self, forKey: .rng)
            nextTileID = try c.decode(Int.self, forKey: .nextTileID)
        }
    }

    public init(mode: GameMode, seed: UInt64) {
        self.mode = mode
        self.seed = seed
        board = Board()
        gravity = .down
        score = 0
        moveCount = 0
        undosUsed = 0
        cascadeCount = 0
        bestCascadeRound = 0
        stasisCharges = 0
        mathStage = 0
        bondsThisStage = 0
        bondsCleared = 0
        rng = SplitMix64(seed: seed)
        nextTileID = 1
        history = []
        bestTile = 0
        // Math deals a full starter hand so there's bonding material from
        // move one; the classic modes open with their traditional pair.
        let isMath = if case .math = mode { true } else { false }
        let starterCount = isMath ? MathProgression.starterCount : 2
        let rule: MergeRule = isMath ? .sumTarget(MathProgression.target(forStage: 0)) : .doubling
        for _ in 0..<starterCount {
            if let (newBoard, event) = MoveResolver.spawn(
                on: board, gravity: gravity, rng: &rng, nextTileID: &nextTileID, rule: rule
            ) {
                board = newBoard
                _ = event
            }
        }
        bestTile = board.tiles.map(\.1.value).max() ?? 0
    }

    /// Test-only entry point for constructing specific board positions.
    init(testBoard: Board, gravity: Direction, mode: GameMode, seed: UInt64) {
        self.mode = mode
        self.seed = seed
        board = testBoard
        self.gravity = gravity
        score = 0
        moveCount = 0
        undosUsed = 0
        cascadeCount = 0
        bestCascadeRound = 0
        stasisCharges = 0
        mathStage = 0
        bondsThisStage = 0
        bondsCleared = 0
        rng = SplitMix64(seed: seed)
        nextTileID = 1000
        history = []
        bestTile = testBoard.tiles.map(\.1.value).max() ?? 0
    }

    /// Test-only stat injection for exercising presentation code.
    mutating func setTestStats(score: Int, cascadeCount: Int, bestCascadeRound: Int = 0) {
        self.score = score
        self.cascadeCount = cascadeCount
        self.bestCascadeRound = bestCascadeRound
    }

    /// Test-only math progression injection.
    mutating func setTestMathStage(_ stage: Int) {
        mathStage = stage
    }

    mutating func setTestMathBonds(thisStage: Int) {
        bondsThisStage = thisStage
    }

    /// Saved games from older versions predate some keys; those default
    /// instead of failing the decode (which would cost the player their game).
    public init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        board = try c.decode(Board.self, forKey: .board)
        gravity = try c.decode(Direction.self, forKey: .gravity)
        score = try c.decode(Int.self, forKey: .score)
        bestTile = try c.decode(Int.self, forKey: .bestTile)
        moveCount = try c.decode(Int.self, forKey: .moveCount)
        undosUsed = try c.decode(Int.self, forKey: .undosUsed)
        cascadeCount = try c.decode(Int.self, forKey: .cascadeCount)
        bestCascadeRound = try c.decodeIfPresent(Int.self, forKey: .bestCascadeRound) ?? 0
        stasisCharges = try c.decodeIfPresent(Int.self, forKey: .stasisCharges) ?? 0
        mathStage = try c.decodeIfPresent(Int.self, forKey: .mathStage) ?? 0
        bondsThisStage = try c.decodeIfPresent(Int.self, forKey: .bondsThisStage) ?? 0
        bondsCleared = try c.decodeIfPresent(Int.self, forKey: .bondsCleared) ?? 0
        mode = try c.decode(GameMode.self, forKey: .mode)
        seed = try c.decode(UInt64.self, forKey: .seed)
        rng = try c.decode(SplitMix64.self, forKey: .rng)
        nextTileID = try c.decode(Int.self, forKey: .nextTileID)
        history = try c.decode([Snapshot].self, forKey: .history)
    }

    public var movesRemaining: Int? {
        switch mode {
        case let .daily(_, budget), let .sprint(budget):
            return max(0, budget - moveCount)
        case .endless, .zen, .math:
            return nil
        }
    }

    /// The active merge semantics — number bonds in Math Pop, doubling
    /// everywhere else (v1.3 spec §3.1).
    var mergeRule: MergeRule {
        if case .math = mode {
            return .sumTarget(MathProgression.target(forStage: mathStage))
        }
        return .doubling
    }

    /// The current "Make N" goal, nil outside Math Pop.
    public var mathTarget: Int? {
        if case .math = mode {
            return MathProgression.target(forStage: mathStage)
        }
        return nil
    }

    /// Spawn pacing is owned by the mode: endless (and daily, whose budget
    /// never reaches the ramp) keep the cruise → tension → collapse ramp,
    /// zen stays at a breathable single spawn, sprint applies constant
    /// double-spawn pressure from move one. Math spawns two — each bond
    /// removes two tiles, so the board stays near equilibrium.
    public var spawnCountForNextMove: Int {
        switch mode {
        case .zen: 1
        case .sprint, .math: 2
        case .endless, .daily: Self.spawnCount(forMovesPlayed: moveCount)
        }
    }

    /// Boulder cadence (v1.2 spec §3.2.4) — stateless in (mode, moveCount,
    /// seed) so resume/undo can't drift the schedule. Returns the ice HP for
    /// this move's first spawn, or 0 for a normal spawn.
    var boulderIceForNextSpawn: Int {
        switch mode {
        case .zen, .math:
            return 0
        case .endless:
            guard moveCount >= 40, (moveCount - 40) % 12 == 0 else { return 0 }
            return moveCount >= 100 ? 2 : 1
        case .sprint:
            return moveCount == 45 ? 1 : 0
        case .daily:
            // Two seed-determined moves in 5...35, identical worldwide.
            let first = 5 + Int(seed % 31)
            var second = 5 + Int((seed / 31) % 31)
            if second == first { second = 5 + (first - 5 + 7) % 31 }
            return (moveCount == first || moveCount == second) ? 1 : 0
        }
    }

    public var hasLegalMove: Bool {
        var scratchID = nextTileID
        return Direction.allCases.contains { direction in
            MoveResolver.slide(board, toward: direction, nextTileID: &scratchID, rule: mergeRule).changed
        }
    }

    public var isGameOver: Bool {
        if let remaining = movesRemaining, remaining == 0 { return true }
        return !hasLegalMove
    }

    /// Stasis availability: zen always (the calm mode has no stakes), endless
    /// while a charge is banked, never in the seeded/competitive modes nor in
    /// Math Pop (one rule fewer to hold in a young player's head).
    public var canUseStasis: Bool {
        switch mode {
        case .zen: true
        case .endless: stasisCharges > 0
        case .daily, .sprint, .math: false
        }
    }

    /// Bank one charge, respecting the cap. Zen doesn't bank (it doesn't need
    /// to), daily/sprint don't bank (they can never spend).
    mutating func bankStasisCharge() {
        guard case .endless = mode else { return }
        stasisCharges = min(Self.stasisBankCap, stasisCharges + 1)
    }

    @discardableResult
    public mutating func applyMove(_ direction: Direction, stasis: Bool = false) -> MoveResult? {
        if let remaining = movesRemaining, remaining == 0 { return nil }
        if stasis, !canUseStasis { return nil }
        // Snapshot before resolution: resolveMove advances the RNG and tile
        // counter, and undo must restore their pre-move values.
        let snapshot = makeSnapshot()
        guard var result = MoveResolver.resolveMove(
            board: board, swipe: direction, gravity: gravity,
            rng: &rng, nextTileID: &nextTileID,
            spawnCount: spawnCountForNextMove,
            rotateGravity: !stasis,
            boulderIce: boulderIceForNextSpawn,
            rule: mergeRule
        ) else { return nil }

        pushSnapshot(snapshot)
        if stasis, case .endless = mode { stasisCharges -= 1 }
        let bestTileBefore = bestTile
        board = result.finalBoard
        gravity = result.newGravity
        score += result.scoreDelta
        moveCount += 1
        cascadeCount += result.phases.filter { !$0.merges.isEmpty }.count
        bestCascadeRound = max(
            bestCascadeRound,
            result.phases.filter { !$0.merges.isEmpty }.map(\.round).max() ?? 0
        )
        bestTile = max(bestTile, board.tiles.map(\.1.value).max() ?? 0)
        for milestone in Self.stasisMilestones where bestTileBefore < milestone && bestTile >= milestone {
            bankStasisCharge()
        }
        if case .math = mode {
            advanceMathStageIfEarned(into: &result)
        }
        return result
    }

    /// Math Pop bookkeeping after a resolved move: count the move's bond pops
    /// and, on crossing the stage quota, sweep the board, deal starters for the
    /// next target, and pay the stage bonus (v1.3 spec §3.2).
    private mutating func advanceMathStageIfEarned(into result: inout MoveResult) {
        let bonds = result.slide.clears.count + result.phases.reduce(0) { $0 + $1.clears.count }
        bondsCleared += bonds
        bondsThisStage += bonds
        guard bondsThisStage >= MathProgression.bondsPerStage else { return }

        bondsThisStage = 0
        mathStage += 1
        let swept = board.tiles.map(\.1.id)
        board = Board()
        var starters: [SpawnEvent] = []
        for _ in 0..<MathProgression.starterCount {
            guard let (withSpawn, event) = MoveResolver.spawn(
                on: board, gravity: gravity, rng: &rng, nextTileID: &nextTileID, rule: mergeRule
            ) else { break }
            board = withSpawn
            starters.append(event)
        }
        let target = MathProgression.target(forStage: mathStage)
        let bonus = target * 5
        score += bonus
        result.stageAdvance = StageAdvance(
            newStage: mathStage, newTarget: target, bonusPoints: bonus,
            sweptTileIDs: swept, starterSpawns: starters
        )
    }

    /// Pressure ramp: 1 tile per move for the first 60 moves, then 2, then 3
    /// from move 120 on. Gives endless games a cruise → tension → collapse
    /// arc; daily's 40-move budget never reaches the ramp. Tuned via
    /// BalanceSim — see docs/balance-report.md.
    public static func spawnCount(forMovesPlayed moves: Int) -> Int {
        min(3, 1 + moves / 60)
    }

    public var canUndo: Bool { !history.isEmpty }

    /// Restores the full prior state (board, score, RNG, budget). `undosUsed`
    /// deliberately survives so entitlement gating can count honestly.
    @discardableResult
    public mutating func undo() -> Bool {
        guard let snapshot = history.popLast() else { return false }
        board = snapshot.board
        gravity = snapshot.gravity
        score = snapshot.score
        bestTile = snapshot.bestTile
        moveCount = snapshot.moveCount
        cascadeCount = snapshot.cascadeCount
        bestCascadeRound = snapshot.bestCascadeRound
        stasisCharges = snapshot.stasisCharges
        mathStage = snapshot.mathStage
        bondsThisStage = snapshot.bondsThisStage
        bondsCleared = snapshot.bondsCleared
        rng = snapshot.rng
        nextTileID = snapshot.nextTileID
        undosUsed += 1
        return true
    }

    private func makeSnapshot() -> Snapshot {
        Snapshot(
            board: board, gravity: gravity, score: score, bestTile: bestTile,
            moveCount: moveCount, cascadeCount: cascadeCount,
            bestCascadeRound: bestCascadeRound, stasisCharges: stasisCharges,
            mathStage: mathStage, bondsThisStage: bondsThisStage,
            bondsCleared: bondsCleared, rng: rng, nextTileID: nextTileID
        )
    }

    private mutating func pushSnapshot(_ snapshot: Snapshot) {
        history.append(snapshot)
        if history.count > Self.historyLimit {
            history.removeFirst()
        }
    }
}
