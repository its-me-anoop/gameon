import SwiftUI
import WatchKit
import GravitileKit

/// The full 5×5 engine on the wrist: swipe to move, crossfade board updates
/// (the phone's phased choreography would be wasted at this size), digital
/// crown untouched. Zen pacing keeps sessions short and kind.
struct WatchGameView: View {
    @State private var store = WatchGameStore()
    @State private var confirmNewGame = false

    private let spacing: CGFloat = 2

    var body: some View {
        GeometryReader { proxy in
            VStack(spacing: 5) {
                header
                board(fitting: proxy.size)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            }
        }
        .padding(.horizontal, 2)
        .contentShape(Rectangle())
        .gesture(swipeGesture)
        .onLongPressGesture { confirmNewGame = true }
        .overlay { if store.game.isGameOver { gameOverOverlay } }
        .confirmationDialog("Start a new game?", isPresented: $confirmNewGame) {
            Button("New Game", role: .destructive) { startNewGame() }
            Button("Keep Playing", role: .cancel) {}
        }
        .background(Theme.bgDeep)
    }

    private var header: some View {
        HStack {
            Image(systemName: arrowName(for: store.game.gravity))
                .font(.system(size: 13, weight: .bold))
                .foregroundStyle(Theme.accent)
                .accessibilityLabel("Gravity \(store.game.gravity.rawValue)")
            Spacer()
            Text("\(store.game.score)")
                .font(.system(size: 17, weight: .heavy, design: .rounded))
                .foregroundStyle(Theme.textPrimary)
                .contentTransition(.numericText())
            Spacer()
            Text("\(store.bestScore)")
                .font(.system(size: 13, weight: .semibold, design: .rounded))
                .foregroundStyle(Theme.textSecondary)
                .accessibilityLabel("Best \(store.bestScore)")
        }
        .padding(.horizontal, 4)
    }

    private func board(fitting size: CGSize) -> some View {
        let side = min(size.width, size.height - 26)
        let cell = (side - spacing * CGFloat(Board.size - 1)) / CGFloat(Board.size)
        return VStack(spacing: spacing) {
            ForEach(0..<Board.size, id: \.self) { row in
                HStack(spacing: spacing) {
                    ForEach(0..<Board.size, id: \.self) { col in
                        let tile = store.game.board[Coordinate(row: row, col: col)]
                        RoundedRectangle(cornerRadius: 4, style: .continuous)
                            .fill(tile.map { Theme.tileColor(for: $0.value) } ?? Theme.cellWell)
                            .overlay {
                                if let tile {
                                    Text("\(tile.value)")
                                        .font(.system(size: cell * 0.42, weight: .heavy, design: .rounded))
                                        .minimumScaleFactor(0.5)
                                        .lineLimit(1)
                                        .foregroundStyle(Theme.tileTextColor(for: tile.value))
                                }
                            }
                            .frame(width: cell, height: cell)
                    }
                }
            }
        }
        .animation(.easeInOut(duration: 0.18), value: store.game.moveCount)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Game board, score \(store.game.score)")
    }

    private var gameOverOverlay: some View {
        VStack(spacing: 8) {
            Text("Board Locked")
                .font(.system(size: 16, weight: .bold))
                .foregroundStyle(Theme.textPrimary)
            Text("Score \(store.game.score)")
                .font(.system(size: 14, weight: .semibold, design: .rounded))
                .foregroundStyle(Theme.accent)
            Button("New Game") { startNewGame() }
                .font(.system(size: 14, weight: .semibold))
                .tint(Theme.accent)
        }
        .padding(14)
        .background(RoundedRectangle(cornerRadius: 12).fill(Theme.bgBoard.opacity(0.96)))
    }

    private var swipeGesture: some Gesture {
        DragGesture(minimumDistance: 12)
            .onEnded { gesture in
                let dx = gesture.translation.width
                let dy = gesture.translation.height
                let direction: Direction = abs(dx) > abs(dy)
                    ? (dx > 0 ? .right : .left)
                    : (dy > 0 ? .down : .up)
                guard let result = store.apply(direction) else { return }
                let merged = !result.slide.merges.isEmpty
                    || result.phases.contains { !$0.merges.isEmpty }
                WKInterfaceDevice.current().play(merged ? .click : .start)
                if store.game.isGameOver {
                    WKInterfaceDevice.current().play(.failure)
                }
            }
    }

    private func startNewGame() {
        store.newGame()
        WKInterfaceDevice.current().play(.success)
    }

    private func arrowName(for direction: Direction) -> String {
        switch direction {
        case .up: "arrow.up"
        case .down: "arrow.down"
        case .left: "arrow.left"
        case .right: "arrow.right"
        }
    }
}
