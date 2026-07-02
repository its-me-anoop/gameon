/// One post-rotation settling step. Round 0 is the initial fall after gravity
/// rotates (no merges). Rounds ≥ 1 pair the round's cascade merges with the
/// falls that follow them, so the UI animates merge → tumble per round.
public struct CascadePhase: Equatable, Sendable {
    public let falls: [TileMove]
    public let merges: [MergeEvent]
    public let round: Int
}

public struct SpawnEvent: Equatable, Sendable {
    public let tile: Tile
    /// Cell on the opposite-gravity edge where the tile visually enters.
    public let enteredAt: Coordinate
    public let restedAt: Coordinate
}

/// Everything that happened for one legal swipe, in animation order:
/// slide events → gravity rotation → phases (fall/merge rounds) → spawn.
public struct MoveResult: Equatable, Sendable {
    public let swipe: Direction
    public let slide: SlideOutcome
    public let newGravity: Direction
    public let phases: [CascadePhase]
    public let spawn: SpawnEvent?
    public let scoreDelta: Int
    /// Board state after every phase and the spawn.
    public let finalBoard: Board
}
