import SwiftUI
import GravitileKit

struct RootView: View {
    @Environment(AppModel.self) private var appModel
    @State private var path: [Route] = []

    var body: some View {
        NavigationStack(path: $path) {
            HomeScreen()
                .navigationDestination(for: Route.self) { route in
                    destination(for: route)
                }
        }
        .tint(Theme.accent)
        .preferredColorScheme(.dark)
        .onAppear {
            appModel.gameCenter.authenticate()
            #if DEBUG
            // Debug hooks for unattended simulator runs: jump to a route for
            // screenshot capture, or into endless for the auto-player.
            switch ProcessInfo.processInfo.environment["GRAVITILE_ROUTE"] {
            case "endless": path = [.endless]
            case "zen": path = [.zen]
            case "sprint": path = [.sprint]
            case "daily": path = [.daily]
            case "stats": path = [.stats]
            case "paywall": path = [.paywall]
            default: break
            }
            if ProcessInfo.processInfo.environment["GRAVITILE_AUTOPLAY"] == "1", path.isEmpty {
                path = [.endless]  // autoplay composes with GRAVITILE_ROUTE
            }
            #endif
        }
    }

    @ViewBuilder
    private func destination(for route: Route) -> some View {
        switch route {
        case .endless:
            GameScreen(
                game: appModel.endlessGame(),
                freeUndoLimit: Entitlements.maxUndosPerGame(isPlus: appModel.isPlus)
            )
        case .zen:
            GameScreen(
                game: appModel.zenGame(),
                freeUndoLimit: Entitlements.maxUndosPerGame(isPlus: appModel.isPlus)
            )
        case .sprint:
            GameScreen(
                game: appModel.sprintGame(),
                freeUndoLimit: Entitlements.maxUndosPerGame(isPlus: appModel.isPlus)
            )
        case .daily:
            DailyScreen()
        case let .dailyPuzzle(number):
            GameScreen(
                game: appModel.dailyGame(puzzleNumber: number),
                freeUndoLimit: Entitlements.maxUndosPerGame(isPlus: appModel.isPlus)
            )
        case .stats:
            StatsScreen()
        case .settings:
            SettingsScreen()
        case .paywall:
            PaywallView()
        }
    }
}
