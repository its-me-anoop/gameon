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
