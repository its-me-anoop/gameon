import Testing
@testable import GravitileKit

@Suite struct FallTests {
    @Test func tilesCompactTowardGravityPreservingOrder() {
        let board = makeBoard([
            [2, e, e, e, e],
            [e, e, e, e, e],
            [4, e, e, e, e],
            [e, e, e, e, e],
            [8, e, e, e, e],
        ])
        let (fallen, moves) = MoveResolver.fall(board, gravity: .down)
        #expect(values(of: fallen).map { $0[0] } == [e, e, 2, 4, 8])
        // 8 already rests on the bottom; 4 and 2 fall.
        #expect(moves.count == 2)
        #expect(!moves.contains { $0.tileID == 3 })
    }

    @Test func settledBoardProducesNoMoves() {
        let board = makeBoard([
            [e, e, e, e, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
            [2, e, e, e, e],
            [4, 8, e, e, e],
        ])
        let (fallen, moves) = MoveResolver.fall(board, gravity: .down)
        #expect(fallen == board)
        #expect(moves.isEmpty)
    }

    @Test func fallNeverMerges() {
        let board = makeBoard([
            [2, e, e, e, e],
            [e, e, e, e, e],
            [2, e, e, e, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
        ])
        let (fallen, _) = MoveResolver.fall(board, gravity: .down)
        #expect(values(of: fallen).map { $0[0] } == [e, e, e, 2, 2])
    }

    @Test func fallWorksInAllDirections() {
        let board = makeBoard([
            [e, e, e, e, e],
            [e, e, 2, e, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
        ])
        for (gravity, expected) in [
            (Direction.up, Coordinate(row: 0, col: 2)),
            (.down, Coordinate(row: 4, col: 2)),
            (.left, Coordinate(row: 1, col: 0)),
            (.right, Coordinate(row: 1, col: 4)),
        ] {
            let (fallen, _) = MoveResolver.fall(board, gravity: gravity)
            #expect(fallen[expected]?.value == 2)
        }
    }
}

@Suite struct CascadeTests {
    @Test func equalStackedPairMerges() {
        let board = makeBoard([
            [e, e, e, e, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
            [2, e, e, e, e],
            [2, e, e, e, e],
        ])
        var nextID = 100
        let result = MoveResolver.cascadeRound(board, gravity: .down, round: 1, nextTileID: &nextID)
        let (merged, merges) = try! #require(result)
        #expect(values(of: merged).map { $0[0] } == [e, e, e, e, 4])
        #expect(merges.count == 1)
        #expect(merges[0].at == Coordinate(row: 4, col: 0))
        #expect(merges[0].points == 4)
        #expect(merges[0].multiplier == 1)
    }

    @Test func tripleStackMergesGravityNearestPairOnly() {
        let board = makeBoard([
            [e, e, e, e, e],
            [e, e, e, e, e],
            [2, e, e, e, e],
            [2, e, e, e, e],
            [2, e, e, e, e],
        ])
        var nextID = 100
        let (merged, merges) = try! #require(
            MoveResolver.cascadeRound(board, gravity: .down, round: 1, nextTileID: &nextID)
        )
        // Bottom pair merges; the top 2 stays (still floating until next fall).
        #expect(values(of: merged).map { $0[0] } == [e, e, 2, e, 4])
        #expect(merges.count == 1)
        #expect(merges[0].at == Coordinate(row: 4, col: 0))
    }

    @Test func stableBoardReturnsNil() {
        let board = makeBoard([
            [e, e, e, e, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
            [2, e, e, e, e],
            [4, e, e, e, e],
        ])
        var nextID = 100
        #expect(MoveResolver.cascadeRound(board, gravity: .down, round: 1, nextTileID: &nextID) == nil)
    }

    @Test func multiplierScalesPoints() {
        let board = makeBoard([
            [e, e, e, e, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
            [4, e, e, e, e],
            [4, e, e, e, e],
        ])
        var nextID = 100
        let (_, merges) = try! #require(
            MoveResolver.cascadeRound(board, gravity: .down, round: 3, nextTileID: &nextID)
        )
        #expect(merges[0].points == 8 * 3)
        #expect(merges[0].multiplier == 3)
    }

    @Test func chainAcrossRoundsReachesFixedPoint() {
        // Column bottom-to-top: 4, 2, 2. Round 1 merges the 2s into a 4 that
        // falls onto the existing 4; round 2 merges those into 8.
        let board = makeBoard([
            [e, e, e, e, e],
            [e, e, e, e, e],
            [2, e, e, e, e],
            [2, e, e, e, e],
            [4, e, e, e, e],
        ])
        var nextID = 100
        var current = board
        var round = 1
        var totalPoints = 0
        while let (merged, merges) = MoveResolver.cascadeRound(
            current, gravity: .down, round: round, nextTileID: &nextID
        ) {
            totalPoints += merges.reduce(0) { $0 + $1.points }
            (current, _) = MoveResolver.fall(merged, gravity: .down)
            round += 1
        }
        #expect(values(of: current).map { $0[0] } == [e, e, e, e, 8])
        // Round 1: 2+2 → 4 (×1 = 4 points). Round 2: 4+4 → 8 (×2 = 16 points).
        #expect(totalPoints == 4 + 16)
        #expect(round == 3)
    }
}
