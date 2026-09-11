import Foundation

@main
struct LifelineWeeklyOccurrenceTests {
    static func main() {
        let formatter = ISO8601DateFormatter()
        func date(_ value: String) -> Date { formatter.date(from: value)! }
        let start = date("2026-09-14T00:00:00Z")
        let end = date("2026-09-21T00:00:00Z")
        let key = "2026-09-14"
        precondition(LifelineWeeklyOccurrence.start(for: key) == start)
        precondition(LifelineWeeklyOccurrence.start(for: "2026-09-15") == nil)
        precondition(LifelineWeeklyOccurrence.start(for: "2026-02-30") == nil)
        precondition(LifelineWeeklyOccurrence.start(for: "2026-9-14") == nil)
        precondition(!LifelineWeeklyOccurrence.contains(key, now: start.addingTimeInterval(-0.1)))
        precondition(LifelineWeeklyOccurrence.contains(key, now: start))
        precondition(LifelineWeeklyOccurrence.contains(key, now: end.addingTimeInterval(-0.1)))
        precondition(!LifelineWeeklyOccurrence.contains(key, now: end))
        precondition(!LifelineWeeklyOccurrence.contains(nil, now: start))
        precondition(LifelineWeeklyOccurrence.matches(key, start: start, duration: 604800, now: start))
        precondition(!LifelineWeeklyOccurrence.matches(key, start: start.addingTimeInterval(3600), duration: 604800, now: start))
        precondition(!LifelineWeeklyOccurrence.matches(key, start: start, duration: 86400, now: start))
        precondition(!LifelineWeeklyOccurrence.matches(key, start: end, duration: 604800, now: end))
        let saved = OrchardPendingScore(id: UUID(), score: 9000099, mode: .lifelineWeekly, dayKey: key, playerID: "first-account")
        let decoded = try! JSONDecoder().decode(OrchardPendingScore.self, from: JSONEncoder().encode(saved))
        precondition(decoded == saved)
        precondition(LifelineWeeklyOccurrence.availabilityMessage(now: start.addingTimeInterval(-1)) != nil)
        precondition(LifelineWeeklyOccurrence.availabilityMessage(now: start) == nil)
        print("Native weekly occurrence and queue-model checks: 16 passed.")
    }
}
