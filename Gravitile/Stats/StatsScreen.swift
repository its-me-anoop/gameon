import SwiftUI
import GravitileKit

struct StatsScreen: View {
    @Environment(AppModel.self) private var appModel
    @State private var showLeaderboards = false
    @State private var achievements: [GameCenterService.AchievementStatus] = []

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 30) {
                recordsBlock

                lifetimeRow

                dailySection

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

                if !achievements.isEmpty {
                    achievementsSection
                }
            }
            .padding(.horizontal, 28)
            .padding(.top, 12)
            .padding(.bottom, 24)
        }
        .background(Theme.bgDeep)
        .navigationTitle("Stats")
        .task {
            achievements = await appModel.gameCenter.loadAchievements()
        }
        .sheet(isPresented: $showLeaderboards) {
            GameCenterView()
                .ignoresSafeArea()
        }
    }

    // MARK: - Records

    /// Six records, big tabular numerals, flat — the mode bests mirror the
    /// leaderboard segmentation.
    private var recordsBlock: some View {
        let persisted = appModel.persisted
        let records: [(value: String, label: String)] = [
            ("\(persisted.bestEndlessScore)", "Best score"),
            ("\(persisted.bestSprintScore)", "Sprint best"),
            ("\(persisted.bestZenTile)", "Zen best tile"),
            ("\(persisted.bestTileEver)", "Best tile ever"),
            (persisted.stats.bestCascadeRound >= 2 ? "×\(persisted.stats.bestCascadeRound)" : "—", "Deepest cascade"),
            ("\(persisted.streak.longest)", "Longest streak"),
        ]
        return VStack(alignment: .leading, spacing: 14) {
            sectionLabel("Records")
            LazyVGrid(
                columns: Array(repeating: GridItem(.flexible(), alignment: .leading), count: 3),
                alignment: .leading, spacing: 18
            ) {
                ForEach(records, id: \.label) { record in
                    VStack(alignment: .leading, spacing: 2) {
                        Text(record.value)
                            .font(Theme.display(22, weight: .semibold))
                            .monospacedDigit()
                            .foregroundStyle(Theme.textPrimary)
                            .minimumScaleFactor(0.6)
                            .lineLimit(1)
                            .contentTransition(.numericText())
                        Text(record.label)
                            .font(.system(size: 12, weight: .semibold))
                            .foregroundStyle(Theme.textSecondary)
                    }
                }
            }
        }
    }

    private var lifetimeRow: some View {
        HStack(spacing: 0) {
            stat("\(appModel.persisted.stats.gamesPlayed)", "Games")
            stat("\(appModel.persisted.stats.totalCascades)", "Cascades")
            stat("\(appModel.persisted.stats.totalScore)", "Total score")
        }
    }

    private func stat(_ value: String, _ label: String) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(value)
                .font(Theme.display(17, weight: .semibold))
                .monospacedDigit()
                .foregroundStyle(Theme.textPrimary)
                .minimumScaleFactor(0.6)
                .lineLimit(1)
            Text(label)
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(Theme.textSecondary)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    // MARK: - Daily

    private var dailySection: some View {
        VStack(alignment: .leading, spacing: 18) {
            HStack(spacing: 14) {
                sectionLabel("Daily")
                Spacer()
                if appModel.persisted.streak.current > 0 {
                    Label("\(appModel.persisted.streak.current)", systemImage: "flame.fill")
                        .font(.system(size: 13, weight: .bold))
                        .monospacedDigit()
                        .foregroundStyle(Theme.accent)
                        .accessibilityLabel("Current streak \(appModel.persisted.streak.current)")
                }
            }

            if !appModel.persisted.dailyRecords.isEmpty {
                scoreDistribution
            }

            monthCalendar
        }
    }

    /// The player's own daily scores bucketed by 500s, today's bar in accent —
    /// Wordle's guess-distribution trick, translated.
    private var scoreDistribution: some View {
        let scores = appModel.persisted.dailyRecords.values.map(\.score)
        var counts = [Int](repeating: 0, count: 8)
        for score in scores { counts[min(7, score / 500)] += 1 }
        let peak = max(1, counts.max() ?? 1)
        let todayBucket = appModel.todayRecord.map { min(7, $0.score / 500) }

        return VStack(alignment: .leading, spacing: 6) {
            HStack(alignment: .bottom, spacing: 4) {
                ForEach(0..<8, id: \.self) { bucket in
                    VStack(spacing: 3) {
                        RoundedRectangle(cornerRadius: 3)
                            .fill(bucket == todayBucket ? Theme.accent : Theme.cellWell)
                            .frame(height: counts[bucket] == 0
                                   ? 3
                                   : max(8, CGFloat(counts[bucket]) / CGFloat(peak) * 56))
                        Text(bucket == 7 ? "3.5k+" : "\(bucket * 500 / 100)")
                            .font(.system(size: 9, weight: .semibold))
                            .monospacedDigit()
                            .foregroundStyle(Theme.textSecondary)
                    }
                    .frame(maxWidth: .infinity)
                }
            }
            .frame(height: 74, alignment: .bottom)
            Text("Daily scores (×100) — today in orange")
                .font(.system(size: 11, weight: .medium))
                .foregroundStyle(Theme.textSecondary)
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Distribution of your daily scores")
    }

    /// This month at a glance: filled = played, ring = clean (no undo).
    /// Past days route to the archive (a Plus perk), today to the puzzle.
    private var monthCalendar: some View {
        let today = appModel.todayPuzzleNumber
        let days = monthDays()
        let columns = Array(repeating: GridItem(.flexible(), spacing: 6), count: 7)

        return VStack(alignment: .leading, spacing: 8) {
            Text(Date.now, format: .dateTime.month(.wide))
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(Theme.textSecondary)
            LazyVGrid(columns: columns, spacing: 6) {
                ForEach(days.indices, id: \.self) { index in
                    if let day = days[index] {
                        calendarCell(day: day, todayPuzzle: today)
                    } else {
                        Color.clear.frame(height: 34)
                    }
                }
            }
        }
    }

    @ViewBuilder
    private func calendarCell(day: (number: Int, puzzle: Int), todayPuzzle: Int) -> some View {
        let record = appModel.persisted.dailyRecords[day.puzzle]
        let isToday = day.puzzle == todayPuzzle
        let isFuture = day.puzzle > todayPuzzle
        let clean = record?.usedUndo == false

        let label = Text("\(day.number)")
            .font(.system(size: 13, weight: isToday ? .heavy : .semibold))
            .monospacedDigit()
            .foregroundStyle(
                record != nil ? Theme.bgDeep : (isFuture ? Theme.textSecondary.opacity(0.35) : Theme.textPrimary)
            )
            .frame(maxWidth: .infinity)
            .frame(height: 34)
            .background(
                RoundedRectangle(cornerRadius: 8, style: .continuous)
                    .fill(record != nil ? Theme.accent : Theme.cellWell.opacity(isFuture ? 0.4 : 1))
            )
            .overlay {
                if clean {
                    RoundedRectangle(cornerRadius: 8, style: .continuous)
                        .strokeBorder(Theme.textPrimary, lineWidth: 1.5)
                }
                if isToday && record == nil {
                    RoundedRectangle(cornerRadius: 8, style: .continuous)
                        .strokeBorder(Theme.accent, lineWidth: 1.5)
                }
            }

        if isFuture {
            label
        } else if isToday {
            NavigationLink(value: Route.dailyPuzzle(day.puzzle)) { label }
        } else if appModel.isPlus {
            NavigationLink(value: Route.dailyPuzzle(day.puzzle)) { label }
        } else {
            NavigationLink(value: Route.paywall) { label }
        }
    }

    /// Day-number/puzzle-number pairs for the current UTC month, padded with
    /// leading nils to align the first weekday.
    private func monthDays() -> [(number: Int, puzzle: Int)?] {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(identifier: "UTC")!
        let now = Date.now
        guard let interval = calendar.dateInterval(of: .month, for: now),
              let dayCount = calendar.range(of: .day, in: .month, for: now)?.count
        else { return [] }
        let firstWeekday = calendar.component(.weekday, from: interval.start)
        let leading = (firstWeekday - calendar.firstWeekday + 7) % 7
        var cells: [(Int, Int)?] = Array(repeating: nil, count: leading)
        for day in 1...dayCount {
            guard let date = calendar.date(byAdding: .day, value: day - 1, to: interval.start) else { continue }
            cells.append((day, DailySeed.puzzleNumber(for: date)))
        }
        return cells
    }

    // MARK: - Achievements

    /// Earned achievements are one tap from a brag; locked ones tease the goal.
    private var achievementsSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            sectionLabel("Achievements")
            ForEach(achievements) { achievement in
                HStack(spacing: 12) {
                    Image(systemName: achievement.earned ? "checkmark.seal.fill" : "seal")
                        .font(.system(size: 20, weight: .semibold))
                        .foregroundStyle(achievement.earned ? Theme.accent : Theme.textSecondary)
                        .frame(width: 26)
                    VStack(alignment: .leading, spacing: 2) {
                        Text(achievement.title)
                            .font(.system(size: 15, weight: .semibold))
                            .foregroundStyle(achievement.earned ? Theme.textPrimary : Theme.textSecondary)
                        Text(achievement.detail)
                            .font(.system(size: 12))
                            .foregroundStyle(Theme.textSecondary)
                    }
                    Spacer()
                    if achievement.earned {
                        ShareLink(item: achievementShareText(achievement)) {
                            Image(systemName: "square.and.arrow.up")
                                .font(.system(size: 15, weight: .semibold))
                                .foregroundStyle(Theme.accent)
                        }
                    }
                }
            }
        }
    }

    private func achievementShareText(_ achievement: GameCenterService.AchievementStatus) -> String {
        "🏆 \(achievement.title) — earned in Gravitile, the tumbling merge puzzle.\n\(ShareCard.appStoreURL)"
    }

    private func sectionLabel(_ text: String) -> some View {
        Text(text)
            .font(.system(size: 14, weight: .semibold))
            .foregroundStyle(Theme.textSecondary)
    }
}
