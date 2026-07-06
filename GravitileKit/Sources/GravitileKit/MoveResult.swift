/// One post-rotation settling step. Round 0 is the initial fall after gravity
/// rotates (no merges). Rounds ≥ 1 pair the round's cascade merges with the
/// falls that follow them, so the UI animates merge → tumble per round.
public struct CascadePhase: Equatable, Sendable {
    public let falls: [TileMove]
    public let merges: [MergeEvent]
    public let round: Int
    /// Boulders chipped by this round's merges (v1.2 spec §3.2).
    public var iceHits: [IceHit] = []
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
    /// One or more spawns depending on the pressure ramp (spec §3 step 5).
    public let spawns: [SpawnEvent]
    public let scoreDelta: Int
    /// Board state after every phase and the spawn.
    public let finalBoard: Board
    /// Stasis: gravity was held in place for this move (v1.2 spec §3.1).
    public var heldGravity: Bool = false
}
