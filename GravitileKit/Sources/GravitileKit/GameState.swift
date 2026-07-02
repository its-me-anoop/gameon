import Foundation

public enum GameMode: Equatable, Codable, Sendable, Hashable {
    case endless
    case daily(puzzleNumber: Int, moveBudget: Int)

    public static let dailyMoveBudget = 40

    public static func daily(puzzleNumber: Int) -> GameMode {
        .daily(puzzleNumber: puzzleNumber, moveBudget: dailyMoveBudget)
    }
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
        var rng: SplitMix64
        var nextTileID: Int
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

    public var movesRemaining: Int? {
        guard case let .daily(_, budget) = mode else { return nil }
        return max(0, budget - moveCount)
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
            spawnCount: Self.spawnCount(forMovesPlayed: moveCount)
        ) else { return nil }

        pushSnapshot(snapshot)
        board = result.finalBoard
        gravity = result.newGravity
        score += result.scoreDelta
        moveCount += 1
        cascadeCount += result.phases.filter { !$0.merges.isEmpty }.count
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
        rng = snapshot.rng
        nextTileID = snapshot.nextTileID
        undosUsed += 1
        return true
    }

    private func makeSnapshot() -> Snapshot {
        Snapshot(
            board: board, gravity: gravity, score: score, bestTile: bestTile,
            moveCount: moveCount, cascadeCount: cascadeCount, rng: rng, nextTileID: nextTileID
        )
    }

    private mutating func pushSnapshot(_ snapshot: Snapshot) {
        history.append(snapshot)
        if history.count > Self.historyLimit {
            history.removeFirst()
        }
    }
}
