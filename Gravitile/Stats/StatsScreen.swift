import SwiftUI
import GravitileKit

struct StatsScreen: View {
    @Environment(AppModel.self) private var appModel
    @State private var showLeaderboards = false

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 28) {
                // Headline numbers — different scales on purpose.
                VStack(alignment: .leading, spacing: 4) {
                    Text("\(appModel.persisted.bestEndlessScore)")
                        .font(Theme.display(44))
                        .foregroundStyle(Theme.accent)
                        .minimumScaleFactor(0.5)
                        .lineLimit(1)
                    Text("Best endless score")
                        .font(.system(size: 14, weight: .semibold))
                        .foregroundStyle(Theme.textSecondary)
                }

                HStack(spacing: 0) {
                    stat("\(appModel.persisted.stats.gamesPlayed)", "Games")
                    stat("\(appModel.persisted.bestTileEver)", "Best tile")
                    stat("\(appModel.persisted.stats.totalCascades)", "Cascades")
                }

                HStack(spacing: 0) {
                    stat("\(appModel.persisted.bestSprintScore)", "Sprint best")
                    stat("\(appModel.persisted.bestZenTile)", "Zen best tile")
                    stat("\(appModel.persisted.stats.totalScore)", "Total score")
                }

                dailyStrip

                streakSection

                if appModel.gameCenter.isAuthenticated {
                    Button {
                        appModel.sounds.tap()
                        showLeaderboards = true
                    } label: {
                        Label("Game Center Leaderboards", systemImage: "trophy.fill")
                            .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(SecondaryButtonStyle())
                    .accessibilityIdentifier("leaderboardsButton")
                }
            }
            .padding(.horizontal, 28)
            .padding(.top, 12)
            .padding(.bottom, 24)
        }
        .background(Theme.bgDeep)
        .navigationTitle("Stats")
        .sheet(isPresented: $showLeaderboards) {
            GameCenterView()
                .ignoresSafeArea()
        }
    }

    private func stat(_ value: String, _ label: String) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(value)
                .font(Theme.display(20, weight: .semibold))
                .foregroundStyle(Theme.textPrimary)
                .minimumScaleFactor(0.6)
                .lineLimit(1)
            Text(label)
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(Theme.textSecondary)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    /// Last 30 dailies as a mini bar chart — real data, height ∝ score.
    private var dailyStrip: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text("Last 30 dailies")
                .font(.system(size: 14, weight: .semibold))
                .foregroundStyle(Theme.textSecondary)
            let today = appModel.todayPuzzleNumber
            let range = Array(max(1, today - 29)...today)
            let maxScore = max(1, range.compactMap { appModel.persisted.dailyRecords[$0]?.score }.max() ?? 1)
            HStack(alignment: .bottom, spacing: 3) {
                ForEach(range, id: \.self) { number in
                    let score = appModel.persisted.dailyRecords[number]?.score
                    RoundedRectangle(cornerRadius: 2)
                        .fill(score == nil ? Theme.cellWell : Theme.accent)
                        .frame(height: score.map { max(8, CGFloat($0) / CGFloat(maxScore) * 64) } ?? 4)
                        .frame(maxWidth: .infinity)
                }
            }
            .frame(height: 68, alignment: .bottom)
        }
    }

    private var streakSection: some View {
        HStack(spacing: 0) {
            stat("\(appModel.persisted.streak.current)", "Current streak")
            stat("\(appModel.persisted.streak.longest)", "Longest streak")
            stat("\(appModel.persisted.dailyRecords.count)", "Dailies played")
        }
    }
}
