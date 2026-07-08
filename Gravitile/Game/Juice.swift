import SwiftUI
import GravitileKit

/// Milestone tiles worth a celebration, with once-per-game bookkeeping.
struct MilestoneTracker: Equatable {
    static let values = [256, 512, 1024, 2048, 4096, 8192]
    private var celebrated: Set<Int>

    /// Resuming a game must not re-celebrate tiles it already has.
    init(alreadyReached bestTile: Int = 0) {
        celebrated = Set(Self.values.filter { $0 <= bestTile })
    }

    /// The highest newly crossed milestone, or nil. Crossing several at once
    /// (a cascade jumping 128 → 512) celebrates only the biggest.
    mutating func newlyReached(bestTile: Int) -> Int? {
        let hits = Self.values.filter { $0 <= bestTile && !celebrated.contains($0) }
        guard let top = hits.max() else { return nil }
        celebrated.formUnion(hits)
        return top
    }
}

/// One floating "+N" score delta above the score badge.
struct ScorePop: Identifiable, Equatable {
    let id: Int
    let points: Int
    let round: Int
}

/// One floating "3 + 7 = 10" over a popped bond — Math Pop's teaching moment.
struct EquationPop: Identifiable, Equatable {
    let id: Int
    let text: String
    let coordinate: Coordinate
}

/// Decaying horizontal jitter; `travel` animates in whole-number increments,
/// one unit per shake, so the board always comes to rest centred.
struct ShakeEffect: GeometryEffect {
    var travel: CGFloat
    var magnitude: CGFloat

    var animatableData: CGFloat {
        get { travel }
        set { travel = newValue }
    }

    func effectValue(size: CGSize) -> ProjectionTransform {
        let phase = travel - travel.rounded(.down)
        let translation = sin(phase * .pi * 5) * magnitude * (1 - phase)
        return ProjectionTransform(CGAffineTransform(translationX: translation, y: 0))
    }
}

extension Direction {
    /// Small board nudge toward the new gravity edge — the "felt" half of the
    /// rotation cue (the compass is the "seen" half).
    var nudgeOffset: CGSize {
        switch self {
        case .up: CGSize(width: 0, height: -5)
        case .down: CGSize(width: 0, height: 5)
        case .left: CGSize(width: -5, height: 0)
        case .right: CGSize(width: 5, height: 0)
        }
    }
}
