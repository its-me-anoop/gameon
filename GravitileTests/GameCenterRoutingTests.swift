import Testing
import GravitileKit
@testable import Gravitile

@MainActor
@Suite struct GameCenterRoutingTests {
    private func boards(for mode: GameMode) -> [String] {
        GameCenterService.leaderboardEntries(for: GameState(mode: mode, seed: 1)).map(\.board)
    }

    @Test func endlessRoutesScoreAndTile() {
        let b = boards(for: .endless)
        #expect(b.contains(GameCenterService.endlessLeaderboardID))
        #expect(b.contains(GameCenterService.bestTileLeaderboardID))
        #expect(!b.contains(GameCenterService.dailyLeaderboardID))
    }

    @Test func dailyRoutesToClassicAndWeeklyBoards() {
        let b = boards(for: .daily(puzzleNumber: 5))
        #expect(b.contains(GameCenterService.dailyLeaderboardID))
        #expect(b.contains(GameCenterService.dailyWeeklyLeaderboardID))
        #expect(!b.contains(GameCenterService.endlessLeaderboardID))
    }

    @Test func zenSubmitsItsBestTileAsTheScore() {
        let game = GameState(mode: .zen, seed: 1)
        let entries = GameCenterService.leaderboardEntries(for: game)
        let zenEntry = entries.first { $0.board == GameCenterService.zenTileLeaderboardID }
        #expect(zenEntry?.score == game.bestTile)
    }

    @Test func sprintRoutesToItsOwnBoard() {
        let b = boards(for: .sprint)
        #expect(b.contains(GameCenterService.sprintLeaderboardID))
        #expect(!b.contains(GameCenterService.endlessLeaderboardID))
    }
}
