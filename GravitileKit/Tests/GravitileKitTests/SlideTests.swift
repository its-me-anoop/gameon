import Testing
@testable import GravitileKit

/// Builds a board from a compact picture: rows top to bottom, "." = empty,
/// numbers are tile values. Tile IDs are assigned 1, 2, 3… in row-major order.
func makeBoard(_ rows: [[Int]]) -> Board {
    var board = Board()
    var id = 0
    for (r, row) in rows.enumerated() {
        for (c, value) in row.enumerated() where value > 0 {
            id += 1
            board[Coordinate(row: r, col: c)] = Tile(id: id, value: value)
        }
    }
    return board
}

/// Reads back values in row-major order (0 = empty) for whole-board assertions.
func values(of board: Board) -> [[Int]] {
    (0..<Board.size).map { r in
        (0..<Board.size).map { c in board[Coordinate(row: r, col: c)]?.value ?? 0 }
    }
}

let e = 0 // empty cell marker for readable literals

@Suite struct SlideTests {
    @Test func singleTileSlidesToEachEdge() {
        for (direction, expected) in [
            (Direction.left, Coordinate(row: 2, col: 0)),
            (.right, Coordinate(row: 2, col: 4)),
            (.up, Coordinate(row: 0, col: 2)),
            (.down, Coordinate(row: 4, col: 2)),
        ] {
            let board = makeBoard([
                [e, e, e, e, e],
                [e, e, e, e, e],
                [e, e, 2, e, e],
                [e, e, e, e, e],
                [e, e, e, e, e],
            ])
            var nextID = 100
            let outcome = MoveResolver.slide(board, toward: direction, nextTileID: &nextID)
            #expect(outcome.changed)
            #expect(outcome.board[expected]?.value == 2)
            #expect(outcome.merges.isEmpty)
            #expect(outcome.moves == [TileMove(tileID: 1, from: Coordinate(row: 2, col: 2), to: expected)])
        }
    }

    @Test func equalPairMergesTowardEdge() {
        let board = makeBoard([
            [2, 2, e, e, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
        ])
        var nextID = 100
        let outcome = MoveResolver.slide(board, toward: .left, nextTileID: &nextID)
        #expect(values(of: outcome.board)[0] == [4, e, e, e, e])
        #expect(outcome.merges.count == 1)
        let merge = outcome.merges[0]
        #expect(Set(merge.consumedTileIDs) == [1, 2])
        #expect(merge.resultTile.value == 4)
        #expect(merge.resultTile.id == 100)
        #expect(merge.at == Coordinate(row: 0, col: 0))
        #expect(merge.points == 4)
        #expect(merge.multiplier == 1)
        #expect(nextID == 101)
    }

    @Test func tripleMergesEdgeNearestPairFirst() {
        let board = makeBoard([
            [2, 2, 2, e, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
        ])
        var nextID = 100
        let outcome = MoveResolver.slide(board, toward: .left, nextTileID: &nextID)
        #expect(values(of: outcome.board)[0] == [4, 2, e, e, e])
        #expect(outcome.merges.count == 1)
        #expect(Set(outcome.merges[0].consumedTileIDs) == [1, 2])
    }

    @Test func quadrupleMergesIntoTwoPairsWithoutChaining() {
        let board = makeBoard([
            [2, 2, 2, 2, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
        ])
        var nextID = 100
        let outcome = MoveResolver.slide(board, toward: .left, nextTileID: &nextID)
        #expect(values(of: outcome.board)[0] == [4, 4, e, e, e])
        #expect(outcome.merges.count == 2)
    }

    @Test func mergedTileDoesNotChainWithinOneSlide() {
        let board = makeBoard([
            [4, 2, 2, e, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
        ])
        var nextID = 100
        let outcome = MoveResolver.slide(board, toward: .left, nextTileID: &nextID)
        // The 2+2 pair merges to 4 but must not merge into the leading 4.
        #expect(values(of: outcome.board)[0] == [4, 4, e, e, e])
        #expect(outcome.merges.count == 1)
    }

    @Test func unmovableBoardReportsUnchanged() {
        let board = makeBoard([
            [2, 4, 8, 16, 32],
            [e, e, e, e, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
        ])
        var nextID = 100
        let outcome = MoveResolver.slide(board, toward: .left, nextTileID: &nextID)
        #expect(!outcome.changed)
        #expect(outcome.moves.isEmpty)
        #expect(outcome.merges.isEmpty)
        #expect(outcome.board == board)
    }

    @Test func slideAffectsAllLinesIndependently() {
        let board = makeBoard([
            [2, e, e, e, 2],
            [e, 4, e, 4, e],
            [e, e, 8, e, e],
            [e, 16, e, 2, e],
            [32, e, e, e, 4],
        ])
        var nextID = 100
        let outcome = MoveResolver.slide(board, toward: .right, nextTileID: &nextID)
        #expect(values(of: outcome.board) == [
            [e, e, e, e, 4],
            [e, e, e, e, 8],
            [e, e, e, e, 8],
            [e, e, e, 16, 2],
            [e, e, e, 32, 4],
        ])
        #expect(outcome.merges.count == 2)
    }

    @Test func moveEventsRecordConsumedTilesDestination() {
        let board = makeBoard([
            [e, 2, e, 2, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
        ])
        var nextID = 100
        let outcome = MoveResolver.slide(board, toward: .left, nextTileID: &nextID)
        // Both consumed tiles must have a TileMove ending at the merge cell so
        // the UI can animate them into the collision point.
        let destinations = outcome.moves.map(\.to)
        #expect(destinations.allSatisfy { $0 == Coordinate(row: 0, col: 0) })
        #expect(Set(outcome.moves.map(\.tileID)) == [1, 2])
    }

    @Test func verticalSlideMergesDownward() {
        let board = makeBoard([
            [e, e, 2, e, e],
            [e, e, e, e, e],
            [e, e, 2, e, e],
            [e, e, e, e, e],
            [e, e, 4, e, e],
        ])
        var nextID = 100
        let outcome = MoveResolver.slide(board, toward: .down, nextTileID: &nextID)
        #expect(values(of: outcome.board).map { $0[2] } == [e, e, e, 4, 4])
        #expect(outcome.merges.count == 1)
        #expect(outcome.merges[0].at == Coordinate(row: 3, col: 2))
    }
}
