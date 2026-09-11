import Foundation

/// Both the seed and the ranking occurrence start at Monday 00:00 UTC.
/// Kept free of GameKit so boundaries can be tested without signing in.
enum LifelineWeeklyOccurrence {
    static let duration: TimeInterval = 7 * 24 * 60 * 60
    static let launchDate = ISO8601DateFormatter().date(from: "2026-09-14T00:00:00Z")!
    static func availabilityMessage(now: Date = Date()) -> String? {
        now < launchDate ? "Weekly rankings open 14 September at 00:00 UTC. You can practise now." : nil
    }
    static func start(for key: String) -> Date? {
        let formatter = DateFormatter()
        formatter.calendar = Calendar(identifier: .gregorian)
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone(secondsFromGMT: 0)
        formatter.dateFormat = "yyyy-MM-dd"
        formatter.isLenient = false
        guard let date = formatter.date(from: key), formatter.string(from: date) == key else { return nil }
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(secondsFromGMT: 0)!
        return calendar.component(.weekday, from: date) == 2 ? date : nil
    }
    static func contains(_ key: String?, now: Date) -> Bool {
        guard let key, let start = start(for: key) else { return false }
        return start <= now && now < start.addingTimeInterval(duration)
    }
    static func matches(_ key: String?, start: Date?, duration: TimeInterval, now: Date) -> Bool {
        guard let key, let expected = self.start(for: key), let start else { return false }
        return abs(start.timeIntervalSince(expected)) < 0.01 && abs(duration - self.duration) < 0.01
            && contains(key, now: now)
    }
}

enum OrchardScoreMode: String, Codable { case classic, daily, practice, lifelineWeekly }
struct OrchardPendingScore: Codable, Equatable {
    let id: UUID
    let score: Int
    let mode: OrchardScoreMode
    let dayKey: String?
    var playerID: String?
}

#if canImport(UIKit)
import GameKit
import UIKit

