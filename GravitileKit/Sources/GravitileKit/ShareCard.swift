import Foundation

/// Renders a game result as a compact, emoji-rich string for the share sheet —
/// the Wordle-style growth loop. Output is deterministic and locale-independent.
public enum ShareCard {
    /// Emoji per tile tier (2, 4, 8, …), heat progression matching the app theme.
    private static let tierBlocks = ["🟨", "🟧", "🟥", "🟪", "🟦", "🟩", "🟫", "⬛"]
    private static let maxBlocks = 8

    public static func text(for state: GameState) -> String {
        text(mode: state.mode, score: state.score, bestTile: state.bestTile, cascadeCount: state.cascadeCount)
    }

    /// Value-based variant so saved records can be shared without a live game.
    public static func text(mode: GameMode, score: Int, bestTile: Int, cascadeCount: Int) -> String {
        let title: String
        switch mode {
        case let .daily(puzzleNumber, _):
            title = "Gravitile #\(puzzleNumber) — \(formatted(score))"
        case .endless:
            title = "Gravitile Endless — \(formatted(score))"
        case .zen:
            title = "Gravitile Zen — \(formatted(score))"
        case .sprint:
            title = "Gravitile Sprint — \(formatted(score))"
        }

        let tiersReached = max(1, Int(log2(Double(max(2, bestTile)))))
        let blocks = (0..<min(tiersReached, maxBlocks))
            .map { tierBlocks[$0 % tierBlocks.count] }
            .joined()

        let cascadeNoun = cascadeCount == 1 ? "cascade" : "cascades"
        let footer = "🌀 \(cascadeCount) \(cascadeNoun) · 🏆 \(bestTile)"

        return [title, blocks, footer].joined(separator: "\n")
    }

    /// Comma-grouped digits, independent of device locale so shared cards
    /// look identical worldwide. Scores are never negative.
    private static func formatted(_ number: Int) -> String {
        let digits = String(number)
        var result = ""
        for (index, character) in digits.enumerated() {
            if index > 0 && (digits.count - index).isMultiple(of: 3) {
                result.append(",")
            }
            result.append(character)
        }
        return result
    }
}
