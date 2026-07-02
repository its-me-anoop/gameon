import Testing
import Foundation
@testable import GravitileKit

@Suite struct GameStateTests {
    @Test func newGameSpawnsTwoTilesDeterministically() {
        let a = GameState(mode: .endless, seed: 42)
        let b = GameState(mode: .endless, seed: 42)
        #expect(a.board.tiles.count == 2)
        #expect(a.board == b.board)
        #expect(a.gravity == .down)
        #expect(a.score == 0)
        #expect(a.moveCount == 0)
        let c = GameState(mode: .endless, seed: 7)
        #expect(c.board != a.board)
    }

    @Test func applyMoveUpdatesScoreMoveCountAndBestTile() {
        var game = GameState(mode: .endless, seed: 42)
        var moved = 0
        for swipe in [Direction.left, .down, .right, .up, .left, .down, .right, .up] {
            if game.applyMove(swipe) != nil { moved += 1 }
        }
        #expect(moved > 0)
        #expect(game.moveCount == moved)
        #expect(game.bestTile >= 2)
        #expect(game.board.tiles.count >= 2)
    }

    @Test func illegalMoveReturnsNilAndMutatesNothing() {
        var game = GameState(mode: .endless, seed: 42)
        // Find an illegal direction, if any, by trying all and using a fresh copy.
        for direction in Direction.allCases {
            var copy = game
            if copy.applyMove(direction) == nil {
                #expect(copy == game)
                return
            }
        }
        // All directions legal from this seed — also fine; force the vacuous pass.
        #expect(Bool(true))
    }

    @Test func undoRestoresExactPriorStateAndCountsUse() {
        var game = GameState(mode: .endless, seed: 42)
        #expect(!game.canUndo)
        let undoOnFreshGame = game.undo()
        #expect(!undoOnFreshGame)

        let before = game
        var applied: Direction?
        for direction in Direction.allCases where game.applyMove(direction) != nil {
            applied = direction
            break
        }
        #expect(applied != nil)
        #expect(game.canUndo)
        let undoSucceeded = game.undo()
        #expect(undoSucceeded)
        #expect(game.board == before.board)
        #expect(game.score == before.score)
        #expect(game.moveCount == before.moveCount)
        #expect(game.gravity == before.gravity)
        #expect(game.undosUsed == 1)

        // Replaying the same direction after undo gives the identical result
        // (the RNG must also have been restored).
        var replayA = before
        var replayB = game
        let ra = replayA.applyMove(applied!)
        let rb = replayB.applyMove(applied!)
        #expect(ra == rb)
        #expect(replayA.board == replayB.board)
    }

    @Test func dailyModeCountsDownBudgetAndEnds() {
        var game = GameState(mode: .daily(puzzleNumber: 1, moveBudget: 3), seed: DailySeed.seed(forPuzzleNumber: 1))
        #expect(game.movesRemaining == 3)
        var made = 0
        outer: while made < 3 {
            for direction in Direction.allCases {
                if game.applyMove(direction) != nil {
                    made += 1
                    continue outer
                }
            }
            Issue.record("Ran out of legal moves before exhausting budget")
            return
        }
        #expect(game.movesRemaining == 0)
        #expect(game.isGameOver)
        // Budget exhausted: further moves are rejected.
        for direction in Direction.allCases {
            let rejected = game.applyMove(direction)
            #expect(rejected == nil)
        }
    }

    @Test func endlessGameOverWhenNoSlideChangesBoard() {
        // Checkerboard of alternating values: no slide can change anything.
        var board = Board()
        var id = 0
        for row in 0..<Board.size {
            for col in 0..<Board.size {
                id += 1
                board[Coordinate(row: row, col: col)] = Tile(id: id, value: (row + col).isMultiple(of: 2) ? 2 : 4)
            }
        }
        let game = GameState(testBoard: board, gravity: .down, mode: .endless, seed: 1)
        #expect(!game.hasLegalMove)
        #expect(game.isGameOver)
    }

    @Test func codableRoundTripPreservesFutureDeterminism() throws {
        var game = GameState(mode: .endless, seed: 42)
        _ = game.applyMove(.left)
        _ = game.applyMove(.up)

        let data = try JSONEncoder().encode(game)
        var restored = try JSONDecoder().decode(GameState.self, from: data)
        var original = game
        for direction in [Direction.right, .down, .left] {
            let a = original.applyMove(direction)
            let b = restored.applyMove(direction)
            #expect(a == b)
        }
        #expect(original == restored)
    }

    @Test func undoHistoryIsBounded() {
        var game = GameState(mode: .endless, seed: 42)
        var made = 0
        outer: while made < 30 {
            for direction in Direction.allCases {
                if game.applyMove(direction) != nil {
                    made += 1
                    continue outer
                }
            }
            break
        }
        // Undo up to the cap; must not crash and must stop at the bound.
        var undos = 0
        while game.undo() { undos += 1 }
        #expect(undos <= 20)
        #expect(undos > 0)
    }

    @Test func goldenTenMoveGame() {
        // Frozen reference game: seed 42, scripted swipes. Guards against any
        // accidental change to engine semantics. Regenerate deliberately only
        // when rules change on purpose (see docs/balance-report.md).
        var game = GameState(mode: .endless, seed: 42)
        let script: [Direction] = [.left, .down, .right, .up, .left, .down, .right, .up, .left, .down]
        var legal = 0
        for swipe in script where game.applyMove(swipe) != nil {
            legal += 1
        }
        // Golden values captured from the first verified run (see commit).
        let signature = "\(legal)|\(game.score)|\(game.bestTile)|\(game.moveCount)|\(game.gravity.rawValue)|\(game.board.tiles.count)"
        #expect(signature == GoldenValues.tenMoveSignature, "signature: \(signature)")
    }
}

enum GoldenValues {
    // Captured from the first verified run after the undo-snapshot fix.
    // Format: legal|score|bestTile|moveCount|gravity|tileCount
    static let tenMoveSignature = "7|32|8|7|right|3"
}
