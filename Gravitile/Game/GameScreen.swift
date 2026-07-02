import SwiftUI
import GravitileKit

struct GameScreen: View {
    @Environment(AppModel.self) private var appModel
    @Environment(\.dismiss) private var dismiss
    @State private var viewModel: GameViewModel
    @State private var showGameOver = false
    @State private var shareText: String?
    @State private var gameEndRecorded = false

    init(game: GameState, freeUndoLimit: Int) {
        _viewModel = State(initialValue: GameViewModel(game: game, freeUndoLimit: freeUndoLimit))
    }

    private var isDaily: Bool {
        if case .daily = viewModel.game.mode { return true }
        return false
    }

    private var showTutorial: Bool {
        #if DEBUG
        if ProcessInfo.processInfo.environment["GRAVITILE_AUTOPLAY"] == "1" { return false }
        #endif
        return !isDaily && !appModel.settings.hasSeenTutorial && !showGameOver
    }

    var body: some View {
        ZStack {
            Theme.bgDeep.ignoresSafeArea()

            VStack(spacing: 0) {
                header
                    .padding(.horizontal, 24)
                    .padding(.top, 8)

                Spacer(minLength: 16)

                GravityCompass(current: viewModel.game.gravity, next: viewModel.game.gravity.rotatedClockwise)
                    .padding(.bottom, 14)

                BoardView(viewModel: viewModel) { direction in
                    viewModel.handleSwipe(direction)
                }
                .padding(.horizontal, 16)

                if let remaining = viewModel.game.movesRemaining {
                    movesIndicator(remaining: remaining)
                        .padding(.top, 18)
                }

                Spacer(minLength: 16)

                controls
                    .padding(.horizontal, 24)
                    .padding(.bottom, 12)
            }
            // iPad: keep the board thumb-scale instead of wall-sized.
            .frame(maxWidth: 620, maxHeight: 980)

            if showTutorial {
                TutorialOverlay(game: viewModel.game) {
                    var settings = appModel.settings
                    settings.hasSeenTutorial = true
                    appModel.settings = settings
                }
            }

            if showGameOver {
                Color.black.opacity(0.55).ignoresSafeArea()
                    .transition(.opacity)
                GameOverOverlay(
                    game: viewModel.game,
                    onNewGame: gameOverPrimaryAction,
                    onShare: { shareText = ShareCard.text(for: viewModel.game) }
                )
                .transition(.scale(scale: 0.9).combined(with: .opacity))
            }
        }
        .navigationBarBackButtonHidden(false)
        .onAppear(perform: wireCallbacks)
        .sheet(item: $shareText) { text in
            ShareSheet(text: text)
                .presentationDetents([.medium])
        }
    }

    private func wireCallbacks() {
        viewModel.onMerge = { round in
            appModel.haptics.merge(round: round)
            appModel.sounds.merge(round: max(1, round))
        }
        viewModel.onMoveCommitted = { game in
            appModel.checkpoint(game)
        }
        viewModel.onGameOver = {
            finishGame()
            withAnimation(.spring(duration: 0.35)) { showGameOver = true }
        }
        if viewModel.game.isGameOver {
            showGameOver = true
        }
        startAutoPlayerIfRequested()
    }

    private func finishGame() {
        guard !gameEndRecorded else { return }
        gameEndRecorded = true
        appModel.recordGameEnd(viewModel.game)
        appModel.haptics.gameOver()
        if isDaily {
            appModel.sounds.fanfare()
        } else {
            appModel.sounds.gameOver()
        }
    }

    private var header: some View {
        HStack(alignment: .center) {
            Text(isDaily ? "Daily #\(dailyNumber)" : "Gravitile")
                .font(Theme.display(20))
                .foregroundStyle(Theme.textPrimary)
            Spacer()
            HStack(spacing: 22) {
                ScoreBadge(title: "Score", value: viewModel.game.score, emphasized: true)
                    .accessibilityIdentifier("score")
                ScoreBadge(
                    title: "Best",
                    value: isDaily
                        ? max(appModel.persisted.dailyRecords[dailyNumber]?.score ?? 0, viewModel.game.score)
                        : max(appModel.persisted.bestEndlessScore, viewModel.game.score)
                )
            }
        }
    }

