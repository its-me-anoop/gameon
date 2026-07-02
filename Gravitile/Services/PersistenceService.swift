import Foundation
import GravitileKit

struct Settings: Codable, Equatable {
    var soundOn = true
    var hapticsOn = true
    var themeID = "ember"
    var hasSeenTutorial = false
}

struct DailyRecord: Codable, Equatable {
    var puzzleNumber: Int
    var score: Int
    var bestTile: Int
    var cascadeCount: Int
    var completedAt: Date
}

struct StreakState: Codable, Equatable {
    var current = 0
    var longest = 0
    var lastCompletedPuzzle: Int?
    /// ISO week number (year * 100 + week) in which the last freeze was spent.
    var freezeSpentInWeek: Int?

    /// Completing puzzle n: consecutive (n == last+1) extends the streak; a
    /// single missed day can be bridged by one freeze per ISO week; anything
    /// else restarts at 1. Completing the same or an older puzzle (archive
    /// replays) never changes the streak.
    mutating func recordCompletion(puzzleNumber: Int, on date: Date = Date()) {
        guard puzzleNumber > (lastCompletedPuzzle ?? Int.min) else { return }
        defer {
            lastCompletedPuzzle = puzzleNumber
            longest = max(longest, current)
        }
        guard let last = lastCompletedPuzzle else {
            current = 1
            return
        }
        switch puzzleNumber - last {
        case 1:
            current += 1
        case 2 where freezeAvailable(on: date):
            current += 1
            freezeSpentInWeek = weekKey(for: date)
        default:
            current = 1
        }
    }

    private func freezeAvailable(on date: Date) -> Bool {
        freezeSpentInWeek != weekKey(for: date)
    }

    private func weekKey(for date: Date) -> Int {
        var calendar = Calendar(identifier: .iso8601)
        calendar.timeZone = TimeZone(identifier: "UTC")!
        let components = calendar.dateComponents([.yearForWeekOfYear, .weekOfYear], from: date)
        return components.yearForWeekOfYear! * 100 + components.weekOfYear!
    }
}

struct LifetimeStats: Codable, Equatable {
    var gamesPlayed = 0
    var totalScore = 0
    var totalCascades = 0
    var bestCascadeRound = 0
}

struct PersistedState: Codable, Equatable {
    var endlessGame: GameState?
    var dailyGame: GameState?
    var bestEndlessScore = 0
    var bestTileEver = 0
    var dailyRecords: [Int: DailyRecord] = [:]
    var streak = StreakState()
    var stats = LifetimeStats()
    var settings = Settings()
}

/// JSON file persistence with a versioned envelope so future schema changes
/// can migrate instead of wiping. Writes are atomic; a corrupt or unreadable
/// file yields a fresh state rather than a crash.
final class PersistenceService: Sendable {
    private struct Envelope: Codable {
        var version: Int
        var payload: PersistedState
    }

    static let currentVersion = 1
    private let fileURL: URL

    init(fileURL: URL? = nil) {
        if let fileURL {
            self.fileURL = fileURL
        } else {
            let support = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            try? FileManager.default.createDirectory(at: support, withIntermediateDirectories: true)
            self.fileURL = support.appendingPathComponent("gravitile-state.json")
        }
    }

    func load() -> PersistedState {
        guard let data = try? Data(contentsOf: fileURL),
              let envelope = try? JSONDecoder().decode(Envelope.self, from: data)
        else { return PersistedState() }
        return envelope.payload
    }

    func save(_ state: PersistedState) {
        let envelope = Envelope(version: Self.currentVersion, payload: state)
        guard let data = try? JSONEncoder().encode(envelope) else { return }
        try? data.write(to: fileURL, options: .atomic)
    }
}
