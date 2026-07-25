import Foundation
import simd

/// An ore meteor on approach. Tap it before it arrives and it shatters into
/// ore; miss it and it burns up harmlessly. Meteors are the reason to open the
/// app rather than only collect from it.
public struct Meteor: Codable, Sendable, Equatable, Identifiable {
    public let id: UInt64
    public let targetTile: Int
    public let spawnedAt: Double
    public let arrivesAt: Double
    public let reward: Double

    /// Approach progress `0…1` at a given world time.
    public func progress(at time: Double) -> Double {
        guard arrivesAt > spawnedAt else { return 1 }
        return min(max((time - spawnedAt) / (arrivesAt - spawnedAt), 0), 1)
    }
}

/// Everything about a world that has to survive a relaunch. Geometry and
/// climate are derived, never stored — the tiling is a pure function of the
/// collapse tier, and terrain is a pure function of the seed.
public struct WorldState: Codable, Sendable, Equatable {
    public var seed: UInt64
    /// Collapse tier: drives tile count, crust richness and storage.
    public var tier: Int
    public var gravity: Int
    public var lifetimeGravity: Int
    public var collapses: Int

    public var axis: Vec3
    public var spinPhase: Double

    public var resources: Resources
    public var buildings: [Int: Building]

    /// Seconds simulated since the last collapse, and over the world's life.
    public var elapsed: Double
    public var lifetimeElapsed: Double
    public var peakMass: Double
    public var lifetimePeakMass: Double
    public var placements: Int

    public var rng: SplitMix64
    public var activeMeteor: Meteor?
    public var nextMeteorAt: Double
    public var nextQuakeCheckAt: Double
    public var crackedUntil: [Int: Double]
    public var meteorsCaught: Int

    public var lastPlayed: Date
    /// Wall-clock seconds from a fresh world to its first collapse, for the
    /// speedrun board. Set once, never overwritten.
    public var firstCollapseSeconds: Double?
    /// Peak wobble reached and then settled — the "you steered it" achievement.
    public var maxSettledWobble: Double

    public static let startingResources = Resources(ore: 70)

    public init(seed: UInt64 = UInt64.random(in: 1...UInt64.max), now: Date = Date()) {
        self.seed = seed
        tier = 0
        gravity = 0
        lifetimeGravity = 0
        collapses = 0
        // A world starts spinning perpendicular to the star — zero declination,
        // which is the readable configuration everyone already understands: a
        // hot equator and a frozen cap at each pole. Steering away from it is
        // the player's first real decision.
        axis = normalize(Vec3(0, 1, 0.12))
        spinPhase = 0
        resources = Self.startingResources
        buildings = [:]
        elapsed = 0
        lifetimeElapsed = 0
        peakMass = 0
        lifetimePeakMass = 0
        placements = 0
        rng = SplitMix64(seed: seed &+ 0x51_ED_270B_2B37_1A5F)
        activeMeteor = nil
        nextMeteorAt = 45
        nextQuakeCheckAt = 60
        crackedUntil = [:]
        meteorsCaught = 0
        lastPlayed = now
        firstCollapseSeconds = nil
        maxSettledWobble = 0
    }
}

/// What happened during a simulated stretch of time. The shell turns these into
/// sound, haptics and floating numbers.
public struct TickEvents: Sendable, Equatable {
    public var gained = Resources.zero
    public var meteorSpawned: Meteor?
    public var meteorMissed = false
    public var meteorsAutoCollected = 0
    public var autoCollectedOre: Double = 0
    public var quakedTiles: [Int] = []
    public var repairedTiles: [Int] = []

    public var isQuiet: Bool {
        meteorSpawned == nil && !meteorMissed && meteorsAutoCollected == 0
            && quakedTiles.isEmpty && repairedTiles.isEmpty
    }
}

/// Per-tile numbers for the inspector and the renderer.
public struct TileReadout: Sendable, Equatable {
    public let tile: Int
    public let building: Building?
    public let terrain: TileTerrain
    public let temperatureIndex: Double
    public let solarFactor: Double
    public let biome: Biome
    /// Combined multiplier: efficiency × terrain × neighbors × level × gravity.
    public let yieldMultiplier: Double
    public let output: Resources
    public let input: Resources
    public let isCracked: Bool
}
