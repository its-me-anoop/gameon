/// A 5×5 grid of optional tiles, stored row-major.
public struct Board: Equatable, Codable, Sendable {
    public static let size = 5

    private var cells: [Tile?]

    public init() {
        cells = Array(repeating: nil, count: Board.size * Board.size)
    }

    public subscript(_ coordinate: Coordinate) -> Tile? {
        get { cells[coordinate.row * Board.size + coordinate.col] }
        set { cells[coordinate.row * Board.size + coordinate.col] = newValue }
    }

    /// All occupied cells in row-major order.
    public var tiles: [(Coordinate, Tile)] {
        allCoordinates.compactMap { c in self[c].map { (c, $0) } }
    }

    /// All empty cells in row-major order.
    public var emptyCoordinates: [Coordinate] {
        allCoordinates.filter { self[$0] == nil }
    }

    public var isFull: Bool { !cells.contains(nil) }

    public static func contains(_ c: Coordinate) -> Bool {
        (0..<size).contains(c.row) && (0..<size).contains(c.col)
    }

    private var allCoordinates: [Coordinate] {
        (0..<Board.size).flatMap { row in
            (0..<Board.size).map { col in Coordinate(row: row, col: col) }
        }
    }

    /// The board decomposed into lines parallel to `direction`, each ordered
    /// starting at the edge that tiles move toward. Sliding, falling, and
    /// cascade merging all walk these lines from index 0 inward.
    public static func lines(toward direction: Direction) -> [[Coordinate]] {
        let range = Array(0..<size)
        switch direction {
        case .down:
            return range.map { col in range.reversed().map { Coordinate(row: $0, col: col) } }
        case .up:
            return range.map { col in range.map { Coordinate(row: $0, col: col) } }
        case .left:
            return range.map { row in range.map { Coordinate(row: row, col: $0) } }
        case .right:
            return range.map { row in range.reversed().map { Coordinate(row: row, col: $0) } }
        }
    }
}
