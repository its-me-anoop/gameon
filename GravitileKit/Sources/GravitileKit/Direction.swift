/// A swipe or gravity direction on the board.
///
/// Grid space puts row 0 at the top of the screen, so `.down` increases row.
public enum Direction: String, CaseIterable, Sendable, Codable, Hashable {
    case up, down, left, right

    /// The next gravity direction after a move: down → left → up → right → down,
    /// which reads as a clockwise turn on screen.
    public var rotatedClockwise: Direction {
        switch self {
        case .down: .left
        case .left: .up
        case .up: .right
        case .right: .down
        }
    }

    public var opposite: Direction {
        switch self {
        case .up: .down
        case .down: .up
        case .left: .right
        case .right: .left
        }
    }

    /// Unit step in grid space (row delta, column delta).
    public var step: (dr: Int, dc: Int) {
        switch self {
        case .up: (-1, 0)
        case .down: (1, 0)
        case .left: (0, -1)
        case .right: (0, 1)
        }
    }
}
