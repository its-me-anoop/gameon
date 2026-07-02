import SwiftUI
import GravitileKit

struct RootView: View {
    @Environment(AppModel.self) private var appModel

    var body: some View {
        NavigationStack {
            HomeScreen()
                .navigationDestination(for: Route.self) { route in
                    destination(for: route)
                }
        }
        .tint(Theme.accent)
        .preferredColorScheme(.dark)
    }

    @ViewBuilder
    private func destination(for route: Route) -> some View {
        switch route {
        case .endless:
            GameScreen(
                game: appModel.endlessGame(),
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
