import Testing
import Foundation
@testable import GravitileKit

/// Frozen replays of the doubling modes. These digests were captured on the
/// v1.2 engine (pre-MergeRule); any drift means the classic modes' RNG stream,
/// merge semantics, or scoring changed — which v1.3 must never do.
@Suite struct GoldenStreamTests {
    /// First legal direction in a fixed order — deterministic for a seed.
    private func playMoves(_ count: Int, game: inout GameState) {
        let order: [Direction] = [.down, .left, .up, .right]
        for _ in 0..<count {
            guard let direction = order.first(where: { d in
                var copy = game
                return copy.applyMove(d) != nil
            }) else { return }
            game.applyMove(direction)
        }
    }

    private func digest(_ board: Board) -> String {
        (0..<Board.size).map { row in
            (0..<Board.size).map { col in
                board[Coordinate(row: row, col: col)].map { String($0.value) } ?? "."
            }.joined(separator: ",")
        }.joined(separator: "/")
    }

    @Test func endlessSeed42ReplaysIdentically() {
        var game = GameState(mode: .endless, seed: 42)
        playMoves(40, game: &game)
        #expect(game.moveCount == 40)
        #expect(game.score == 240)
        #expect(game.bestTile == 32)
        #expect(digest(game.board) == ".,.,.,.,./.,.,.,.,./.,.,.,.,2/.,32,16,4,4/2,16,4,8,2")
    }

    @Test func dailyPuzzle100ReplaysIdentically() {
        let seed = DailySeed.seed(forPuzzleNumber: 100)
        var game = GameState(mode: .daily(puzzleNumber: 100), seed: seed)
        playMoves(GameMode.dailyMoveBudget, game: &game)
        #expect(game.score == 404)
        #expect(digest(game.board) == ".,.,.,.,./.,.,.,.,./.,.,.,.,./.,2,8,64,./.,4,2,4,8")
    }
}
