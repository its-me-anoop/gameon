import SwiftUI
import GravitileKit

/// Today's puzzle plus the archive. Past puzzles are a Plus perk; free users
/// see them locked as a paywall tease.
struct DailyScreen: View {
    @Environment(AppModel.self) private var appModel
    @State private var sharePayload: SharePayload?

    var body: some View {
        List {
            Section {
                todayCard
                    .listRowBackground(Color.clear)
                    .listRowInsets(EdgeInsets())
            }

            Section("Streak") {
                HStack(spacing: 18) {
                    streakStat(value: appModel.persisted.streak.current, label: "Current", flame: true)
                    streakStat(value: appModel.persisted.streak.longest, label: "Longest", flame: false)
                    Spacer()
                }
                .listRowBackground(Theme.bgBoard)
            }

            Section("Archive") {
                ForEach(archiveNumbers, id: \.self) { number in
                    archiveRow(number)
                        .listRowBackground(Theme.bgBoard)
                }
            }
        }
        .scrollContentBackground(.hidden)
        .background(Theme.bgDeep)
        .navigationTitle("Daily")
        .sheet(item: $sharePayload) { payload in
            ShareSheet(items: payload.items).presentationDetents([.medium])
        }
    }

    private var todayCard: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text("Daily #\(appModel.todayPuzzleNumber)")
                .font(Theme.display(24))
                .foregroundStyle(Theme.textPrimary)

            if let record = appModel.todayRecord {
                VStack(alignment: .leading, spacing: 6) {
                    Text("\(record.score) points · best tile \(record.bestTile)")
                        .font(.system(size: 15, weight: .semibold))
                        .foregroundStyle(Theme.accent)
                    Text("Next puzzle at midnight UTC")
                        .font(.system(size: 13))
                        .foregroundStyle(Theme.textSecondary)
                }
                Button {
                    sharePayload = ShareCardRenderer.payload(for: record)
                } label: {
                    Label("Share result", systemImage: "square.and.arrow.up")
                        .frame(maxWidth: .infinity)
                }
                .buttonStyle(PrimaryButtonStyle())
            } else {
                Text("Everyone plays the same board. 40 moves — make them count.")
                    .font(.system(size: 14))
                    .foregroundStyle(Theme.textSecondary)
                NavigationLink(value: Route.dailyPuzzle(appModel.todayPuzzleNumber)) {
                    Text("Play Today")
                        .frame(maxWidth: .infinity)
                }
                .buttonStyle(PrimaryButtonStyle())
                .accessibilityIdentifier("playToday")
            }
        }
        .padding(20)
        .background(RoundedRectangle(cornerRadius: 20, style: .continuous).fill(Theme.bgBoard))
    }

    private func streakStat(value: Int, label: String, flame: Bool) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            HStack(spacing: 4) {
                if flame {
                    Image(systemName: "flame.fill")
                        .font(.system(size: 14))
                        .foregroundStyle(Theme.accent)
                }
                Text("\(value)")
                    .font(Theme.display(20, weight: .semibold))
                    .foregroundStyle(Theme.textPrimary)
            }
            Text(label)
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(Theme.textSecondary)
        }
    }

    /// Most recent 14 past puzzles (newest first). Puzzle #1 anchors the range.
    private var archiveNumbers: [Int] {
        let today = appModel.todayPuzzleNumber
        return stride(from: today - 1, through: max(1, today - 14), by: -1).map { $0 }
    }

    @ViewBuilder
    private func archiveRow(_ number: Int) -> some View {
        let record = appModel.persisted.dailyRecords[number]
        if appModel.isPlus {
            NavigationLink(value: Route.dailyPuzzle(number)) {
                archiveLabel(number: number, record: record, locked: false)
            }
        } else {
            NavigationLink(value: Route.paywall) {
                archiveLabel(number: number, record: record, locked: true)
            }
        }
    }

    private func archiveLabel(number: Int, record: DailyRecord?, locked: Bool) -> some View {
        HStack {
            Text("#\(number)")
                .font(.system(size: 15, weight: .bold))
                .foregroundStyle(Theme.textPrimary)
            Text(DailySeed.date(forPuzzleNumber: number), format: .dateTime.day().month())
                .font(.system(size: 13))
                .foregroundStyle(Theme.textSecondary)
            Spacer()
            if let record {
                Text("\(record.score)")
                    .font(.system(size: 14, weight: .semibold))
                    .foregroundStyle(Theme.accent)
            }
            if locked {
                Image(systemName: "lock.fill")
                    .font(.system(size: 12))
                    .foregroundStyle(Theme.textSecondary)
            }
        }
    }

}