    private var dailyNumber: Int {
        if case let .daily(number, _) = viewModel.game.mode { return number }
        return 0
    }

    private var controls: some View {
        HStack {
            Button {
                viewModel.undoTapped()
            } label: {
                Label(
                    undoLabel,
                    systemImage: "arrow.uturn.backward"
                )
            }
            .buttonStyle(SecondaryButtonStyle())
            .disabled(!viewModel.canUndo)
            .opacity(viewModel.canUndo ? 1 : 0.4)
            .accessibilityIdentifier("undoButton")

            Spacer()

            if viewModel.lastCascadeHighlight >= 2 {
                Text("CASCADE ×\(viewModel.lastCascadeHighlight)")
                    .font(.system(size: 13, weight: .heavy))
                    .tracking(1.5)
                    .foregroundStyle(Theme.accent)
                    .transition(.scale.combined(with: .opacity))
            }

            Spacer()

            if !isDaily {
                Button {
                    startNewEndlessGame()
                } label: {
                    Label("New", systemImage: "plus")
                }
                .buttonStyle(SecondaryButtonStyle())
                .accessibilityIdentifier("newGameButton")
            }
        }
    }

    /// Plus users have unlimited undo — no point showing a 19-digit counter.
    private var undoLabel: String {
        if viewModel.freeUndoLimit == Int.max { return "Undo" }
        return viewModel.undosRemaining > 0 ? "Undo (\(viewModel.undosRemaining))" : "Undo"
    }

    private func movesIndicator(remaining: Int) -> some View {
        HStack(spacing: 8) {
            Image(systemName: "hourglass")
                .foregroundStyle(remaining <= 5 ? Theme.accent : Theme.textSecondary)
            Text("\(remaining) moves left")
                .font(.system(size: 15, weight: .semibold))
                .foregroundStyle(remaining <= 5 ? Theme.accent : Theme.textSecondary)
                .contentTransition(.numericText())
        }
        .accessibilityIdentifier("movesRemaining")
    }

    private func gameOverPrimaryAction() {
        if isDaily {
            dismiss()
        } else {
            startNewEndlessGame()
        }
    }

    private func startNewEndlessGame() {
        finishGameIfOver()
        withAnimation(.spring(duration: 0.3)) { showGameOver = false }
        gameEndRecorded = false
        viewModel.replace(game: appModel.newEndlessGame())
    }

    private func finishGameIfOver() {
        if viewModel.game.isGameOver { finishGame() }
    }

    /// Debug soak-testing: GRAVITILE_AUTOPLAY=1 plays random legal moves so
    /// animations and game-over flow can be exercised hands-free on simulator.
    private func startAutoPlayerIfRequested() {
        #if DEBUG
        guard ProcessInfo.processInfo.environment["GRAVITILE_AUTOPLAY"] == "1" else { return }
        Task { @MainActor in
            while !viewModel.game.isGameOver {
                try? await Task.sleep(for: .seconds(0.6))
                guard !viewModel.isAnimating else { continue }
                if let direction = Direction.allCases.shuffled().first(where: { direction in
                    var copy = viewModel.game
                    return copy.applyMove(direction) != nil
                }) {
                    viewModel.handleSwipe(direction)
                }
            }
        }
        #endif
    }
}

extension String: @retroactive Identifiable {
    public var id: String { self }
}

struct ShareSheet: UIViewControllerRepresentable {
    let text: String

    func makeUIViewController(context: Context) -> UIActivityViewController {
        UIActivityViewController(activityItems: [text], applicationActivities: nil)
    }

    func updateUIViewController(_ controller: UIActivityViewController, context: Context) {}
}
