import GameKit
import GravitileKit

/// Game Center integration. Everything degrades silently when the player
/// isn't authenticated — the game never blocks on it.
@Observable @MainActor
final class GameCenterService {
    static let endlessLeaderboardID = "grv.endless.best"
    static let dailyLeaderboardID = "grv.daily.score"
    static let bestTileLeaderboardID = "grv.best.tile"

    private(set) var isAuthenticated = false

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
                GKAccessPoint.shared.location = .topLeading
            }
        }
    }

    func submit(game: GameState) {
        guard isAuthenticated else { return }
        var entries: [(Int, String)] = [(game.bestTile, Self.bestTileLeaderboardID)]
        switch game.mode {
        case .endless:
            entries.append((game.score, Self.endlessLeaderboardID))
        case .daily:
            entries.append((game.score, Self.dailyLeaderboardID))
        }
        for (score, board) in entries {
            GKLeaderboard.submitScore(
                score, context: 0, player: GKLocalPlayer.local,
                leaderboardIDs: [board]
            ) { _ in }
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
