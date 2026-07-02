/// A cell position on the board. Row 0 is the top edge, column 0 the left edge.
public struct Coordinate: Hashable, Codable, Sendable {
    public var row: Int
    public var col: Int

    public init(row: Int, col: Int) {
        self.row = row
        self.col = col
    }

    public func offset(by direction: Direction) -> Coordinate {
        let (dr, dc) = direction.step
        return Coordinate(row: row + dr, col: col + dc)
    }
}
