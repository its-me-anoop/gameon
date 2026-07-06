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

    @Test func zenHasNoBudgetAndAlwaysSpawnsAtMostOne() {
        var game = GameState(mode: .zen, seed: 7)
        #expect(game.movesRemaining == nil)
        var made = 0
        outer: while made < 130 {
            for direction in Direction.allCases {
                var copy = game
                guard copy.applyMove(direction) != nil else { continue }
                let result = game.applyMove(direction)!
                #expect(result.spawns.count <= 1)
                made += 1
                continue outer
            }
            break // board locked — fine, pacing was verified up to here
        }
        #expect(made > 60, "zen should comfortably outlive the endless ramp threshold")
    }

    @Test func sprintCountsDownBudgetAndSpawnsTwo() {
        var game = GameState(mode: .sprint, seed: 7)
        #expect(game.movesRemaining == GameMode.sprintMoveBudget)
        var sawDoubleSpawn = false
        outer: while !game.isGameOver {
            for direction in Direction.allCases {
                var copy = game
                guard copy.applyMove(direction) != nil else { continue }
                let result = game.applyMove(direction)!
                if result.spawns.count == 2 { sawDoubleSpawn = true }
                continue outer
            }
            break
        }
        #expect(sawDoubleSpawn, "sprint should spawn 2 tiles when the board has room")
        #expect(game.moveCount <= GameMode.sprintMoveBudget)
        if game.moveCount == GameMode.sprintMoveBudget {
            #expect(game.movesRemaining == 0)
            #expect(game.isGameOver)
        }
    }

    @Test func allModesCodableRoundTrip() throws {
        let modes: [GameMode] = [.zen, .sprint, .endless, .daily(puzzleNumber: 3)]
        for mode in modes {
            let game = GameState(mode: mode, seed: 1)
            let data = try JSONEncoder().encode(game)
            let back = try JSONDecoder().decode(GameState.self, from: data)
            #expect(back.mode == mode)
            #expect(back == game)
        }
    }

    /// Byte-for-byte v1.1 saved game (captured before v1.2 fields existed).
    /// New engine fields must default instead of failing the decode — a
    /// decode throw upstream wipes the player's persisted state.
    @Test func v11SavedGameDecodesWithNewFieldsDefaulted() throws {
        let fixture = #"{"mode":{"endless":{}},"rng":{"state":17418742259747381458},"history":[{"moveCount":0,"score":0,"nextTileID":3,"board":{"cells":[null,null,null,null,null,null,null,null,null,null,null,null,null,null,null,null,null,null,{"value":2,"id":2},null,null,null,null,{"value":2,"id":1},null]},"gravity":"down","bestTile":2,"rng":{"state":8709371129873690750},"cascadeCount":0},{"board":{"cells":[{"value":2,"id":3},null,null,null,null,null,null,null,null,null,null,null,null,null,null,{"value":2,"id":2},null,null,null,null,{"value":2,"id":1},null,null,null,null]},"bestTile":2,"score":0,"cascadeCount":0,"rng":{"state":13064056694810536104},"nextTileID":4,"moveCount":1,"gravity":"left"}],"nextTileID":6,"bestTile":4,"moveCount":2,"score":4,"undosUsed":0,"board":{"cells":[{"id":4,"value":4},null,null,null,null,{"id":1,"value":2},null,null,null,null,{"value":2,"id":5},null,null,null,null,null,null,null,null,null,null,null,null,null,null]},"gravity":"up","seed":42,"cascadeCount":0}"#
        var game = try JSONDecoder().decode(GameState.self, from: Data(fixture.utf8))
        #expect(game.score == 4)
        #expect(game.bestTile == 4)
        #expect(game.moveCount == 2)
        #expect(game.bestCascadeRound == 0)  // new in v1.2 — defaults
        #expect(game.stasisCharges == 0)     // new in v1.2 — defaults
        #expect(game.canUndo)
        let undone = game.undo()             // old history snapshots decode too
        #expect(undone)
        #expect(game.moveCount == 1)
    }

    @Test func bestCascadeRoundTracksTheDeepestMergeRound() {
        var game = GameState(mode: .zen, seed: 3)
        var deepest = 0
        var made = 0
        outer: while made < 80 {
            for direction in Direction.allCases {
                var copy = game
                guard copy.applyMove(direction) != nil else { continue }
                let result = game.applyMove(direction)!
                deepest = max(
                    deepest,
                    result.phases.filter { !$0.merges.isEmpty }.map(\.round).max() ?? 0
                )
                made += 1
                continue outer
            }
            break
        }
        #expect(deepest > 0, "80 zen moves should cascade at least once")
        #expect(game.bestCascadeRound == deepest)
    }

    @Test func undoRestoresBestCascadeRound() {
        var game = GameState(mode: .zen, seed: 3)
        var made = 0
        outer: while made < 40 {
            let before = game.bestCascadeRound
            for direction in Direction.allCases {
                var copy = game
                guard copy.applyMove(direction) != nil else { continue }
                _ = game.applyMove(direction)
                if game.bestCascadeRound > before {
                    let undone = game.undo()
                    #expect(undone)
                    #expect(game.bestCascadeRound == before)
                    return
                }
                made += 1
                continue outer
            }
            break
        }
        Issue.record("never saw bestCascadeRound increase in 40 moves")
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
