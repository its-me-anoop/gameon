import Testing
@testable import GravitileKit

@Suite struct ShareCardTests {
    @Test func dailyCardShowsPuzzleNumberScoreAndProgression() {
        var board = Board()
        board[Coordinate(row: 4, col: 0)] = Tile(id: 1, value: 64)
        var game = GameState(testBoard: board, gravity: .down, mode: .daily(puzzleNumber: 12, moveBudget: 40), seed: 1)
        game.setTestStats(score: 4320, cascadeCount: 7)
        let text = ShareCard.text(for: game)
        #expect(text == """
        Gravitile #12 — 4,320
        🟨🟧🟥🟪🟦🟩
        🌀 7 cascades · 🏆 64
        """)
    }

    @Test func endlessCardOmitsPuzzleNumber() {
        var board = Board()
        board[Coordinate(row: 4, col: 0)] = Tile(id: 1, value: 8)
        var game = GameState(testBoard: board, gravity: .down, mode: .endless, seed: 1)
        game.setTestStats(score: 96, cascadeCount: 1)
        let text = ShareCard.text(for: game)
        #expect(text == """
        Gravitile Endless — 96
        🟨🟧🟥
        🌀 1 cascade · 🏆 8
        """)
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
}
