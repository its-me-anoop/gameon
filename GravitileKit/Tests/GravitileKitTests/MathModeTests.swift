import Testing
import Foundation
@testable import GravitileKit

@Suite struct MathModeTests {
    // MARK: - Progression curriculum

    @Test func targetsClimbThenLoopBelowTheSingleDigitCeiling() {
        #expect(MathProgression.target(forStage: 0) == 5)
        #expect(MathProgression.target(forStage: 1) == 10)
        #expect(MathProgression.target(forStage: 2) == 12)
        #expect(MathProgression.target(forStage: 3) == 14)
        #expect(MathProgression.target(forStage: 4) == 16)
        #expect(MathProgression.target(forStage: 5) == 10)
        #expect(MathProgression.target(forStage: 8) == 16)
        #expect(MathProgression.target(forStage: 9) == 10)
    }

    @Test func spawnRangeAlwaysContainsItsOwnComplements() {
        #expect(MathProgression.spawnRange(for: 5) == 1...4)
        #expect(MathProgression.spawnRange(for: 10) == 1...9)
        #expect(MathProgression.spawnRange(for: 16) == 7...9)
        for target in [5, 10, 12, 14, 16] {
            let range = MathProgression.spawnRange(for: target)
            for value in range {
                #expect(range.contains(target - value), "complement of \(value) for \(target)")
            }
        }
    }

    // MARK: - Bond merges and clears

    @Test func pairSummingToTargetMergesAndPopsOffTheBoard() {
        var board = Board()
        board[Coordinate(row: 4, col: 1)] = Tile(id: 900, value: 2)
        board[Coordinate(row: 4, col: 2)] = Tile(id: 901, value: 3)
        var game = GameState(testBoard: board, gravity: .down, mode: .math, seed: 1)

        let result = game.applyMove(.left)!
        #expect(result.slide.merges.count == 1)
        #expect(result.slide.merges.first?.resultTile.value == 5)
        #expect(result.slide.clears.count == 1)
        #expect(result.slide.clears.first?.value == 5)
        #expect(Set(result.slide.clears.first?.addends ?? []) == [2, 3])
        // The bond popped: no resting tile carries the target value.
        #expect(game.board.tiles.allSatisfy { $0.1.value != 5 })
        #expect(game.bondsThisStage == 1)
        #expect(game.bondsCleared == 1)
        // Bond scores its face value, like a doubling merge scores its result.
        #expect(game.score == 5)
    }

    @Test func equalPairThatDoesNotReachTargetStaysApart() {
        var board = Board()
        board[Coordinate(row: 4, col: 1)] = Tile(id: 900, value: 2)
        board[Coordinate(row: 4, col: 2)] = Tile(id: 901, value: 2)
        var game = GameState(testBoard: board, gravity: .down, mode: .math, seed: 1)

        let result = game.applyMove(.left)!
        #expect(result.slide.merges.isEmpty)
        #expect(result.slide.clears.isEmpty)
        #expect(game.board.tiles.contains { $0.1.id == 900 })
        #expect(game.board.tiles.contains { $0.1.id == 901 })
    }

    @Test func equalPairSummingToTargetBonds() {
        var board = Board()
        board[Coordinate(row: 4, col: 1)] = Tile(id: 900, value: 5)
        board[Coordinate(row: 4, col: 2)] = Tile(id: 901, value: 5)
        var game = GameState(testBoard: board, gravity: .down, mode: .math, seed: 1)
        game.setTestMathStage(1) // target 10

        let result = game.applyMove(.left)!
        #expect(result.slide.clears.first?.value == 10)
        #expect(result.slide.clears.first?.addends == [5, 5])
    }

    @Test func cascadeBondsClearAndScoreWithRoundMultiplier() {
        // Row [4,3,2,1], swipe left (gravity .down → .left): the slide bonds
        // 3+2 and pops it; the 1 then falls left onto the 4, and 4+1 = 5
        // bonds in cascade round 1.
        var board = Board()
        board[Coordinate(row: 4, col: 0)] = Tile(id: 900, value: 4)
        board[Coordinate(row: 4, col: 1)] = Tile(id: 901, value: 3)
        board[Coordinate(row: 4, col: 2)] = Tile(id: 902, value: 2)
        board[Coordinate(row: 4, col: 3)] = Tile(id: 903, value: 1)
        var game = GameState(testBoard: board, gravity: .down, mode: .math, seed: 1)

        let result = game.applyMove(.left)!
        let cascadeClears = result.phases.flatMap(\.clears)
        #expect(!cascadeClears.isEmpty)
        #expect(cascadeClears.allSatisfy { $0.value == 5 })
        #expect(game.bondsCleared >= 2)
    }

    // MARK: - Mode policy

    @Test func mathModePlaysLikeZenWithDoubleSpawns() {
        let game = GameState(mode: .math, seed: 7)
        #expect(game.movesRemaining == nil)
        #expect(game.canUseStasis == false)
        #expect(game.boulderIceForNextSpawn == 0)
        #expect(game.spawnCountForNextMove == 2)
        #expect(game.mathTarget == 5)
    }

