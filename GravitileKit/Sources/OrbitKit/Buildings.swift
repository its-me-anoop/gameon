import Foundation

/// The five things a world produces. Ore and Charge are raw; Water, Alloy and
/// Biomass are refined from them, which is what makes climate placement matter
/// — a refinery is only as good as the tiles feeding it.
public enum ResourceKind: String, Codable, CaseIterable, Sendable, Identifiable {
    case ore, charge, water, alloy, biomass

    public var id: String { rawValue }

    public var displayName: String {
        switch self {
        case .ore: "Ore"
        case .charge: "Charge"
        case .water: "Water"
        case .alloy: "Alloy"
        case .biomass: "Biomass"
        }
    }

    public var symbol: String {
        switch self {
        case .ore: "cube.fill"
        case .charge: "bolt.fill"
        case .water: "drop.fill"
        case .alloy: "square.stack.3d.up.fill"
        case .biomass: "leaf.fill"
        }
    }
}

/// A bundle of resources — a stock, a cost, or a per-second rate.
public struct Resources: Codable, Sendable, Equatable {
    public var ore: Double
    public var charge: Double
    public var water: Double
    public var alloy: Double
    public var biomass: Double

    public static let zero = Resources()

    public init(
        ore: Double = 0, charge: Double = 0, water: Double = 0,
        alloy: Double = 0, biomass: Double = 0
    ) {
        self.ore = ore
        self.charge = charge
        self.water = water
        self.alloy = alloy
        self.biomass = biomass
    }

    public subscript(kind: ResourceKind) -> Double {
        get {
            switch kind {
            case .ore: ore
            case .charge: charge
            case .water: water
            case .alloy: alloy
            case .biomass: biomass
            }
        }
        set {
            switch kind {
            case .ore: ore = newValue
            case .charge: charge = newValue
            case .water: water = newValue
            case .alloy: alloy = newValue
            case .biomass: biomass = newValue
            }
        }
    }

    public var nonZeroKinds: [ResourceKind] {
        ResourceKind.allCases.filter { self[$0] > 0 }
    }

    public func covers(_ cost: Resources) -> Bool {
        ResourceKind.allCases.allSatisfy { self[$0] >= cost[$0] - 1e-9 }
    }

    public static func + (lhs: Resources, rhs: Resources) -> Resources {
        var result = lhs
        for kind in ResourceKind.allCases { result[kind] += rhs[kind] }
        return result
    }

    public static func - (lhs: Resources, rhs: Resources) -> Resources {
        var result = lhs
        for kind in ResourceKind.allCases { result[kind] -= rhs[kind] }
        return result
    }

    public static func * (lhs: Resources, scalar: Double) -> Resources {
        var result = lhs
        for kind in ResourceKind.allCases { result[kind] *= scalar }
        return result
    }

    public mutating func clamp(to caps: Resources) {
        for kind in ResourceKind.allCases {
            self[kind] = min(max(self[kind], 0), caps[kind])
        }
    }
}

/// What you can put on a tile. Ballast is the pure expression of the game's
/// mechanic: it produces nothing at all and exists only to move mass, and
/// therefore to steer the axis.
public enum BuildingKind: String, Codable, CaseIterable, Sendable, Identifiable {
    case solarArray, oreMine, condenser, smelter, greenhouse, beacon, ballast

    public var id: String { rawValue }

    public var displayName: String {
        switch self {
        case .solarArray: "Solar Array"
        case .oreMine: "Ore Mine"
        case .condenser: "Ice Condenser"
        case .smelter: "Smelter"
        case .greenhouse: "Greenhouse"
        case .beacon: "Beacon"
        case .ballast: "Ballast"
        }
    }

    /// One line, shown in the build tray. Says what it wants, not what it is.
    public var tagline: String {
        switch self {
        case .solarArray: "Charge. Wants the most sunlight it can get."
        case .oreMine: "Ore. Works anywhere; loves rich crust."
        case .condenser: "Water. Peaks right on the frost line."
        case .smelter: "Alloy from ore and charge. Needs heat."
        case .greenhouse: "Biomass from water and charge. Needs mild ground."
        case .beacon: "Lifts every neighbor's output. Almost weightless."
        case .ballast: "Produces nothing. Very heavy. Steers your world."
        }
    }

    /// Mass at level 1. This is the second, hidden price of every building.
    public var baseMass: Double {
        switch self {
        case .solarArray: 1.0
        case .oreMine: 2.5
        case .condenser: 1.5
        case .smelter: 4.0
        case .greenhouse: 2.0
        case .beacon: 0.4
        case .ballast: 18.0
        }
    }

