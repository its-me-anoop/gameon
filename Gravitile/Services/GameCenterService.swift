import GameKit
import GravitileKit

/// Game Center integration. Everything degrades silently when the player
/// isn't authenticated — the game never blocks on it.
@Observable @MainActor
final class GameCenterService {
    static let endlessLeaderboardID = "grv.endless.best"
    static let dailyLeaderboardID = "grv.daily.score"
    static let bestTileLeaderboardID = "grv.best.tile"
    static let zenTileLeaderboardID = "grv.zen.tile"
    static let sprintLeaderboardID = "grv.sprint.best"
    static let dailyWeeklyLeaderboardID = "grv.daily.weekly"

    private(set) var isAuthenticated = false
    /// Whether the current screen wants the floating GKAccessPoint visible.
    private var accessPointWanted = false

    func authenticate() {
        GKLocalPlayer.local.authenticateHandler = { [weak self] viewController, error in
            Task { @MainActor in
                guard let self else { return }
                if let viewController {
                    // Present Apple's sign-in UI if the system provides one.
                    UIApplication.shared.connectedScenes
                        .compactMap { ($0 as? UIWindowScene)?.keyWindow?.rootViewController }
                        .first?
                        .present(viewController, animated: true)
                    return
                }
                self.isAuthenticated = error == nil && GKLocalPlayer.local.isAuthenticated
                self.applyAccessPoint()
            }
        }
    }

    /// Screens opt the access point in/out (Home shows it; gameplay never).
    func setAccessPointActive(_ active: Bool) {
        accessPointWanted = active
        applyAccessPoint()
    }

    private func applyAccessPoint() {
        GKAccessPoint.shared.location = .topTrailing
        GKAccessPoint.shared.showHighlights = false
        GKAccessPoint.shared.isActive = accessPointWanted && isAuthenticated
    }

    /// Pure routing — which scores land on which boards — split out so tests
    /// don't need GameKit. Daily scores go to both the classic all-time board
    /// and the weekly recurring one. Math Pop submits nothing: no board exists
    /// for it yet, and its 1–9 tiles would corrupt the doubling-tile records.
    static func leaderboardEntries(for game: GameState) -> [(score: Int, board: String)] {
        if case .math = game.mode { return [] }
        var entries: [(score: Int, board: String)] = [(game.bestTile, bestTileLeaderboardID)]
        switch game.mode {
        case .endless:
            entries.append((game.score, endlessLeaderboardID))
        case .daily:
            entries.append((game.score, dailyLeaderboardID))
            entries.append((game.score, dailyWeeklyLeaderboardID))
        case .zen:
            entries.append((game.bestTile, zenTileLeaderboardID))
        case .sprint:
            entries.append((game.score, sprintLeaderboardID))
        case .math:
            break
        }
        return entries
    }

    func submit(game: GameState) {
        guard isAuthenticated else { return }
        // Math Pop is off the boards entirely — including achievements, whose
        // merge/tile wording describes the doubling modes.
        if case .math = game.mode { return }
        for entry in Self.leaderboardEntries(for: game) {
            if entry.board == Self.dailyWeeklyLeaderboardID {
                // Occurrence-scoped submit: a late-arriving network call must
                // not leak last week's score into the new occurrence.
                GKLeaderboard.loadLeaderboards(IDs: [entry.board]) { boards, _ in
                    boards?.first?.submitScore(
                        entry.score, context: 0, player: GKLocalPlayer.local
                    ) { _ in }
                }
            } else {
                GKLeaderboard.submitScore(
                    entry.score, context: 0, player: GKLocalPlayer.local,
                    leaderboardIDs: [entry.board]
                ) { _ in }
            }
        }
        reportAchievements(for: game)
    }

    private func reportAchievements(for game: GameState) {
        var earned: [String] = []
        if game.score > 0 { earned.append("grv.first.merge") }
        if game.cascadeCount >= 1 { earned.append("grv.first.cascade") }
        if game.bestTile >= 256 { earned.append("grv.tile.256") }
        if game.bestTile >= 512 { earned.append("grv.tile.512") }
        if game.bestTile >= 1024 { earned.append("grv.tile.1024") }
        if game.bestTile >= 2048 { earned.append("grv.tile.2048") }
        guard !earned.isEmpty else { return }
        let achievements = earned.map { identifier in
            let achievement = GKAchievement(identifier: identifier)
            achievement.percentComplete = 100
            achievement.showsCompletionBanner = true
            return achievement
        }
        GKAchievement.report(achievements) { _ in }
    }

    struct AchievementStatus: Identifiable, Equatable {
        let id: String
        let title: String
        let detail: String
        let earned: Bool
    }

    /// Descriptions + completion state for the in-app achievements list.
    /// Empty when unauthenticated or offline — callers hide the section.
    func loadAchievements() async -> [AchievementStatus] {
        guard isAuthenticated else { return [] }
        do {
            let descriptions = try await GKAchievementDescription.loadAchievementDescriptions()
            let earnedIDs = Set(
                ((try? await GKAchievement.loadAchievements()) ?? [])
                    .filter(\.isCompleted)
                    .map(\.identifier)
            )
            return descriptions.map { description in
                let earned = earnedIDs.contains(description.identifier)
                return AchievementStatus(
                    id: description.identifier,
                    title: description.title,
                    detail: earned ? description.achievedDescription : description.unachievedDescription,
                    earned: earned
                )
            }
        } catch {
            return []
        }
    }

    func submitStreak(_ streak: StreakState) {
        guard isAuthenticated else { return }
        var earned: [String] = []
        if streak.current >= 7 { earned.append("grv.streak.7") }
        if streak.current >= 30 { earned.append("grv.streak.30") }
        guard !earned.isEmpty else { return }
        GKAchievement.report(earned.map {
            let achievement = GKAchievement(identifier: $0)
            achievement.percentComplete = 100
            return achievement
        }) { _ in }
    }
}
