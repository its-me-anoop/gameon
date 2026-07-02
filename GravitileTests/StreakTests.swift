import Testing
import Foundation
@testable import Gravitile

@Suite struct StreakTests {
    private func date(_ iso: String) -> Date {
        ISO8601DateFormatter().date(from: iso)!
    }

    @Test func consecutiveDaysExtendStreak() {
        var streak = StreakState()
        streak.recordCompletion(puzzleNumber: 10, on: date("2026-07-10T10:00:00Z"))
        streak.recordCompletion(puzzleNumber: 11, on: date("2026-07-11T10:00:00Z"))
        streak.recordCompletion(puzzleNumber: 12, on: date("2026-07-12T10:00:00Z"))
        #expect(streak.current == 3)
        #expect(streak.longest == 3)
    }

    @Test func oneMissedDayBridgedByWeeklyFreeze() {
        var streak = StreakState()
        streak.recordCompletion(puzzleNumber: 10, on: date("2026-07-10T10:00:00Z"))
        streak.recordCompletion(puzzleNumber: 12, on: date("2026-07-12T10:00:00Z")) // skipped #11
        #expect(streak.current == 2)
        #expect(streak.freezeSpentInWeek != nil)
    }

    @Test func secondFreezeInSameWeekResets() {
        var streak = StreakState()
        streak.recordCompletion(puzzleNumber: 6, on: date("2026-07-06T10:00:00Z"))  // Monday
        streak.recordCompletion(puzzleNumber: 8, on: date("2026-07-08T10:00:00Z"))  // freeze #7
        #expect(streak.current == 2)
        streak.recordCompletion(puzzleNumber: 10, on: date("2026-07-10T10:00:00Z")) // needs freeze #9, same week
        #expect(streak.current == 1)
    }

    @Test func freezeRefreshesNextWeek() {
        var streak = StreakState()
        streak.recordCompletion(puzzleNumber: 6, on: date("2026-07-06T10:00:00Z"))  // Mon week 28
        streak.recordCompletion(puzzleNumber: 8, on: date("2026-07-08T10:00:00Z"))  // freeze, week 28
        streak.recordCompletion(puzzleNumber: 9, on: date("2026-07-09T10:00:00Z"))
        streak.recordCompletion(puzzleNumber: 10, on: date("2026-07-10T10:00:00Z"))
        #expect(streak.current == 4)
        streak.recordCompletion(puzzleNumber: 12, on: date("2026-07-14T10:00:00Z")) // freeze #11, week 29
        #expect(streak.current == 5)
    }

    @Test func gapOfTwoOrMoreResets() {
        var streak = StreakState()
        streak.recordCompletion(puzzleNumber: 10, on: date("2026-07-10T10:00:00Z"))
        streak.recordCompletion(puzzleNumber: 13, on: date("2026-07-13T10:00:00Z"))
        #expect(streak.current == 1)
        #expect(streak.longest == 1)
    }

    @Test func longestSurvivesReset() {
        var streak = StreakState()
        for day in 10...14 {
            streak.recordCompletion(puzzleNumber: day, on: date("2026-07-\(day)T10:00:00Z"))
        }
        #expect(streak.longest == 5)
        streak.recordCompletion(puzzleNumber: 20, on: date("2026-07-20T10:00:00Z"))
        #expect(streak.current == 1)
        #expect(streak.longest == 5)
    }

    @Test func archiveCatchUpOnNextPuzzleExtendsStreak() {
        // Plus's "catch up on a missed day": completing the next uncompleted
        // puzzle via the archive extends the streak like a normal completion.
        var streak = StreakState()
        streak.recordCompletion(puzzleNumber: 10, on: date("2026-07-10T10:00:00Z"))
        // Missed #11; played it from the archive on the 12th, then today's.
        streak.recordCompletion(puzzleNumber: 11, on: date("2026-07-12T09:00:00Z"))
        streak.recordCompletion(puzzleNumber: 12, on: date("2026-07-12T10:00:00Z"))
        #expect(streak.current == 3)
    }

    @Test func archiveReplaysNeverChangeStreak() {
        var streak = StreakState()
        streak.recordCompletion(puzzleNumber: 10, on: date("2026-07-10T10:00:00Z"))
        streak.recordCompletion(puzzleNumber: 11, on: date("2026-07-11T10:00:00Z"))
        streak.recordCompletion(puzzleNumber: 5, on: date("2026-07-11T11:00:00Z"))  // archive replay
        streak.recordCompletion(puzzleNumber: 11, on: date("2026-07-11T12:00:00Z")) // same-day repeat
        #expect(streak.current == 2)
        #expect(streak.lastCompletedPuzzle == 11)
    }
}
