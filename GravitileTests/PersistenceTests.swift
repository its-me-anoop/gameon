import Testing
import Foundation
import GravitileKit
@testable import Gravitile

@Suite struct PersistenceTests {
    private func temporaryFile() -> URL {
        FileManager.default.temporaryDirectory
            .appendingPathComponent("gravitile-test-\(UUID().uuidString).json")
    }

    @Test func roundTripsFullState() {
        let url = temporaryFile()
        defer { try? FileManager.default.removeItem(at: url) }
        let service = PersistenceService(fileURL: url)

        var state = PersistedState()
        var game = GameState(mode: .endless, seed: 7)
        game.applyMove(.left)
        state.endlessGame = game
        state.bestEndlessScore = 1234
        state.dailyRecords[3] = DailyRecord(
            puzzleNumber: 3, score: 400, bestTile: 64, cascadeCount: 5,
            completedAt: Date(timeIntervalSince1970: 1_700_000_000)
        )
        state.streak.recordCompletion(puzzleNumber: 3)
        state.settings.soundOn = false

        service.save(state)
        let loaded = service.load()
        #expect(loaded == state)

        // The restored game must continue deterministically.
        var a = state.endlessGame!
        var b = loaded.endlessGame!
        #expect(a.applyMove(.up) == b.applyMove(.up))
    }

    /// A byte-for-byte v1.0 save file (no musicOn, no zen/sprint slots) must
    /// decode losslessly with new fields defaulting — an update that wipes
    /// player streaks would be unforgivable.
    @Test func v10SaveFileDecodesLosslessly() throws {
        let url = temporaryFile()
        defer { try? FileManager.default.removeItem(at: url) }
        let fixture = #"""
        {"version":1,"payload":{"bestEndlessScore":1234,"bestTileEver":128,
        "dailyRecords":{"3":{"puzzleNumber":3,"score":400,"bestTile":64,"cascadeCount":5,"completedAt":727000000}},
        "streak":{"current":2,"longest":5,"lastCompletedPuzzle":3},
        "stats":{"gamesPlayed":10,"totalScore":5000,"totalCascades":42,"bestCascadeRound":4},
        "settings":{"soundOn":false,"hapticsOn":true,"themeID":"ember","hasSeenTutorial":true}}}
        """#
        try Data(fixture.utf8).write(to: url)
        let loaded = PersistenceService(fileURL: url).load()
        #expect(loaded.bestEndlessScore == 1234)
        #expect(loaded.bestTileEver == 128)
        #expect(loaded.dailyRecords[3]?.score == 400)
        #expect(loaded.streak.current == 2)
        #expect(loaded.streak.longest == 5)
        #expect(loaded.stats.gamesPlayed == 10)
        #expect(loaded.settings.soundOn == false)
        #expect(loaded.settings.hasSeenTutorial == true)
        // v1.1 additions default sanely instead of failing the decode.
        #expect(loaded.settings.musicOn == true)
        #expect(loaded.zenGame == nil)
        #expect(loaded.sprintGame == nil)
        #expect(loaded.bestZenTile == 0)
        #expect(loaded.bestSprintScore == 0)
    }

    @Test func roundTripsModeSlotsAndBests() {
        let url = temporaryFile()
        defer { try? FileManager.default.removeItem(at: url) }
        let service = PersistenceService(fileURL: url)

        var state = PersistedState()
        state.zenGame = GameState(mode: .zen, seed: 9)
        state.sprintGame = GameState(mode: .sprint, seed: 10)
        state.bestZenTile = 512
        state.bestSprintScore = 4321
        state.settings.musicOn = false

        service.save(state)
        let loaded = service.load()
        #expect(loaded == state)
        #expect(loaded.zenGame?.mode == .zen)
        #expect(loaded.sprintGame?.mode == .sprint)
    }

    @Test func corruptFileYieldsFreshStateWithoutCrashing() throws {
        let url = temporaryFile()
        defer { try? FileManager.default.removeItem(at: url) }
        try Data("not json at all {{{".utf8).write(to: url)
        let service = PersistenceService(fileURL: url)
        #expect(service.load() == PersistedState())
    }

    @Test func missingFileYieldsFreshState() {
        let service = PersistenceService(fileURL: temporaryFile())
        #expect(service.load() == PersistedState())
    }

    @Test func entitlementGatingValues() {
        #expect(Entitlements.maxUndosPerGame(isPlus: false) == 1)
        #expect(Entitlements.maxUndosPerGame(isPlus: true) == Int.max)
        #expect(!Entitlements.canPlayArchive(isPlus: false))
        #expect(Entitlements.canPlayArchive(isPlus: true))
    }
}
