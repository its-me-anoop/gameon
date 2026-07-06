import Foundation

public enum GameMode: Equatable, Codable, Sendable, Hashable {
    case endless
    case daily(puzzleNumber: Int, moveBudget: Int)
    /// No budget, no pressure ramp — a single spawn per move keeps the board
    /// breathable indefinitely (see docs/balance-report.md).
    case zen
    /// Fixed-budget score attack under constant double-spawn pressure.
    case sprint(moveBudget: Int)

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
    public let mode: GameMode
    public let seed: UInt64

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
        var rng: SplitMix64
        var nextTileID: Int

        init(
            board: Board, gravity: Direction, score: Int, bestTile: Int,
            moveCount: Int, cascadeCount: Int, bestCascadeRound: Int,
            rng: SplitMix64, nextTileID: Int
        ) {
            self.board = board
            self.gravity = gravity
            self.score = score
            self.bestTile = bestTile
            self.moveCount = moveCount
            self.cascadeCount = cascadeCount
            self.bestCascadeRound = bestCascadeRound
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
        rng = SplitMix64(seed: seed)
        nextTileID = 1
        history = []
        for _ in 0..<2 {
            if let (newBoard, event) = MoveResolver.spawn(
                on: board, gravity: gravity, rng: &rng, nextTileID: &nextTileID
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
        rng = SplitMix64(seed: seed)
        nextTileID = 1000
        history = []
        bestTile = testBoard.tiles.map(\.1.value).max() ?? 0
    }

    /// Test-only stat injection for exercising presentation code.
    mutating func setTestStats(score: Int, cascadeCount: Int) {
        self.score = score
        self.cascadeCount = cascadeCount
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
        case .endless, .zen:
            return nil
        }
    }

    /// Spawn pacing is owned by the mode: endless (and daily, whose budget
    /// never reaches the ramp) keep the cruise → tension → collapse ramp,
    /// zen stays at a breathable single spawn, sprint applies constant
    /// double-spawn pressure from move one.
    public var spawnCountForNextMove: Int {
        switch mode {
        case .zen: 1
        case .sprint: 2
        case .endless, .daily: Self.spawnCount(forMovesPlayed: moveCount)
        }
    }

    public var hasLegalMove: Bool {
        var scratchID = nextTileID
        return Direction.allCases.contains { direction in
            MoveResolver.slide(board, toward: direction, nextTileID: &scratchID).changed
        }
    }

    public var isGameOver: Bool {
        if let remaining = movesRemaining, remaining == 0 { return true }
        return !hasLegalMove
    }

    @discardableResult
    public mutating func applyMove(_ direction: Direction) -> MoveResult? {
        if let remaining = movesRemaining, remaining == 0 { return nil }
        // Snapshot before resolution: resolveMove advances the RNG and tile
        // counter, and undo must restore their pre-move values.
        let snapshot = makeSnapshot()
        guard let result = MoveResolver.resolveMove(
            board: board, swipe: direction, gravity: gravity,
            rng: &rng, nextTileID: &nextTileID,
            spawnCount: spawnCountForNextMove
        ) else { return nil }

        pushSnapshot(snapshot)
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
        return result
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
        rng = snapshot.rng
        nextTileID = snapshot.nextTileID
        undosUsed += 1
        return true
    }

    private func makeSnapshot() -> Snapshot {
        Snapshot(
            board: board, gravity: gravity, score: score, bestTile: bestTile,
            moveCount: moveCount, cascadeCount: cascadeCount,
            bestCascadeRound: bestCascadeRound, rng: rng, nextTileID: nextTileID
        )
    }

    private mutating func pushSnapshot(_ snapshot: Snapshot) {
        history.append(snapshot)
        if history.count > Self.historyLimit {
            history.removeFirst()
        }
    }
}
