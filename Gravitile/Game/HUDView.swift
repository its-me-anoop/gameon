import SwiftUI
import GravitileKit

/// Shows current and next gravity so the tumble is never a surprise.
struct GravityCompass: View {
    let current: Direction
    let next: Direction

    var body: some View {
        HStack(spacing: 10) {
            Image(systemName: arrowName(for: current))
                .font(.system(size: 17, weight: .bold))
                .foregroundStyle(Theme.accent)
            Image(systemName: "chevron.right")
                .font(.system(size: 11, weight: .semibold))
                .foregroundStyle(Theme.textSecondary)
            Image(systemName: arrowName(for: next))
                .font(.system(size: 14, weight: .semibold))
                .foregroundStyle(Theme.textSecondary)
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 8)
        .background(Capsule().fill(Theme.bgBoard))
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Gravity \(label(for: current)), next \(label(for: next))")
        .accessibilityIdentifier("gravityCompass")
    }

    private func arrowName(for direction: Direction) -> String {
        switch direction {
        case .up: "arrow.up"
        case .down: "arrow.down"
        case .left: "arrow.left"
        case .right: "arrow.right"
        }
    }

    private func label(for direction: Direction) -> String { direction.rawValue }
}

struct ScoreBadge: View {
    let title: String
    let value: Int
    var emphasized = false

    var body: some View {
        VStack(spacing: 2) {
            Text(title.uppercased())
                .font(.system(size: 11, weight: .semibold))
                .tracking(1.2)
                .foregroundStyle(Theme.textSecondary)
            Text("\(value)")
                .font(Theme.display(emphasized ? 24 : 17, weight: .bold))
                .foregroundStyle(emphasized ? Theme.accent : Theme.textPrimary)
                .contentTransition(.numericText())
                .monospacedDigit()
        }
        .accessibilityElement(children: .combine)
    }
}

struct GameOverOverlay: View {
    let game: GameState
    var onNewGame: () -> Void
    var onShare: () -> Void

    var body: some View {
        VStack(spacing: 20) {
            Text(titleText)
                .font(Theme.display(26))
                .foregroundStyle(Theme.textPrimary)
            VStack(spacing: 6) {
                Text("Score \(game.score)")
                    .font(Theme.display(20, weight: .semibold))
                    .foregroundStyle(Theme.accent)
                Text("Best tile \(game.bestTile) · \(game.cascadeCount) cascades")
                    .font(.subheadline)
                    .foregroundStyle(Theme.textSecondary)
            }
            HStack(spacing: 12) {
                Button(action: onShare) {
                    Label("Share", systemImage: "square.and.arrow.up")
                        .frame(maxWidth: .infinity)
                }
                .buttonStyle(SecondaryButtonStyle())
                Button(action: onNewGame) {
                    Text(isDaily ? "Back to Today" : "New Game")
                        .frame(maxWidth: .infinity)
                }
                .buttonStyle(PrimaryButtonStyle())
                .accessibilityIdentifier("newGameButton")
            }
        }
        .padding(28)
        .frame(maxWidth: 340)
        .background(
            RoundedRectangle(cornerRadius: 24, style: .continuous)
                .fill(Theme.bgBoard)
                .shadow(color: .black.opacity(0.5), radius: 30, y: 10)
        )
        .accessibilityIdentifier("gameOverOverlay")
    }

    private var isDaily: Bool {
        if case .daily = game.mode { return true }
        return false
    }

    private var titleText: String {
        if case .daily = game.mode { return "Daily Done" }
        return "Board Locked"
    }
}

struct PrimaryButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 16, weight: .bold))
            .foregroundStyle(Theme.bgDeep)
            .padding(.vertical, 13)
            .padding(.horizontal, 18)
            .background(Capsule().fill(Theme.accent))
            .opacity(configuration.isPressed ? 0.75 : 1)
    }
}

struct SecondaryButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 16, weight: .semibold))
            .foregroundStyle(Theme.textPrimary)
            .padding(.vertical, 13)
            .padding(.horizontal, 18)
            .background(Capsule().fill(Theme.cellWell))
            .opacity(configuration.isPressed ? 0.75 : 1)
    }
}
