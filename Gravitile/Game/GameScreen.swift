import SwiftUI
import GravitileKit

struct GameScreen: View {
    @Environment(AppModel.self) private var appModel
    @Environment(\.dismiss) private var dismiss
    @Environment(\.scenePhase) private var scenePhase
    @State private var viewModel: GameViewModel
    @State private var showGameOver = false
    @State private var shareText: String?
    @State private var gameEndRecorded = false
    /// Personal best when this screen appeared — crossing it mid-game earns
    /// a one-time sting; nil until first wired.
    @State private var sessionStartBest: Int?
    @State private var newBestCelebrated = false

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
                .overlay {
                    if let value = viewModel.celebrationValue {
                        MilestoneCelebration(value: value)
                            .transition(.scale(scale: 0.6).combined(with: .opacity))
                            .allowsHitTesting(false)
                    }
                }

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
                    isNewBest: newBestCelebrated,
                    onNewGame: gameOverPrimaryAction,
                    onShare: { shareText = ShareCard.text(for: viewModel.game) }
                )
                .transition(.scale(scale: 0.9).combined(with: .opacity))
            }
        }
        // The interactive pop gesture starts at the same left edge as game
        // swipes and kept eating them mid-game — hide the system back button
        // (which also disables the gesture) and own the exit via the header.
        .navigationBarBackButtonHidden(true)
        .toolbar(.hidden, for: .navigationBar)
        .onAppear {
            wireCallbacks()
            appModel.sounds.startMusic()
        }
        .onDisappear {
            appModel.sounds.stopMusic()
        }
        // Ambient audio doesn't resume itself after backgrounding.
        .onChange(of: scenePhase) { _, phase in
            if phase == .active { appModel.sounds.syncMusic() }
        }
        .sheet(item: $shareText) { text in
            ShareSheet(text: text)
                .presentationDetents([.medium])
        }
    }

    private func wireCallbacks() {
        if sessionStartBest == nil { sessionStartBest = storedBest }
        viewModel.onMerge = { round in
            appModel.haptics.merge(round: round)
            appModel.sounds.merge(round: max(1, round))
        }
        viewModel.onRotation = {
            appModel.haptics.rotationTick()
            appModel.sounds.whoosh()
        }
        viewModel.onLanding = {
            appModel.haptics.landing()
            appModel.sounds.land()
        }
        viewModel.onMilestone = { _ in
            appModel.haptics.milestone()
            appModel.sounds.milestone()
        }
        viewModel.onMoveCommitted = { game in
            appModel.checkpoint(game)
            celebrateNewBestIfCrossed(game)
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
        HStack(alignment: .center, spacing: 14) {
            Button {
                dismiss()
            } label: {
                Image(systemName: "chevron.left")
                    .font(.system(size: 15, weight: .bold))
                    .foregroundStyle(Theme.textPrimary)
                    .frame(width: 36, height: 36)
                    .background(Circle().fill(Theme.cellWell))
            }
            .accessibilityLabel("Back")
            .accessibilityIdentifier("exitGame")
            Text(modeTitle)
                .font(Theme.display(20))
                .foregroundStyle(Theme.textPrimary)
            Spacer()
            HStack(spacing: 22) {
                ScoreBadge(title: "Score", value: viewModel.game.score, emphasized: true)
                    .accessibilityIdentifier("score")
                    .overlay(alignment: .bottom) {
                        ForEach(viewModel.scorePops) { pop in
                            ScorePopView(pop: pop)
                        }
                    }
                ScoreBadge(title: bestBadgeTitle, value: bestBadgeValue)
            }
        }
    }

    private var modeTitle: String {
        switch viewModel.game.mode {
        case .endless: "Gravitile"
        case .zen: "Zen"
        case .sprint: "Sprint"
        case .daily: "Daily #\(dailyNumber)"
        }
    }

    /// Zen has no meaningful score ceiling (games can run forever), so its
    /// personal best is the tile chase; every other mode competes on score.
    private var bestBadgeTitle: String {
        if case .zen = viewModel.game.mode { return "Best Tile" }
        return "Best"
    }

    private var bestBadgeValue: Int {
        switch viewModel.game.mode {
        case .endless: max(appModel.persisted.bestEndlessScore, viewModel.game.score)
        case .zen: max(appModel.persisted.bestZenTile, viewModel.game.bestTile)
        case .sprint: max(appModel.persisted.bestSprintScore, viewModel.game.score)
        case .daily: max(appModel.persisted.dailyRecords[dailyNumber]?.score ?? 0, viewModel.game.score)
        }
    }

    /// Stored bests only (no live-game max) — the yardstick for "new best".
    private var storedBest: Int {
        switch viewModel.game.mode {
        case .endless: appModel.persisted.bestEndlessScore
        case .zen: appModel.persisted.bestZenTile
        case .sprint: appModel.persisted.bestSprintScore
        case .daily: appModel.persisted.dailyRecords[dailyNumber]?.score ?? 0
        }
    }

    private func celebrateNewBestIfCrossed(_ game: GameState) {
        guard !newBestCelebrated, let start = sessionStartBest, start > 0 else { return }
        let comparable: Int
        if case .zen = game.mode { comparable = game.bestTile } else { comparable = game.score }
        guard comparable > start else { return }
        newBestCelebrated = true
        appModel.haptics.newBest()
        appModel.sounds.newBest()
    }

    private var dailyNumber: Int {
        if case let .daily(number, _) = viewModel.game.mode { return number }
        return 0
    }

    private var controls: some View {
        HStack {
            Button {
                appModel.sounds.tap()
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
                    appModel.sounds.tap()
                    startNewGame()
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
            startNewGame()
        }
    }

    private func startNewGame() {
        finishGameIfOver()
        withAnimation(.spring(duration: 0.3)) { showGameOver = false }
        gameEndRecorded = false
        viewModel.replace(game: appModel.newGame(like: viewModel.game.mode))
        // The bar to beat may have just moved (recordGameEnd above).
        sessionStartBest = storedBest
        newBestCelebrated = false
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

/// Floating "+N" above the score — rises and fades, louder for deep cascades.
struct ScorePopView: View {
    let pop: ScorePop
    @State private var risen = false

    var body: some View {
        Text("+\(pop.points)")
            .font(Theme.display(pop.round >= 2 ? 17 : 13, weight: .bold))
            .foregroundStyle(pop.round >= 2 ? Theme.accent : Theme.textPrimary)
            .offset(y: risen ? -34 : -6)
            .opacity(risen ? 0 : 1)
            .onAppear { withAnimation(.easeOut(duration: 0.85)) { risen = true } }
            .allowsHitTesting(false)
    }
}

/// Full-board flourish for the first 256/512/… of a game.
struct MilestoneCelebration: View {
    let value: Int

    var body: some View {
        ZStack {
            ParticleBurstView(round: 5, milestone: true)
                .frame(width: 260, height: 260)
            Text("\(value)!")
                .font(Theme.display(46))
                .foregroundStyle(Theme.accent)
                .shadow(color: .black.opacity(0.55), radius: 14)
        }
        .accessibilityLabel("Milestone: reached tile \(value)")
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
