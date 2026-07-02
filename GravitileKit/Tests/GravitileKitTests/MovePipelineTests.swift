import Testing
@testable import GravitileKit

@Suite struct MovePipelineTests {
    @Test func illegalSwipeReturnsNilAndLeavesInputsUntouched() {
        let board = makeBoard([
            [2, 4, 8, 16, 32],
            [e, e, e, e, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
            [e, e, e, e, e],
        ])
        var rng = SplitMix64(seed: 1)
        let rngBefore = rng
        var nextID = 100
        let result = MoveResolver.resolveMove(
            board: board, swipe: .left, gravity: .down, rng: &rng, nextTileID: &nextID
        )
        #expect(result == nil)
        #expect(rng == rngBefore)
        #expect(nextID == 100)
    }

    @Test func legalSwipeRotatesGravityAndSettlesBoard() {
        let board = makeBoard([
            [e, e, e, e, 2],
            [e, e, e, e, e],
            [e, e, e, e, e],
            [e, e, 4, e, e],
            [e, e, e, e, e],
        ])
        var rng = SplitMix64(seed: 1)
        var nextID = 100
        let result = MoveResolver.resolveMove(
            board: board, swipe: .left, gravity: .down, rng: &rng, nextTileID: &nextID
        )!
        #expect(result.newGravity == .left)
        #expect(result.phases.first?.round == 0)
        // Every tile (including the spawn) must rest against the left edge:
        // a tile may only sit in column c+1 if column c of its row is occupied.
        let finalBoard = result.finalBoard
        for (coordinate, _) in finalBoard.tiles where coordinate.col > 0 {
            #expect(finalBoard[Coordinate(row: coordinate.row, col: coordinate.col - 1)] != nil)
        }
        #expect(result.spawn != nil)
        #expect(finalBoard.tiles.count == 3)
    }

    @Test func swipePlusRotationTriggersCascade() {
        // Swiping down stacks nothing, but after gravity rotates to .left the
        // two 4s in row 4 collide horizontally… construct explicitly:
        // gravity .down, swipe .left on rows [4,4] in row 4 merges in slide;
        // instead use vertical alignment created by the post-rotation fall.
        let board = makeBoard([
            [e, e, e, e, e],
            [e, e, e, e, e],
            [4, e, e, e, e],
            [2, e, e, e, e],
            [2, 4, e, e, e],
        ])
        var rng = SplitMix64(seed: 9)
        var nextID = 100
        // Swipe .down: col 0 merges 2+2 → 4 stacked over nothing else changes…
        // col 0 becomes [., ., 4, 4] bottom two cells? Verify via events:
        let result = MoveResolver.resolveMove(
            board: board, swipe: .down, gravity: .down, rng: &rng, nextTileID: &nextID
        )!
        // Slide merged the 2s.
        #expect(result.slide.merges.count == 1)
        // After rotation to .left everything falls left; the two 4s in col 0
        // (from the merge) and the slid 4 meet. At least one cascade round.
        let cascadeMerges = result.phases.flatMap(\.merges)
        #expect(!cascadeMerges.isEmpty)
        #expect(cascadeMerges.allSatisfy { $0.multiplier >= 1 })
        // Score = slide points + cascade points.
        let expected = result.slide.merges.reduce(0) { $0 + $1.points }
            + cascadeMerges.reduce(0) { $0 + $1.points }
        #expect(result.scoreDelta == expected)
    }

    @Test func spawnRestsedAgainstGravityAndNeverMerges() {
        var rng = SplitMix64(seed: 3)
        var nextID = 1
        var board = Board()
        board[Coordinate(row: 0, col: 0)] = Tile(id: 900, value: 2)
        // Spawn 20 tiles under .down gravity onto a mostly empty board.
        for _ in 0..<20 {
            guard let (newBoard, event) = MoveResolver.spawn(
                on: board, gravity: .down, rng: &rng, nextTileID: &nextID
            ) else {
                Issue.record("Board unexpectedly full")
                return
            }
            // Entered at the top edge of its column, rested at the lowest empty cell.
            #expect(event.enteredAt.row == 0)
            #expect(event.enteredAt.col == event.restedAt.col)
            let below = event.restedAt.offset(by: .down)
            if Board.contains(below) {
                #expect(newBoard[below] != nil)
            } else {
                #expect(event.restedAt.row == Board.size - 1)
            }
            #expect([2, 4].contains(event.tile.value))
            board = newBoard
        }
        #expect(board.tiles.count == 21)
    }

    @Test func spawnValueDistributionIsRoughlyNinetyTen() {
        var rng = SplitMix64(seed: 12345)
        var fours = 0
        let trials = 2000
        for _ in 0..<trials {
            var nextID = 1
            let empty = Board()
            let (_, event) = MoveResolver.spawn(
                on: empty, gravity: .down, rng: &rng, nextTileID: &nextID
            )!
            if event.tile.value == 4 { fours += 1 }
        }
        let rate = Double(fours) / Double(trials)
        #expect(rate > 0.05 && rate < 0.15, "4-spawn rate was \(rate)")
    }

    @Test func spawnSkippedOnFullBoard() {
        var full = Board()
        var id = 0
        for row in 0..<Board.size {
            for col in 0..<Board.size {
                id += 1
                // Checkerboard of alternating values so nothing merges.
                full[Coordinate(row: row, col: col)] = Tile(id: id, value: (row + col).isMultiple(of: 2) ? 2 : 4)
            }
        }
        var rng = SplitMix64(seed: 1)
        var nextID = 100
        #expect(MoveResolver.spawn(on: full, gravity: .down, rng: &rng, nextTileID: &nextID) == nil)
    }

    @Test func identicalSeedsReplayIdentically() {
        func play(seed: UInt64) -> (Board, Int, Direction) {
            var board = makeBoard([
                [e, e, e, e, e],
                [e, e, e, e, e],
                [e, e, e, e, e],
                [2, e, e, e, e],
                [2, 4, e, e, e],
            ])
            var rng = SplitMix64(seed: seed)
            var nextID = 100
            var gravity = Direction.down
            var score = 0
            let script: [Direction] = [.down, .left, .up, .left, .down, .right, .up, .right, .down, .left]
            for swipe in script {
                if let result = MoveResolver.resolveMove(
                    board: board, swipe: swipe, gravity: gravity, rng: &rng, nextTileID: &nextID
                ) {
                    board = result.finalBoard
                    gravity = result.newGravity
                    score += result.scoreDelta
                }
            }
            return (board, score, gravity)
        }
        let a = play(seed: 42)
        let b = play(seed: 42)
        #expect(a.0 == b.0)
        #expect(a.1 == b.1)
        #expect(a.2 == b.2)
        let c = play(seed: 43)
        #expect(a.0 != c.0 || a.1 != c.1)
    }
}
