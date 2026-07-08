import Testing
import Foundation
import GravitileKit
@testable import Gravitile

@MainActor
@Suite struct MathModeAppTests {
    private func freshModel() -> AppModel {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("gravitile-math-test-\(UUID().uuidString).json")
        return AppModel(persistence: PersistenceService(fileURL: url))
    }

    private func playOneMove(_ game: inout GameState) {
        for direction in Direction.allCases where game.applyMove(direction) != nil { return }
        Issue.record("no legal move from fresh board")
    }

    @Test func mathLifecycleResumesCheckpointsAndRecordsBestScore() {
        let model = freshModel()
        var game = model.mathGame()
        #expect(game.mode == .math)
        #expect(game.mathTarget == 5)

        playOneMove(&game)
        model.checkpoint(game)
        #expect(model.mathGame().moveCount == game.moveCount)

        model.recordGameEnd(game)
        #expect(model.persisted.bestMathScore == game.score)
        #expect(model.persisted.mathGame == nil)
        #expect(model.persisted.stats.gamesPlayed == 1)
    }

    @Test func mathGameSurvivesAModelReload() {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("gravitile-math-reload-\(UUID().uuidString).json")
        let first = AppModel(persistence: PersistenceService(fileURL: url))
        var game = first.mathGame()
        playOneMove(&game)
        first.checkpoint(game)

        let second = AppModel(persistence: PersistenceService(fileURL: url))
        #expect(second.mathGame().moveCount == game.moveCount)
        #expect(second.mathGame().mode == .math)
    }

    @Test func mathSubmitsNothingToGameCenter() {
        let entries = GameCenterService.leaderboardEntries(for: GameState(mode: .math, seed: 1))
        #expect(entries.isEmpty)
    }
}

@MainActor
@Suite struct ThemeTests {
    @Test func paletteLookupFallsBackToEmber() {
        #expect(Theme.palette(id: "ember").id == "ember")
        #expect(Theme.palette(id: "does-not-exist").id == "ember")
        #expect(Theme.palettes.count == 5)
    }

    @Test func everyPaletteCoversTheFullTileRamp() {
        var value = 2
        while value <= 65536 {
            for palette in Theme.palettes {
                #expect(palette.tileColors[value] != nil, "\(palette.id) missing \(value)")
            }
            value *= 2
        }
    }

    @Test func themeChoicePersistsThroughSettings() {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("gravitile-theme-test-\(UUID().uuidString).json")
        let model = AppModel(persistence: PersistenceService(fileURL: url))
        var settings = model.settings
        settings.themeID = "tidepool"
        model.settings = settings
        #expect(Theme.current.id == "tidepool")

        let reloaded = AppModel(persistence: PersistenceService(fileURL: url))
        #expect(reloaded.settings.themeID == "tidepool")
        #expect(Theme.current.id == "tidepool")

        // Leave the process-global default in place for other suites.
        settings.themeID = "ember"
        model.settings = settings
    }

    @Test func mathTilesKeepCuisenaireIdentity() {
        // Distinct colors for every digit; targets flash gold.
        let digits = (1...9).map { Theme.mathTileColor(for: $0) }
        #expect(Set(digits.map(\.description)).count == 9)
        #expect(Theme.mathTileColor(for: 10) == Theme.mathTileColor(for: 16))
    }
}
