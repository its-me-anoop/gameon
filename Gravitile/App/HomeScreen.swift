import SwiftUI
import GravitileKit

struct HomeScreen: View {
    @Environment(AppModel.self) private var appModel

    var body: some View {
        ZStack {
            Theme.bgDeep.ignoresSafeArea()

            VStack(alignment: .leading, spacing: 0) {
                // Hero — deliberately asymmetric, left-aligned.
                VStack(alignment: .leading, spacing: 10) {
                    Text("Gravitile")
                        .font(Theme.display(40))
                        .foregroundStyle(Theme.textPrimary)
                    Text("Merge tiles. Gravity turns.\nChase the cascade.")
                        .font(.system(size: 16))
                        .lineSpacing(4)
                        .foregroundStyle(Theme.textSecondary)
                }
                .padding(.top, 28)

                Spacer(minLength: 24)

                VStack(spacing: 14) {
                    NavigationLink(value: Route.endless) {
                        HStack {
                            VStack(alignment: .leading, spacing: 3) {
                                Text(hasEndlessInProgress ? "Resume Game" : "Play Endless")
                                    .font(Theme.display(18, weight: .semibold))
                                    .foregroundStyle(Theme.bgDeep)
                                if appModel.persisted.bestEndlessScore > 0 {
                                    Text("Best \(appModel.persisted.bestEndlessScore)")
                                        .font(.system(size: 13, weight: .semibold))
                                        .foregroundStyle(Theme.bgDeep.opacity(0.7))
                                }
                            }
                            Spacer()
                            Image(systemName: "arrow.right")
                                .font(.system(size: 18, weight: .bold))
                                .foregroundStyle(Theme.bgDeep)
                        }
                        .padding(20)
                        .background(RoundedRectangle(cornerRadius: 20, style: .continuous).fill(Theme.accent))
                    }
                    .accessibilityIdentifier("playEndless")

                    NavigationLink(value: Route.daily) {
                        dailyCard
                    }
                    .accessibilityIdentifier("playDaily")

                    // Two compact chips, a deliberate visual step down from the
                    // full-width cards above — modes, not the main event.
                    HStack(spacing: 14) {
                        NavigationLink(value: Route.zen) {
                            modeChip(
                                title: "Zen",
                                subtitle: "No clock. No pressure.",
                                systemImage: "leaf.fill"
                            )
                        }
                        .accessibilityIdentifier("playZen")

                        NavigationLink(value: Route.sprint) {
                            modeChip(
                                title: "Sprint",
                                subtitle: "60 moves. Go big.",
                                systemImage: "bolt.fill"
                            )
                        }
                        .accessibilityIdentifier("playSprint")
                    }
                }

                Spacer(minLength: 24)

                HStack(spacing: 24) {
                    NavigationLink(value: Route.stats) {
                        Label("Stats", systemImage: "chart.bar")
                    }
                    .accessibilityIdentifier("statsLink")
                    NavigationLink(value: Route.settings) {
                        Label("Settings", systemImage: "gearshape")
                    }
                    .accessibilityIdentifier("settingsLink")
                }
                .font(.system(size: 15, weight: .semibold))
                .foregroundStyle(Theme.textSecondary)
                .padding(.bottom, 18)
            }
            .padding(.horizontal, 28)
            .frame(maxWidth: 620, maxHeight: 900)
        }
        // The floating Game Center pill belongs on the menu, never mid-game.
        .onAppear { appModel.gameCenter.setAccessPointActive(true) }
        .onDisappear { appModel.gameCenter.setAccessPointActive(false) }
    }

    private var hasEndlessInProgress: Bool {
        if let game = appModel.persisted.endlessGame, !game.isGameOver, game.moveCount > 0 { return true }
        return false
    }

    private var dailyCard: some View {
        HStack {
            VStack(alignment: .leading, spacing: 3) {
                HStack(spacing: 8) {
                    Text("Daily #\(appModel.todayPuzzleNumber)")
                        .font(Theme.display(18, weight: .semibold))
                        .foregroundStyle(Theme.textPrimary)
                    if appModel.persisted.streak.current > 0 {
                        Label("\(appModel.persisted.streak.current)", systemImage: "flame.fill")
                            .font(.system(size: 13, weight: .bold))
                            .foregroundStyle(Theme.accent)
                    }
                }
                Text(dailySubtitle)
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundStyle(Theme.textSecondary)
            }
            Spacer()
            Image(systemName: appModel.todayRecord == nil ? "calendar" : "checkmark.circle.fill")
                .font(.system(size: 22, weight: .semibold))
                .foregroundStyle(appModel.todayRecord == nil ? Theme.textSecondary : Theme.accent)
        }
        .padding(20)
        .background(
            RoundedRectangle(cornerRadius: 20, style: .continuous)
                .fill(Theme.bgBoard)
        )
    }

    private func modeChip(title: String, subtitle: String, systemImage: String) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(spacing: 7) {
                Image(systemName: systemImage)
                    .font(.system(size: 13, weight: .bold))
                    .foregroundStyle(Theme.accent)
                Text(title)
                    .font(Theme.display(15, weight: .semibold))
                    .foregroundStyle(Theme.textPrimary)
            }
            Text(subtitle)
                .font(.system(size: 12, weight: .medium))
                .foregroundStyle(Theme.textSecondary)
                .lineLimit(1)
                .minimumScaleFactor(0.8)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.horizontal, 16)
        .padding(.vertical, 14)
        .background(RoundedRectangle(cornerRadius: 16, style: .continuous).fill(Theme.cellWell))
    }

    private var dailySubtitle: String {
        if let record = appModel.todayRecord {
            return "Done — \(record.score) points. Archive inside."
        }
        return "Same board for everyone. 40 moves."
    }
}

enum Route: Hashable {
    case endless
    case zen
    case sprint
    case daily
    case dailyPuzzle(Int)
    case stats
    case settings
    case paywall
}