    @Test func newMathGameStartsWithStarterTilesInStageRange() {
        let game = GameState(mode: .math, seed: 7)
        #expect(game.board.tiles.count == MathProgression.starterCount)
        #expect(game.board.tiles.allSatisfy { (1...4).contains($0.1.value) })
    }

    @Test func spawnsStayInsideTheStageRange() {
        var game = GameState(mode: .math, seed: 11)
        var made = 0
        outer: while made < 30 {
            for direction in Direction.allCases {
                var copy = game
                guard copy.applyMove(direction) != nil else { continue }
                let result = game.applyMove(direction)!
                let range = MathProgression.spawnRange(for: game.mathTarget!)
                for spawn in result.spawns where result.stageAdvance == nil {
                    #expect(range.contains(spawn.tile.value))
                    #expect(spawn.tile.ice == 0)
                }
                made += 1
                continue outer
            }
            break
        }
        #expect(made == 30)
    }

    // MARK: - Stage advance

    @Test func sixthBondSweepsBoardAdvancesTargetAndPaysBonus() {
        var board = Board()
        board[Coordinate(row: 4, col: 1)] = Tile(id: 900, value: 2)
        board[Coordinate(row: 4, col: 2)] = Tile(id: 901, value: 3)
        board[Coordinate(row: 4, col: 4)] = Tile(id: 902, value: 4)
        var game = GameState(testBoard: board, gravity: .down, mode: .math, seed: 1)
        game.setTestMathBonds(thisStage: MathProgression.bondsPerStage - 1)

        let scoreBefore = game.score
        let result = game.applyMove(.left)!
        let advance = result.stageAdvance
        #expect(advance != nil)
        #expect(advance?.newStage == 1)
        #expect(advance?.newTarget == 10)
        #expect(advance?.bonusPoints == 50)
        #expect(game.mathStage == 1)
        #expect(game.bondsThisStage == 0)
        // Swept clean, then reseeded with starters for the new target.
        #expect(game.board.tiles.count == MathProgression.starterCount)
        #expect(advance?.starterSpawns.count == MathProgression.starterCount)
        #expect(game.board.tiles.allSatisfy { (1...9).contains($0.1.value) })
        #expect(game.score == scoreBefore + 5 + 50)
    }

    // MARK: - Undo

    @Test func undoRestoresStageAndBondCounters() {
        var board = Board()
        board[Coordinate(row: 4, col: 1)] = Tile(id: 900, value: 2)
        board[Coordinate(row: 4, col: 2)] = Tile(id: 901, value: 3)
        var game = GameState(testBoard: board, gravity: .down, mode: .math, seed: 1)
        game.setTestMathBonds(thisStage: MathProgression.bondsPerStage - 1)

        game.applyMove(.left)
        #expect(game.mathStage == 1)
        let undone = game.undo()
        #expect(undone)
        #expect(game.mathStage == 0)
        #expect(game.bondsThisStage == MathProgression.bondsPerStage - 1)
        #expect(game.bondsCleared == 0)
        #expect(game.mathTarget == 5)
    }

    // MARK: - Game over under the sum rule

    @Test func fullBoardOfEqualNonBondingTilesIsLocked() {
        var board = Board()
        for row in 0..<Board.size {
            for col in 0..<Board.size {
                board[Coordinate(row: row, col: col)] = Tile(id: row * 5 + col + 1, value: 2)
            }
        }
        let game = GameState(testBoard: board, gravity: .down, mode: .math, seed: 1)
        // Doubling would merge everywhere; 2+2 ≠ 5 must read as no legal move.
        #expect(game.hasLegalMove == false)
        #expect(game.isGameOver)
    }

    // MARK: - Persistence

    @Test func mathGameRoundTripsThroughCodable() throws {
        var game = GameState(mode: .math, seed: 3)
        for direction in Direction.allCases {
            var copy = game
            if copy.applyMove(direction) != nil {
                game.applyMove(direction)
                break
            }
        }
        let data = try JSONEncoder().encode(game)
        let back = try JSONDecoder().decode(GameState.self, from: data)
        #expect(back == game)
        #expect(back.mathTarget == game.mathTarget)
    }

    @Test func v12GameJSONWithoutMathKeysDecodes() throws {
        let game = GameState(mode: .endless, seed: 5)
        var json = try JSONSerialization.jsonObject(
            with: JSONEncoder().encode(game)
        ) as! [String: Any]
        json.removeValue(forKey: "mathStage")
        json.removeValue(forKey: "bondsThisStage")
        json.removeValue(forKey: "bondsCleared")
        let data = try JSONSerialization.data(withJSONObject: json)
        let back = try JSONDecoder().decode(GameState.self, from: data)
        #expect(back.mathStage == 0)
        #expect(back.bondsCleared == 0)
    }

    // MARK: - Share card

    @Test func shareTextNamesTheMathMode() {
        let text = ShareCard.text(mode: .math, score: 120, bestTile: 9, cascadeCount: 3)
        #expect(text.contains("Math Pop"))
        #expect(text.contains("120"))
    }
}
