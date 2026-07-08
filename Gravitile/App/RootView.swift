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
        // Reading the palette off the observable model (not the Theme statics)
        // is what re-renders the stack when Settings changes the theme.
        .tint(appModel.theme.accent)
        .preferredColorScheme(appModel.theme.isLight ? .light : .dark)
        .onAppear {
            appModel.gameCenter.authenticate()
            #if DEBUG
            // Debug hooks for unattended simulator runs: jump to a route for
            // screenshot capture, or into endless for the auto-player.
            if let themeID = ProcessInfo.processInfo.environment["GRAVITILE_THEME"] {
                var settings = appModel.settings
                settings.themeID = themeID
                appModel.settings = settings
            }
            switch ProcessInfo.processInfo.environment["GRAVITILE_ROUTE"] {
            case "endless": path = [.endless]
            case "zen": path = [.zen]
            case "sprint": path = [.sprint]
            case "math": path = [.math]
            case "daily": path = [.daily]
            case "stats": path = [.stats]
            case "settings": path = [.settings]
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
        case .math:
            GameScreen(
                game: appModel.mathGame(),
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
