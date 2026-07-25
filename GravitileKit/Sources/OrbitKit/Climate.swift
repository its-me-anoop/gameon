import simd

/// Where a tile sits in the world's temperature range. Bands are drawn on the
/// planet, and every building's efficiency curve is written against them.
public enum Biome: String, Sendable, CaseIterable, Codable {
    case frozen, tundra, temperate, arid, molten

    public var displayName: String {
        switch self {
        case .frozen: "Frozen"
        case .tundra: "Tundra"
        case .temperate: "Temperate"
        case .arid: "Arid"
        case .molten: "Molten"
        }
    }
}

/// The world's lighting model. The star is fixed in space; the spin axis moves,
/// so climate is a function of the axis the player is steering.
///
/// Insolation uses the standard rotation-averaged (daily-mean) formula, which
/// makes two very different configurations available and both of them valid:
/// an axis perpendicular to the star gives the familiar hot equator and frozen
/// poles, while an axis aimed at the star puts one pole in permanent day —
/// three times the peak yield, and half the world in permanent night.
public struct Climate: Sendable, Equatable {
    /// Peak rotation-averaged insolation of an equatorial tile when the axis is
    /// perpendicular to the star (`cos φ / π` at `φ = 0`). Yields are expressed
    /// relative to this so "1.0" means "a good ordinary equator tile".
    public static let referenceInsolation = 1.0 / Double.pi

    /// Temperature index at which water freezes; the frost line is this contour.
    public static let freezingIndex = 0.18

    public let axis: Vec3
    public let starDirection: Vec3
    /// Global warming offset from atmosphere upgrades, added to every tile.
    public let greenhouse: Double

    public init(axis: Vec3, starDirection: Vec3 = Vec3(1, 0, 0), greenhouse: Double = 0) {
        self.axis = normalize(axis)
        self.starDirection = normalize(starDirection)
        self.greenhouse = greenhouse
    }

    /// Star's declination: the latitude directly under the star. ±90° means the
    /// axis points at (or away from) the star.
    public var declination: Double {
        asin(min(max(dot(starDirection, axis), -1), 1))
    }

    /// Latitude of a surface point relative to the current spin axis.
    public func latitude(of position: Vec3) -> Double {
        asin(min(max(dot(normalize(position), axis), -1), 1))
    }

    /// Rotation-averaged insolation, `0…1`. Zero is polar night.
    public func insolation(at position: Vec3) -> Double {
        insolation(latitude: latitude(of: position))
    }

    public func insolation(latitude: Double) -> Double {
        // Clamp away from the poles so `tan` stays finite; the clamped value is
        // within a thousandth of the limit, which no player can perceive.
        let limit = 89.5 * .pi / 180
        let phi = min(max(latitude, -limit), limit)
        let delta = min(max(declination, -limit), limit)

        let cosHourAngle = min(max(-tan(phi) * tan(delta), -1), 1)
        let h0 = acos(cosHourAngle)
        let mean = (h0 * sin(phi) * sin(delta) + cos(phi) * cos(delta) * sin(h0)) / .pi
        return max(0, mean)
    }

    /// Solar yield multiplier: `1.0` on a good ordinary equator tile, up to
    /// ~3.1 on a pole aimed straight at the star.
    public func solarFactor(at position: Vec3) -> Double {
        insolation(at: position) / Self.referenceInsolation
    }

    /// Normalized temperature, `0…1`, from insolation plus the greenhouse
    /// offset. The divisor puts an ordinary equator tile in the arid band.
    public func temperatureIndex(at position: Vec3) -> Double {
        temperatureIndex(insolation: insolation(at: position))
    }

    public func temperatureIndex(insolation: Double) -> Double {
        min(max(insolation / 0.45 + greenhouse, 0), 1)
    }

    /// Flavor readout for the tile inspector.
    public func celsius(at position: Vec3) -> Double {
        -140 + temperatureIndex(at: position) * 300
    }

    public func biome(at position: Vec3) -> Biome {
        Self.biome(temperatureIndex: temperatureIndex(at: position))
    }

    public static func biome(temperatureIndex t: Double) -> Biome {
        switch t {
        case ..<freezingIndex: .frozen
        case ..<0.34: .tundra
        case ..<0.58: .temperate
        case ..<0.82: .arid
        default: .molten
        }
    }

    /// Latitudes where temperature crosses freezing — the rings the renderer
    /// draws, and the lines Condensers want to sit on.
    ///
    /// There are two of them when the axis is perpendicular to the star (a
    /// frozen cap at each pole), one when the world is tilted far enough that
    /// a pole comes into permanent day, and none when the whole surface is on
    /// one side of freezing. Found by scanning for sign changes and bisecting,
    /// since the insolation formula has no closed-form inverse.
    public func frostLatitudes() -> [Double] {
        let steps = 180
        func excess(_ latitude: Double) -> Double {
            temperatureIndex(insolation: insolation(latitude: latitude)) - Self.freezingIndex
        }

        var crossings: [Double] = []
        var previousLatitude = -Double.pi / 2
        var previousExcess = excess(previousLatitude)

        for step in 1...steps {
            let latitude = -Double.pi / 2 + Double(step) / Double(steps) * .pi
            let value = excess(latitude)
            if previousExcess == 0 {
                crossings.append(previousLatitude)
            } else if previousExcess * value < 0 {
                var low = previousLatitude, high = latitude
                let lowIsCold = previousExcess < 0
                for _ in 0..<32 {
                    let mid = (low + high) / 2
                    if (excess(mid) < 0) == lowIsCold { low = mid } else { high = mid }
                }
                crossings.append((low + high) / 2)
            }
            previousLatitude = latitude
            previousExcess = value
        }
        return crossings
    }
}
