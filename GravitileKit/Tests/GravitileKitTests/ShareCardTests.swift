import Testing
@testable import GravitileKit

@Suite struct ShareCardTests {
    @Test func dailyCardShowsPuzzleNumberScoreMovesAndProgression() {
        var board = Board()
        board[Coordinate(row: 4, col: 0)] = Tile(id: 1, value: 64)
        var game = GameState(testBoard: board, gravity: .down, mode: .daily(puzzleNumber: 12, moveBudget: 40), seed: 1)
        game.setTestStats(score: 4320, cascadeCount: 7)
        let text = ShareCard.text(for: game)
        #expect(text == """
        Gravitile #12 — 4,320 · 0/40
        🟨🟧🟥🟪🟦🟩
        🌀 7 cascades · 🏆 64
        \(ShareCard.appStoreURL)
        """)
    }

    @Test func endlessCardOmitsPuzzleNumberAndMoves() {
        var board = Board()
        board[Coordinate(row: 4, col: 0)] = Tile(id: 1, value: 8)
        var game = GameState(testBoard: board, gravity: .down, mode: .endless, seed: 1)
        game.setTestStats(score: 96, cascadeCount: 1)
        let text = ShareCard.text(for: game)
        #expect(text == """
        Gravitile Endless — 96
        🟨🟧🟥
        🌀 1 cascade · 🏆 8
        \(ShareCard.appStoreURL)
        """)
    }

    @Test func deepCascadesEarnTheDepthMarker() {
        var board = Board()
        board[Coordinate(row: 4, col: 0)] = Tile(id: 1, value: 64)
        var game = GameState(testBoard: board, gravity: .down, mode: .endless, seed: 1)
        game.setTestStats(score: 500, cascadeCount: 4, bestCascadeRound: 3)
        let text = ShareCard.text(for: game)
        #expect(text.contains("🌀 4 cascades · ×3 deep · 🏆 64"))
    }

    @Test func progressionCapsAtEightBlocks() {
        var board = Board()
        board[Coordinate(row: 4, col: 0)] = Tile(id: 1, value: 2048) // tier 11
        var game = GameState(testBoard: board, gravity: .down, mode: .endless, seed: 1)
        game.setTestStats(score: 50000, cascadeCount: 0)
        let text = ShareCard.text(for: game)
        let progressionLine = text.split(separator: "\n")[1]
        #expect(progressionLine.count == 8)
        #expect(text.contains("50,000"))
        #expect(text.contains("🏆 2048"))
        #expect(text.contains("0 cascades"))
    }

    @Test func numberFormattingIsLocaleIndependent() {
        var board = Board()
        board[Coordinate(row: 0, col: 0)] = Tile(id: 1, value: 2)
        var game = GameState(testBoard: board, gravity: .down, mode: .daily(puzzleNumber: 1, moveBudget: 40), seed: 1)
        game.setTestStats(score: 1234567, cascadeCount: 2)
        #expect(ShareCard.text(for: game).contains("1,234,567"))
    }

    @Test func recordVariantAcceptsExplicitMovesAndDepth() {
        let text = ShareCard.text(
            mode: .daily(puzzleNumber: 9), score: 800, bestTile: 128,
            cascadeCount: 3, movesUsed: 34, deepestRound: 2
        )
        #expect(text.contains("Gravitile #9 — 800 · 34/40"))
        #expect(text.contains("×2 deep"))
        #expect(text.contains(ShareCard.appStoreURL))
    }
}
