import Foundation

/// Deterministic RNG (SplitMix64). Codable so a saved game resumes with an
/// identical random stream, keeping every game replayable.
public struct SplitMix64: RandomNumberGenerator, Codable, Equatable, Sendable {
    public private(set) var state: UInt64

    public init(seed: UInt64) {
        state = seed
    }

    public mutating func next() -> UInt64 {
        state &+= 0x9E37_79B9_7F4A_7C15
        var z = state
        z = (z ^ (z >> 30)) &* 0xBF58_476D_1CE4_E5B9
        z = (z ^ (z >> 27)) &* 0x94D0_49BB_1331_11EB
        return z ^ (z >> 31)
    }
}

/// Derives the seed shared by all players for a given day's puzzle.
/// Day boundaries are UTC so the daily is globally fair.
public enum DailySeed {
    /// Puzzle #1 is 2026-07-01 (UTC).
    private static let anchor = DateComponents(
        calendar: utcCalendar, year: 2026, month: 7, day: 1
    ).date!

    private static var utcCalendar: Calendar {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(identifier: "UTC")!
        return calendar
    }

    public static func seed(for date: Date) -> UInt64 {
        fnv1a("gravitile-\(dayKey(for: date))")
    }

    public static func puzzleNumber(for date: Date) -> Int {
        let days = utcCalendar.dateComponents(
            [.day], from: anchor, to: utcCalendar.startOfDay(for: date)
        ).day ?? 0
        return days + 1
    }

    public static func seed(forPuzzleNumber number: Int) -> UInt64 {
        let date = utcCalendar.date(byAdding: .day, value: number - 1, to: anchor)!
        return seed(for: date)
    }

    public static func date(forPuzzleNumber number: Int) -> Date {
        utcCalendar.date(byAdding: .day, value: number - 1, to: anchor)!
    }

    private static func dayKey(for date: Date) -> String {
        let parts = utcCalendar.dateComponents([.year, .month, .day], from: date)
        return String(format: "%04d-%02d-%02d", parts.year!, parts.month!, parts.day!)
    }

    private static func fnv1a(_ string: String) -> UInt64 {
        var hash: UInt64 = 0xCBF2_9CE4_8422_2325
        for byte in Array(string.utf8) {
            hash ^= UInt64(byte)
            hash = hash &* 0x0000_0100_0000_01B3
        }
        return hash
    }
}
