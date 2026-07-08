import Foundation
import GravitileKit

/// Flattens a `MoveResult` into ordered animation steps. Pure data → the view
/// model just walks the list, so choreography stays testable and in one place.
enum AnimationStep: Equatable {
    /// Reposition existing tiles (slide phase or a fall).
    case moves([TileMove], duration: TimeInterval)
    /// Consumed tiles vanish into the merge cell; result tiles pop in.
    case merges([MergeEvent], round: Int)
    /// The board's gravity indicator flips to the new direction.
    case gravityCue(Direction)
    /// Boulders chipped by the merges just shown (hpAfter 0 = shattered).
    case iceChips([IceHit])
    /// Completed number bonds pop off the board with their equations.
    case clears([ClearEvent])
    /// Math Pop stage-up: leftover tiles sweep away, the banner announces the
    /// new target; the planner follows with a `.spawn` of the starters.
    case stageSweep(StageAdvance)
    /// Fresh tiles drop in from the entry edge together.
    case spawn([SpawnEvent])
}

enum AnimationPlanner {
    static let slideDuration: TimeInterval = 0.14
    static let fallDuration: TimeInterval = 0.16
    static let mergeDuration: TimeInterval = 0.10
    static let spawnDuration: TimeInterval = 0.12
    static let cueDuration: TimeInterval = 0.08
    static let chipDuration: TimeInterval = 0.12
    static let clearDuration: TimeInterval = 0.22
    static let sweepDuration: TimeInterval = 0.5

    static func steps(for result: MoveResult) -> [AnimationStep] {
        var steps: [AnimationStep] = []
        if !result.slide.moves.isEmpty {
            steps.append(.moves(result.slide.moves, duration: slideDuration))
        }
        if !result.slide.merges.isEmpty {
            steps.append(.merges(result.slide.merges, round: 0))
        }
        if !result.slide.iceHits.isEmpty {
            steps.append(.iceChips(result.slide.iceHits))
        }
        if !result.slide.clears.isEmpty {
            steps.append(.clears(result.slide.clears))
        }
        // Stasis holds the world still — no rotation cue to show.
        if !result.heldGravity {
            steps.append(.gravityCue(result.newGravity))
        }
        for phase in result.phases {
            if !phase.merges.isEmpty {
                steps.append(.merges(phase.merges, round: phase.round))
            }
            if !phase.iceHits.isEmpty {
                steps.append(.iceChips(phase.iceHits))
            }
            if !phase.clears.isEmpty {
                steps.append(.clears(phase.clears))
            }
            if !phase.falls.isEmpty {
                steps.append(.moves(phase.falls, duration: fallDuration))
            }
        }
        if !result.spawns.isEmpty {
            steps.append(.spawn(result.spawns))
        }
        if let advance = result.stageAdvance {
            steps.append(.stageSweep(advance))
            if !advance.starterSpawns.isEmpty {
                steps.append(.spawn(advance.starterSpawns))
            }
        }
        return steps
    }

    static func duration(of step: AnimationStep) -> TimeInterval {
        switch step {
        case let .moves(_, duration): duration
        case .merges: mergeDuration
        case .gravityCue: cueDuration
        case .iceChips: chipDuration
        case .clears: clearDuration
        case .stageSweep: sweepDuration
        case .spawn: spawnDuration
        }
    }
}
