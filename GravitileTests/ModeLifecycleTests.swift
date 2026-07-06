import Testing
import Foundation
import GravitileKit
@testable import Gravitile

@MainActor
@Suite struct ModeLifecycleTests {
    private func freshModel() -> AppModel {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("gravitile-mode-test-\(UUID().uuidString).json")
        return AppModel(persistence: PersistenceService(fileURL: url))
    }

    private func playOneMove(_ game: inout GameState) {
        for direction in Direction.allCases where game.applyMove(direction) != nil { return }
        Issue.record("no legal move from fresh board")
    }

    @Test func zenLifecycleResumesCheckpointsAndRecordsBestTile() {
        let model = freshModel()
        var game = model.zenGame()
        #expect(game.mode == .zen)

        playOneMove(&game)
        model.checkpoint(game)
        #expect(model.zenGame().moveCount == game.moveCount)

        model.recordGameEnd(game)
        #expect(model.persisted.bestZenTile == game.bestTile)
        #expect(model.persisted.zenGame == nil)
        #expect(model.persisted.stats.gamesPlayed == 1)
    }

    @Test func sprintLifecycleResumesCheckpointsAndRecordsBestScore() {
        let model = freshModel()
        var game = model.sprintGame()
        #expect(game.mode == .sprint)
        #expect(game.movesRemaining == GameMode.sprintMoveBudget)

        playOneMove(&game)
        model.checkpoint(game)
        #expect(model.sprintGame().moveCount == game.moveCount)

        model.recordGameEnd(game)
        #expect(model.persisted.bestSprintScore == game.score)
        #expect(model.persisted.sprintGame == nil)
    }

    @Test func newGameReplacesFinishedSlot() {
        let model = freshModel()
        let first = model.newSprintGame()
        let second = model.newSprintGame()
        #expect(first.seed != second.seed || first.board != second.board)
        #expect(model.persisted.sprintGame == second)
    }
}
