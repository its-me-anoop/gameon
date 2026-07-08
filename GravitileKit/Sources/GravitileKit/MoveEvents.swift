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

/// A completed number bond popping off the board (Math Pop, v1.3 spec §3.3).
/// `addends` are the two values that met — the UI's "3 + 7 = 10".
public struct ClearEvent: Equatable, Codable, Sendable {
    public let tileID: Int
    public let at: Coordinate
    public let value: Int
    public let addends: [Int]
}

public struct SlideOutcome: Equatable, Sendable {
    public let board: Board
    public let moves: [TileMove]
    public let merges: [MergeEvent]
    public var iceHits: [IceHit] = []
    /// Bond pops under `.sumTarget`; always empty under `.doubling`.
    public var clears: [ClearEvent] = []
    /// A swipe is legal iff its slide outcome changed the board.
    public var changed: Bool { !(moves.isEmpty && merges.isEmpty) }
}
