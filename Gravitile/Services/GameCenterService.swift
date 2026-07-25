import GameKit
import OrbitKit

/// Game Center integration. Everything degrades silently when the player isn't
/// signed in — the game never blocks on it and never nags.
@Observable @MainActor
final class GameCenterService {
    static let massLeaderboardID = "grv2.mass.best"
    static let gravityLeaderboardID = "grv2.gravity"
    static let tierLeaderboardID = "grv2.collapse.tier"
    static let speedrunLeaderboardID = "grv2.speedrun.first"

    private(set) var isAuthenticated = false

    func authenticate() {
        guard !isAuthenticated else { return }
        GKLocalPlayer.local.authenticateHandler = { [weak self] viewController, error in
            Task { @MainActor in
                guard let self else { return }
                if let viewController {
                    UIApplication.shared.connectedScenes
                        .compactMap { ($0 as? UIWindowScene)?.keyWindow?.rootViewController }
                        .first?
                        .present(viewController, animated: true)
                    return
                }
                self.isAuthenticated = error == nil && GKLocalPlayer.local.isAuthenticated
            }
        }
    }

    /// Pure routing, split out so it can be tested without GameKit. Scores are
    /// integers, so mass is rounded and the speedrun is in whole seconds.
    static func entries(for records: Records) -> [(score: Int, board: String)] {
        var entries: [(score: Int, board: String)] = []
        if records.heaviestWorld > 0 {
            entries.append((Int(records.heaviestWorld.rounded()), massLeaderboardID))
        }
        if records.totalGravity > 0 {
            entries.append((records.totalGravity, gravityLeaderboardID))
        }
        if records.collapses > 0 {
            entries.append((records.collapses, tierLeaderboardID))
        }
        if let fastest = records.fastestFirstCollapse, fastest > 0 {
            entries.append((Int(fastest.rounded()), speedrunLeaderboardID))
        }
        return entries
    }

    func submit(records: Records) {
        guard isAuthenticated else { return }
        for entry in Self.entries(for: records) {
            GKLeaderboard.submitScore(
                entry.score, context: 0, player: GKLocalPlayer.local,
                leaderboardIDs: [entry.board]
            ) { _ in }
        }
        report(achievementsFor: records)
    }

    /// Achievement identifiers earned at the given records.
    static func achievements(for records: Records) -> [String] {
        var earned: [String] = []
        if records.machinesBuilt >= 1 { earned.append("grv2.first.machine") }
        if records.meteorsCaught >= 1 { earned.append("grv2.first.meteor") }
        if records.collapses >= 1 { earned.append("grv2.first.collapse") }
        if records.collapses >= 3 { earned.append("grv2.tier.three") }
        if records.heaviestWorld >= 2000 { earned.append("grv2.mass.2000") }
        if records.totalGravity >= 100 { earned.append("grv2.gravity.100") }
        return earned
    }

    private func report(achievementsFor records: Records) {
        let earned = Self.achievements(for: records)
        guard !earned.isEmpty else { return }
        GKAchievement.report(earned.map { identifier in
            let achievement = GKAchievement(identifier: identifier)
            achievement.percentComplete = 100
            achievement.showsCompletionBanner = true
            return achievement
        }) { _ in }
    }

    /// Reported separately: steering a world hard and letting it settle is a
    /// thing you do, not a number you accumulate.
    func reportSteering(wobbleDegrees: Double) {
        guard isAuthenticated, wobbleDegrees >= 45 else { return }
        let achievement = GKAchievement(identifier: "grv2.steered")
        achievement.percentComplete = 100
        achievement.showsCompletionBanner = true
        GKAchievement.report([achievement]) { _ in }
    }
}
