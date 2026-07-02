/// A numbered tile. IDs are issued by an incrementing counter in game state so
/// that games are fully deterministic and SwiftUI can animate stable identities.
public struct Tile: Identifiable, Hashable, Codable, Sendable {
    public let id: Int
    public var value: Int

    public init(id: Int, value: Int) {
        self.id = id
        self.value = value
    }
}