@MainActor
final class OrchardGameCenterService {
    var onChange: (() -> Void)?
    nonisolated static let weeklyLeaderboardID = "grv3.lifeline.weekly.v1"
    // A distinct store deliberately leaves earlier games' queued scores untouched and unread.
    private static let queueKey = "little-lifeline.game-center.pending.v1"
    enum AuthenticationState: Equatable { case signedOut, authenticating, authenticated, unavailable }
    private(set) var isAuthenticated = false { didSet { onChange?() } }
    private(set) var authenticationState: AuthenticationState = .signedOut { didSet { onChange?() } }
    private(set) var playerDisplayName: String? { didSet { onChange?() } }
    private(set) var statusMessage = LifelineWeeklyOccurrence.availabilityMessage() ?? "Connect to Game Center for the weekly management challenge." { didSet { onChange?() } }
    private(set) var queuedScoreCount = 0 { didSet { onChange?() } }
    private(set) var isSubmitting = false { didSet { onChange?() } }
    private var pendingScores: [OrchardPendingScore] = []
    private var retryTask: Task<Void, Never>?
    private var authenticationRequested = false
    private let defaults: UserDefaults

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        if let data = defaults.data(forKey: Self.queueKey),
           let saved = try? JSONDecoder().decode([OrchardPendingScore].self, from: data) {
            pendingScores = saved.filter { $0.score > 0 && $0.score <= Int(Int32.max) && $0.mode == .lifelineWeekly }
        }
        pruneExpiredScores()
        retryTask = Task { [weak self] in
            while !Task.isCancelled {
                do { try await Task.sleep(for: .seconds(30)) } catch { return }
                guard let self else { return }
                if UIApplication.shared.applicationState == .active { await self.retryPendingScores() }
            }
        }
    }
    deinit { retryTask?.cancel() }

    /// Wire to a player action. Initializing this service never opens Game Center login.
    func authenticate() {
        if isAuthenticated && GKLocalPlayer.local.isAuthenticated {
            Task { await retryPendingScores() }
            return
        }
        guard authenticationState != .authenticating else { return }
        authenticationRequested = true
        authenticationState = .authenticating
        statusMessage = "Connecting to Game Center…"
        GKLocalPlayer.local.authenticateHandler = { [weak self] viewController, error in
            Task { @MainActor in
                guard let self else { return }
                if let viewController {
                    guard self.authenticationRequested else { return }
                    guard var presenter = UIApplication.shared.connectedScenes
                        .compactMap({ $0 as? UIWindowScene })
                        .filter({ $0.activationState == .foregroundActive })
                        .flatMap({ $0.windows }).first(where: \.isKeyWindow)?.rootViewController else {
                        self.authenticationState = .unavailable
                        self.statusMessage = "Game Center couldn't open. Please try again."
                        return
                    }
                    while let presented = presenter.presentedViewController {
                        presenter = presented
                    }
                    presenter.present(viewController, animated: true)
                    self.authenticationRequested = false
                    return
                }
                self.authenticationRequested = false
                self.isAuthenticated = GKLocalPlayer.local.isAuthenticated
                self.authenticationState = self.isAuthenticated ? .authenticated : .unavailable
                self.playerDisplayName = self.isAuthenticated ? GKLocalPlayer.local.displayName : nil
                if self.isAuthenticated {
                    self.statusMessage = LifelineWeeklyOccurrence.availabilityMessage() ?? "Connected as \(GKLocalPlayer.local.displayName)."
                    await self.retryPendingScores()
                } else {
                    self.statusMessage = error == nil
                        ? "Game Center is signed out. Your records stay on this device."
                        : "Game Center is unavailable. Check your connection or Game Center in Settings."
                }
            }
        }
    }

    /// Only the new equal-start weekly challenge is ranked. Campaign progress never submits.
    func submit(score: Int, mode: OrchardScoreMode, dayKey: String? = nil) {
        guard mode == .lifelineWeekly else {
            statusMessage = "Only Little Lifeline weekly challenges use these rankings."
            return
        }
        guard score > 0, score <= Int(Int32.max) else { return }
        if let opening = LifelineWeeklyOccurrence.availabilityMessage() {
            statusMessage = opening
            return
        }
        guard LifelineWeeklyOccurrence.contains(dayKey, now: Date()) else {
            statusMessage = "That challenge week has ended. Its result stays on this device."
            return
        }
        let playerID = isAuthenticated && GKLocalPlayer.local.isAuthenticated ? GKLocalPlayer.local.gamePlayerID : nil
        let previous = pendingScores.first { $0.dayKey == dayKey && $0.playerID == playerID }
        guard score > (previous?.score ?? 0) else { return }
        pendingScores.removeAll { $0.dayKey == dayKey && $0.playerID == playerID }
        pendingScores.append(.init(id: UUID(), score: score, mode: mode, dayKey: dayKey, playerID: playerID))
        persistQueue()
        if isAuthenticated { Task { await retryPendingScores() } }
        else { statusMessage = "Weekly result saved. Connect to Game Center to submit it." }
    }

    func currentWeeklyLeaderboard() async -> GKLeaderboard? {
        if let opening = LifelineWeeklyOccurrence.availabilityMessage() {
            statusMessage = opening
            return nil
        }
        guard isAuthenticated, GKLocalPlayer.local.isAuthenticated else {
            statusMessage = "Connect to Game Center to view weekly rankings."
            return nil
        }
        do {
            let boards = try await GKLeaderboard.loadLeaderboards(IDs: [Self.weeklyLeaderboardID])
            guard let board = boards.first, let start = board.startDate,
                  abs(board.duration - LifelineWeeklyOccurrence.duration) < 0.01,
                  start <= Date(), start.addingTimeInterval(board.duration) > Date() else {
                statusMessage = "Weekly rankings are not available yet. Challenge results stay on this device."
                return nil
            }
            return board
        } catch {
            statusMessage = "Weekly rankings could not load. Check your connection and try again."
            return nil
        }
    }

    func retryPendingScores() async {
        pruneExpiredScores()
        guard isAuthenticated, GKLocalPlayer.local.isAuthenticated, !isSubmitting else { return }
        let localPlayer = GKLocalPlayer.local
        let playerID = localPlayer.gamePlayerID
        // Claim anonymous results once; an account switch cannot reassign an existing result.
        for index in pendingScores.indices where pendingScores[index].playerID == nil {
            pendingScores[index].playerID = playerID
        }
        persistQueue()
        let submissions = pendingScores.filter { $0.playerID == playerID }
        guard !submissions.isEmpty else { return }
        isSubmitting = true
        defer { isSubmitting = false }
        var failed = false
        var submitted = false
        for entry in submissions {
            guard GKLocalPlayer.local.isAuthenticated, GKLocalPlayer.local.gamePlayerID == playerID else { break }
            do {
                let boards = try await GKLeaderboard.loadLeaderboards(IDs: [Self.weeklyLeaderboardID])
                // Loading the recurring occurrence and checking its exact UTC interval prevents a
                // late network response or clock boundary from routing old seeds into a new week.
                guard let board = boards.first,
                      LifelineWeeklyOccurrence.matches(entry.dayKey, start: board.startDate, duration: board.duration, now: Date()),
                      GKLocalPlayer.local.isAuthenticated, GKLocalPlayer.local.gamePlayerID == playerID else {
                    failed = true
                    continue
                }
                try await board.submitScore(entry.score, context: 1, player: localPlayer)
                pendingScores.removeAll { $0.id == entry.id }
                persistQueue()
                submitted = true
            } catch { failed = true }
        }
        if failed {
            statusMessage = "Weekly rankings are unavailable. Your result is saved and will retry until Monday 00:00 UTC."
        } else if submitted { statusMessage = "Weekly result submitted to Game Center." }
    }

    private func pruneExpiredScores() {
        pendingScores.removeAll { $0.mode != .lifelineWeekly || !LifelineWeeklyOccurrence.contains($0.dayKey, now: Date()) }
        persistQueue()
    }
    private func persistQueue() {
        queuedScoreCount = pendingScores.count
        if let data = try? JSONEncoder().encode(pendingScores) { defaults.set(data, forKey: Self.queueKey) }
    }
}
#endif
