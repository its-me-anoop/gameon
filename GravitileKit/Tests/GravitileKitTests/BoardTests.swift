import Testing
import Foundation
@testable import GravitileKit

@Suite struct BoardTests {
    @Test func newBoardIsEmpty() {
        let board = Board()
        #expect(board.tiles.isEmpty)
        #expect(board.emptyCoordinates.count == Board.size * Board.size)
        #expect(!board.isFull)
    }

    @Test func subscriptRoundTrips() {
        var board = Board()
        let c = Coordinate(row: 2, col: 3)
        board[c] = Tile(id: 1, value: 4)
        #expect(board[c] == Tile(id: 1, value: 4))
        board[c] = nil
        #expect(board[c] == nil)
    }

    @Test func emptyCoordinatesAreRowMajorAndExcludeOccupied() {
        var board = Board()
        board[Coordinate(row: 0, col: 0)] = Tile(id: 1, value: 2)
        let empties = board.emptyCoordinates
        #expect(empties.count == 24)
        #expect(empties.first == Coordinate(row: 0, col: 1))
        #expect(empties.last == Coordinate(row: 4, col: 4))
    }

    @Test func coordinateOffsets() {
        let c = Coordinate(row: 2, col: 2)
        #expect(c.offset(by: .up) == Coordinate(row: 1, col: 2))
        #expect(c.offset(by: .down) == Coordinate(row: 3, col: 2))
        #expect(c.offset(by: .left) == Coordinate(row: 2, col: 1))
        #expect(c.offset(by: .right) == Coordinate(row: 2, col: 3))
    }

    @Test func linesTowardDownOrderFromBottomEdge() {
        let lines = Board.lines(toward: .down)
        #expect(lines.count == 5)
        // First line is column 0 ordered bottom row first.
        #expect(lines[0].first == Coordinate(row: 4, col: 0))
        #expect(lines[0].last == Coordinate(row: 0, col: 0))
        #expect(lines[0].count == 5)
    }

    @Test func linesTowardLeftOrderFromLeftEdge() {
        let lines = Board.lines(toward: .left)
        #expect(lines.count == 5)
        // First line is row 0 ordered left column first.
        #expect(lines[0].first == Coordinate(row: 0, col: 0))
        #expect(lines[0].last == Coordinate(row: 0, col: 4))
    }

    @Test func linesTowardUpAndRight() {
        #expect(Board.lines(toward: .up)[0].first == Coordinate(row: 0, col: 0))
        #expect(Board.lines(toward: .up)[0].last == Coordinate(row: 4, col: 0))
        #expect(Board.lines(toward: .right)[0].first == Coordinate(row: 0, col: 4))
        #expect(Board.lines(toward: .right)[0].last == Coordinate(row: 0, col: 0))
    }

    @Test func isFullWhenAllCellsOccupied() {
        var board = Board()
        var id = 0
        for row in 0..<Board.size {
            for col in 0..<Board.size {
                id += 1
                board[Coordinate(row: row, col: col)] = Tile(id: id, value: 2)
            }
        }
        #expect(board.isFull)
        #expect(board.emptyCoordinates.isEmpty)
    }

    @Test func boardCodableRoundTrip() throws {
        var board = Board()
        board[Coordinate(row: 1, col: 1)] = Tile(id: 7, value: 8)
        board[Coordinate(row: 4, col: 0)] = Tile(id: 9, value: 2)
        let data = try JSONEncoder().encode(board)
        let decoded = try JSONDecoder().decode(Board.self, from: data)
        #expect(decoded == board)
    }
}
