/// A numbered tile. IDs are issued by an incrementing counter in game state so
/// that games are fully deterministic and SwiftUI can animate stable identities.
public struct Tile: Identifiable, Hashable, Codable, Sendable {
    public let id: Int
    public var value: Int
    /// Ice hit-points; 0 is a normal tile. Iced tiles ("boulders") slide and
    /// fall like any tile but never merge; each orthogonally-adjacent merge
    /// chips one HP, and at zero the tile is freed (v1.2 spec §3.2).
    public var ice: Int

    public init(id: Int, value: Int, ice: Int = 0) {
        self.id = id
        self.value = value
        self.ice = ice
    }

    /// Tiles persist inside saved games from before `ice` existed.
    public init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        id = try c.decode(Int.self, forKey: .id)
        value = try c.decode(Int.self, forKey: .value)
        ice = try c.decodeIfPresent(Int.self, forKey: .ice) ?? 0
    }
}
