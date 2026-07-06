/// Events describe everything that happens during move resolution as plain
/// data. The app's animation planner replays them phase by phase; the engine
/// never touches UI.

public struct TileMove: Equatable, Codable, Sendable {
    public let tileID: Int
    public let from: Coordinate
    public let to: Coordinate

    public init(tileID: Int, from: Coordinate, to: Coordinate) {
        self.tileID = tileID
        self.from = from
        self.to = to
    }
}

public struct MergeEvent: Equatable, Codable, Sendable {
    /// The two tiles that collided; they disappear at `at`.
    public let consumedTileIDs: [Int]
    /// The freshly minted doubled tile occupying `at`.
    public let resultTile: Tile
    public let at: Coordinate
    /// Points awarded: result value × multiplier.
    public let points: Int
    /// 1 during the slide phase; the cascade round number otherwise.
    public let multiplier: Int
}

/// One HP chipped off a boulder by an orthogonally-adjacent merge.
public struct IceHit: Equatable, Codable, Sendable {
    public let tileID: Int
    public let at: Coordinate
    /// Remaining HP; 0 means the tile just shattered free.
    public let hpAfter: Int
}

public struct SlideOutcome: Equatable, Sendable {
    public let board: Board
    public let moves: [TileMove]
    public let merges: [MergeEvent]
    public var iceHits: [IceHit] = []
    /// A swipe is legal iff its slide outcome changed the board.
    public var changed: Bool { !(moves.isEmpty && merges.isEmpty) }
}
