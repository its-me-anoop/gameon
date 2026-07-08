import Foundation

/// Renders a game result as a compact, emoji-rich string for the share sheet —
/// the Wordle-style growth loop. Output is deterministic and locale-independent.
public enum ShareCard {
    /// Emoji per tile tier (2, 4, 8, …), heat progression matching the app theme.
    private static let tierBlocks = ["🟨", "🟧", "🟥", "🟪", "🟦", "🟩", "🟫", "⬛"]
    private static let maxBlocks = 8

    /// Clones taught the lesson Wordle learned late: the share must say where
    /// the game lives.
    public static let appStoreURL = "https://apps.apple.com/app/id6786840477"

    public static func text(for state: GameState) -> String {
        var movesUsed: Int?
        if case .daily = state.mode { movesUsed = state.moveCount }
        return text(
            mode: state.mode, score: state.score, bestTile: state.bestTile,
            cascadeCount: state.cascadeCount, movesUsed: movesUsed,
            deepestRound: state.bestCascadeRound
        )
    }

    /// Value-based variant so saved records can be shared without a live game.
    public static func text(
        mode: GameMode, score: Int, bestTile: Int, cascadeCount: Int,
        movesUsed: Int? = nil, deepestRound: Int = 0
    ) -> String {
        let title: String
        switch mode {
        case let .daily(puzzleNumber, budget):
            let moves = movesUsed.map { " · \($0)/\(budget)" } ?? ""
            title = "Gravitile #\(puzzleNumber) — \(formatted(score))\(moves)"
        case .endless:
            title = "Gravitile Endless — \(formatted(score))"
        case .zen:
            title = "Gravitile Zen — \(formatted(score))"
        case .sprint:
            title = "Gravitile Sprint — \(formatted(score))"
        case .math:
            title = "Gravitile Math Pop — \(formatted(score))"
        }

        let tiersReached = max(1, Int(log2(Double(max(2, bestTile)))))
        let blocks = (0..<min(tiersReached, maxBlocks))
            .map { tierBlocks[$0 % tierBlocks.count] }
            .joined()

        let cascadeNoun = cascadeCount == 1 ? "cascade" : "cascades"
        let depth = deepestRound >= 2 ? " · ×\(deepestRound) deep" : ""
        let footer = "🌀 \(cascadeCount) \(cascadeNoun)\(depth) · 🏆 \(bestTile)"

        return [title, blocks, footer, appStoreURL].joined(separator: "\n")
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
