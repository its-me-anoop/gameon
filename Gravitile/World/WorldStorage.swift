import Foundation
import OrbitKit

/// Player settings. Hand-written decoding so a file written by an older build
/// (missing newer keys) loads with defaults instead of throwing — a throw here
/// would cost someone their world.
struct Settings: Codable, Equatable {
    var soundOn = true
    var musicOn = true
    var hapticsOn = true
    var themeID = "ember"
    var hasSeenTutorial = false

    init() {}

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        soundOn = try container.decodeIfPresent(Bool.self, forKey: .soundOn) ?? true
        musicOn = try container.decodeIfPresent(Bool.self, forKey: .musicOn) ?? true
        hapticsOn = try container.decodeIfPresent(Bool.self, forKey: .hapticsOn) ?? true
        themeID = try container.decodeIfPresent(String.self, forKey: .themeID) ?? "ember"
        hasSeenTutorial = try container.decodeIfPresent(Bool.self, forKey: .hasSeenTutorial) ?? false
    }
}

/// Lifetime records, kept separately from the world so a collapse — or a reset
/// — never erases what a player has achieved.
struct Records: Codable, Equatable {
    var heaviestWorld: Double = 0
    var totalGravity: Int = 0
    var deepestTier: Int = 0
    var fastestFirstCollapse: Double?
    var meteorsCaught: Int = 0
    var machinesBuilt: Int = 0
    var collapses: Int = 0

    init() {}

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        heaviestWorld = try container.decodeIfPresent(Double.self, forKey: .heaviestWorld) ?? 0
        totalGravity = try container.decodeIfPresent(Int.self, forKey: .totalGravity) ?? 0
        deepestTier = try container.decodeIfPresent(Int.self, forKey: .deepestTier) ?? 0
        fastestFirstCollapse = try container.decodeIfPresent(Double.self, forKey: .fastestFirstCollapse)
        meteorsCaught = try container.decodeIfPresent(Int.self, forKey: .meteorsCaught) ?? 0
        machinesBuilt = try container.decodeIfPresent(Int.self, forKey: .machinesBuilt) ?? 0
        collapses = try container.decodeIfPresent(Int.self, forKey: .collapses) ?? 0
    }

    mutating func absorb(_ world: World) {
        heaviestWorld = max(heaviestWorld, world.state.lifetimePeakMass)
        totalGravity = max(totalGravity, world.state.lifetimeGravity)
        deepestTier = max(deepestTier, world.state.tier)
        meteorsCaught = max(meteorsCaught, world.state.meteorsCaught)
        machinesBuilt = max(machinesBuilt, world.state.placements)
        collapses = max(collapses, world.state.collapses)
        if let first = world.state.firstCollapseSeconds {
            fastestFirstCollapse = min(fastestFirstCollapse ?? .greatestFiniteMagnitude, first)
        }
    }
}

/// JSON on disk in Application Support. Small enough that saving is cheap and
/// frequent, which matters for a game that is mostly left running.
struct WorldStorage {
    private let directory: URL
    private let worldURL: URL
    private let settingsURL: URL
    private let recordsURL: URL

    init(directoryName: String = "Gravitile") {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
        directory = base.appendingPathComponent(directoryName, isDirectory: true)
        worldURL = directory.appendingPathComponent("world.json")
        settingsURL = directory.appendingPathComponent("settings.json")
        recordsURL = directory.appendingPathComponent("records.json")
        try? FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
    }

    func loadWorld() -> WorldState? {
        guard let data = try? Data(contentsOf: worldURL) else { return nil }
        return try? JSONDecoder().decode(WorldState.self, from: data)
    }

    func save(world: WorldState) {
        guard let data = try? JSONEncoder().encode(world) else { return }
        try? data.write(to: worldURL, options: .atomic)
    }

    func loadSettings() -> Settings {
        guard let data = try? Data(contentsOf: settingsURL),
              let settings = try? JSONDecoder().decode(Settings.self, from: data)
        else { return Settings() }
        return settings
    }

    func save(settings: Settings) {
        guard let data = try? JSONEncoder().encode(settings) else { return }
        try? data.write(to: settingsURL, options: .atomic)
    }

    func loadRecords() -> Records {
        guard let data = try? Data(contentsOf: recordsURL),
              let records = try? JSONDecoder().decode(Records.self, from: data)
        else { return Records() }
        return records
    }

    func save(records: Records) {
        guard let data = try? JSONEncoder().encode(records) else { return }
        try? data.write(to: recordsURL, options: .atomic)
    }

    func wipe() {
        for url in [worldURL, settingsURL, recordsURL] {
            try? FileManager.default.removeItem(at: url)
        }
    }
}
