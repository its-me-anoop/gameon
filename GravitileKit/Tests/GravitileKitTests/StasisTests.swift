import Testing
import Foundation
@testable import GravitileKit

@Suite struct StasisTests {
    /// Two equal tiles side by side on the floor: swiping left merges them.
    private func boardWithAdjacentPair(value: Int) -> Board {
        var board = Board()
        board[Coordinate(row: 4, col: 1)] = Tile(id: 900, value: value)
        board[Coordinate(row: 4, col: 2)] = Tile(id: 901, value: value)
        return board
    }

    @Test func stasisHoldsGravityForExactlyOneMove() {
        var game = GameState(mode: .zen, seed: 7)
        let before = game.gravity
        var applied = false
        for direction in Direction.allCases {
            var copy = game
            guard copy.applyMove(direction, stasis: true) != nil else { continue }
            let result = game.applyMove(direction, stasis: true)!
            #expect(result.heldGravity)
            #expect(result.newGravity == before)
            #expect(game.gravity == before)
            applied = true
            break
        }
        #expect(applied)

        // The hold is one-shot: the following move rotates as usual.
        for direction in Direction.allCases {
            var copy = game
            guard copy.applyMove(direction) != nil else { continue }
            let result = game.applyMove(direction)!
            #expect(!result.heldGravity)
            #expect(game.gravity == before.rotatedClockwise)
            return
        }
        Issue.record("no legal follow-up move")
    }

    @Test func endlessEarnsAChargeWhenCrossingAMilestone() {
        var game = GameState(
            testBoard: boardWithAdjacentPair(value: 128),
            gravity: .down, mode: .endless, seed: 1
        )
        #expect(game.stasisCharges == 0)
        #expect(!game.canUseStasis)

        game.applyMove(.left)  // 128+128 → 256 crosses the first milestone
        #expect(game.bestTile >= 256)
        #expect(game.stasisCharges == 1)
        #expect(game.canUseStasis)
    }

    @Test func bankIsCappedAtTwoCharges() {
        var game = GameState(mode: .endless, seed: 1)
        game.bankStasisCharge()
        game.bankStasisCharge()
        game.bankStasisCharge()
        #expect(game.stasisCharges == 2)
    }

    @Test func zenUsesStasisWithoutCharges() {
        var game = GameState(mode: .zen, seed: 7)
        #expect(game.stasisCharges == 0)
        #expect(game.canUseStasis)
        for direction in Direction.allCases {
            var copy = game
            guard copy.applyMove(direction, stasis: true) != nil else { continue }
            let result = game.applyMove(direction, stasis: true)
            #expect(result?.heldGravity == true)
            #expect(game.stasisCharges == 0)
            return
        }
        Issue.record("no legal move")
    }

    @Test func endlessConsumesAChargeOnUse() {
        var game = GameState(
            testBoard: boardWithAdjacentPair(value: 128),
            gravity: .down, mode: .endless, seed: 1
        )
        game.applyMove(.left)
        #expect(game.stasisCharges == 1)
        for direction in Direction.allCases {
            var copy = game
            guard copy.applyMove(direction, stasis: true) != nil else { continue }
            _ = game.applyMove(direction, stasis: true)
            #expect(game.stasisCharges == 0)
            #expect(!game.canUseStasis)
            return
        }
        Issue.record("no legal move")
    }

    @Test func endlessWithoutChargesRefusesStasis() {
        var game = GameState(mode: .endless, seed: 7)
        #expect(game.stasisCharges == 0)
        let before = game
        for direction in Direction.allCases {
            let result = game.applyMove(direction, stasis: true)
            #expect(result == nil)
        }
        #expect(game == before)
    }

    @Test func dailyAndSprintRefuseStasis() {
        for mode in [GameMode.daily(puzzleNumber: 3), .sprint] {
            var game = GameState(mode: mode, seed: 7)
            game.bankStasisCharge()
            #expect(!game.canUseStasis, "\(mode) must never allow stasis")
            let before = game
            for direction in Direction.allCases {
                let result = game.applyMove(direction, stasis: true)
                #expect(result == nil)
            }
            #expect(game == before, "a refused stasis move must not mutate anything")
        }
    }

    @Test func undoRestoresChargesAndHeldGravity() {
        var game = GameState(
            testBoard: boardWithAdjacentPair(value: 128),
            gravity: .down, mode: .endless, seed: 1
        )
        game.applyMove(.left)
        let charges = game.stasisCharges
        let gravity = game.gravity
        #expect(charges == 1)

        for direction in Direction.allCases {
            var copy = game
            guard copy.applyMove(direction, stasis: true) != nil else { continue }
            _ = game.applyMove(direction, stasis: true)
            #expect(game.stasisCharges == 0)
            let undone = game.undo()
            #expect(undone)
            #expect(game.stasisCharges == charges)
            #expect(game.gravity == gravity)
            return
        }
        Issue.record("no legal move")
    }

    @Test func stasisRoundTripsThroughCodable() throws {
        var game = GameState(
            testBoard: boardWithAdjacentPair(value: 128),
            gravity: .down, mode: .endless, seed: 1
        )
        game.applyMove(.left)
        let data = try JSONEncoder().encode(game)
        let back = try JSONDecoder().decode(GameState.self, from: data)
        #expect(back.stasisCharges == game.stasisCharges)
        #expect(back == game)
    }
}