    /// Cost of the first level; later levels scale by `costGrowth`.
    public var baseCost: Resources {
        switch self {
        case .solarArray: Resources(ore: 25)
        case .oreMine: Resources(ore: 40)
        case .condenser: Resources(ore: 60, charge: 30)
        case .smelter: Resources(ore: 150, charge: 80)
        case .greenhouse: Resources(water: 120, alloy: 60)
        case .beacon: Resources(charge: 120, alloy: 150)
        // Ballast costs alloy, so steering the world is gated behind running a
        // smelter, which is gated behind finding heat. The mechanic has to be
        // earned before it can be used freely.
        case .ballast: Resources(ore: 120, alloy: 25)
        }
    }

    /// Collapse tier at which the kind becomes available.
    public var unlockTier: Int {
        switch self {
        case .solarArray, .oreMine, .ballast: 0
        case .condenser, .smelter: 0
        case .greenhouse: 1
        case .beacon: 1
        }
    }

    public static let costGrowth = 1.75
    public static let outputGrowth = 1.6
    public static let massGrowth = 1.35

    /// Cost of reaching `level`; level 1 is the initial placement.
    ///
    /// Upgrades additionally demand Water and then Alloy — coolant and plating.
    /// That is what gives the refined resources a purpose from the very first
    /// world, and it is why the climate has to be steered: Water only comes
    /// from the frost line and Alloy only from heat, so a player who never
    /// moves their axis runs out of ways to grow.
    public func cost(forLevel level: Int) -> Resources {
        var cost = baseCost * pow(Self.costGrowth, Double(level - 1))
        if level >= 2 { cost.water += 20 * pow(1.9, Double(level - 2)) }
        if level >= 3 { cost.alloy += 15 * pow(1.9, Double(level - 3)) }
        return cost
    }

    public func mass(atLevel level: Int) -> Double {
        baseMass * pow(Self.massGrowth, Double(level - 1))
    }

    public func levelScale(_ level: Int) -> Double {
        pow(Self.outputGrowth, Double(level - 1))
    }

    /// Gross output per second at level 1, before efficiency and bonuses.
    public var baseOutput: (kind: ResourceKind, rate: Double)? {
        switch self {
        case .solarArray: (.charge, 0.55)
        case .oreMine: (.ore, 0.45)
        case .condenser: (.water, 0.35)
        case .smelter: (.alloy, 0.14)
        case .greenhouse: (.biomass, 0.22)
        case .beacon, .ballast: nil
        }
    }

    /// Inputs consumed per second at level 1, at full rate.
    public var baseInput: Resources {
        switch self {
        case .condenser: Resources(charge: 0.18)
        case .smelter: Resources(ore: 0.55, charge: 0.45)
        case .greenhouse: Resources(charge: 0.22, water: 0.28)
        default: .zero
        }
    }

    /// Output bonus this building grants to each adjacent tile.
    public func neighborBonus(atLevel level: Int) -> Double {
        self == .beacon ? 0.12 * Double(level) : 0
    }

    /// How well the kind performs at a given temperature index and insolation.
    /// These curves are the reason the axis matters: steering the climate bands
    /// is how you turn a mediocre tile into a good one.
    public func efficiency(temperatureIndex t: Double, solarFactor: Double) -> Double {
        switch self {
        case .solarArray:
            return min(solarFactor, 3.2)
        case .oreMine:
            return 1.0
        case .condenser:
            return bell(t, center: Climate.freezingIndex + 0.04, width: 0.13)
        case .smelter:
            return min(max((t - 0.45) / 0.4, 0), 1.15)
        case .greenhouse:
            return bell(t, center: 0.5, width: 0.16)
        case .beacon, .ballast:
            return 1.0
        }
    }

    private func bell(_ x: Double, center: Double, width: Double) -> Double {
        let z = (x - center) / width
        return exp(-0.5 * z * z)
    }
}

/// A placed building.
public struct Building: Codable, Sendable, Equatable {
    public var kind: BuildingKind
    public var level: Int

    public init(kind: BuildingKind, level: Int = 1) {
        self.kind = kind
        self.level = level
    }

    public var mass: Double { kind.mass(atLevel: level) }
    public var upgradeCost: Resources { kind.cost(forLevel: level + 1) }
}

/// Per-tile ground properties, derived from the world seed so they never need
/// persisting and always replay identically.
public struct TileTerrain: Sendable, Equatable {
    public let oreRichness: Double
    public let hasIce: Bool
    /// Purely visual height jitter, `0…1`.
    public let roughness: Double

    public static func derive(seed: UInt64, tile: Int) -> TileTerrain {
        var rng = SplitMix64(seed: seed &+ fnv1a("terrain-\(tile)"))
        return TileTerrain(
            oreRichness: rng.double(in: 0.7...1.5),
            hasIce: rng.unitDouble() < 0.18,
            roughness: rng.unitDouble()
        )
    }
}
