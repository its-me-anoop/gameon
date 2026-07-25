import Testing
import Foundation
import OrbitKit
@testable import Gravitile

@Suite @MainActor struct RecordsTests {
    @Test func recordsOnlyEverGoUp() {
        var records = Records()
        var world = World(state: WorldState(seed: 3))
        world.state.resources = Resources(ore: 99_999, charge: 99_999, water: 99_999, alloy: 99_999)
        for tile in 0..<12 { world.place(.ballast, on: tile) }
        records.absorb(world)
        let peak = records.heaviestWorld
        #expect(peak > 0)

        // A collapse wipes the surface; the record must survive it.
        world.state.resources = Resources(ore: 999_999, charge: 99_999, water: 99_999, alloy: 999_999)
        var tile = 12
        while !world.canCollapse, tile < world.geometry.tileCount {
            world.place(.ballast, on: tile)
            tile += 1
        }
        world.collapse()
        records.absorb(world)
        #expect(records.heaviestWorld >= peak)
        #expect(records.collapses == 1)
        #expect(records.totalGravity > 0)
    }

    @Test func fastestFirstCollapseKeepsTheLowestTime() {
        var records = Records()
        var world = World(state: WorldState(seed: 5))
        world.state.firstCollapseSeconds = 900
        records.absorb(world)
        #expect(records.fastestFirstCollapse == 900)

        world.state.firstCollapseSeconds = 1500
        records.absorb(world)
        #expect(records.fastestFirstCollapse == 900, "a slower run must not overwrite a faster one")
    }

    @Test func settingsSurviveAFileMissingNewerKeys() throws {
        let legacy = Data(#"{"soundOn": false}"#.utf8)
        let settings = try JSONDecoder().decode(Settings.self, from: legacy)
        #expect(!settings.soundOn)
        #expect(settings.musicOn, "missing keys must fall back to defaults, not throw")
        #expect(settings.themeID == "ember")
    }

    @Test func storageRoundTripsAWorld() {
        let storage = WorldStorage(directoryName: "GravitileTests-\(UUID().uuidString)")
        var world = World(state: WorldState(seed: 11))
        world.place(.solarArray, on: 4)
        world.advance(by: 120)
        storage.save(world: world.state)

        let restored = storage.loadWorld()
        #expect(restored == world.state)
        storage.wipe()
        #expect(storage.loadWorld() == nil)
    }
}

@Suite @MainActor struct GameCenterRoutingTests {
    @Test func emptyRecordsSubmitNothing() {
        #expect(GameCenterService.entries(for: Records()).isEmpty)
        #expect(GameCenterService.achievements(for: Records()).isEmpty)
    }

    @Test func eachRecordGoesToItsOwnBoard() {
        var records = Records()
        records.heaviestWorld = 1234.6
        records.totalGravity = 42
        records.collapses = 3
        records.fastestFirstCollapse = 1499.4

        let entries = GameCenterService.entries(for: records)
        let byBoard = Dictionary(uniqueKeysWithValues: entries.map { ($0.board, $0.score) })
        #expect(byBoard[GameCenterService.massLeaderboardID] == 1235)
        #expect(byBoard[GameCenterService.gravityLeaderboardID] == 42)
        #expect(byBoard[GameCenterService.tierLeaderboardID] == 3)
        #expect(byBoard[GameCenterService.speedrunLeaderboardID] == 1499)
    }

    @Test func achievementsUnlockInOrder() {
        var records = Records()
        records.machinesBuilt = 1
        #expect(GameCenterService.achievements(for: records) == ["grv2.first.machine"])

        records.meteorsCaught = 2
        records.collapses = 3
        records.heaviestWorld = 2500
        records.totalGravity = 120
        let earned = Set(GameCenterService.achievements(for: records))
        #expect(earned.contains("grv2.first.meteor"))
        #expect(earned.contains("grv2.first.collapse"))
        #expect(earned.contains("grv2.tier.three"))
        #expect(earned.contains("grv2.mass.2000"))
        #expect(earned.contains("grv2.gravity.100"))
    }
}
