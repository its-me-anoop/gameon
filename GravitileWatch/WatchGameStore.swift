import Foundation
import GravitileKit

/// The wrist game's whole persistence story: one resumable zen game and a
/// best score in UserDefaults. Standalone by design — no phone sync, the
/// watch app must carry its own weight (App Review 4.2).
@Observable @MainActor
final class WatchGameStore {
    private static let gameKey = "watch.game"
    private static let bestKey = "watch.bestScore"

    private(set) var game: GameState
    private(set) var bestScore: Int
    private let defaults: UserDefaults

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        bestScore = defaults.integer(forKey: Self.bestKey)
        if let data = defaults.data(forKey: Self.gameKey),
           let saved = try? JSONDecoder().decode(GameState.self, from: data),
           !saved.isGameOver {
            game = saved
        } else {
            game = GameState(mode: .zen, seed: UInt64.random(in: .min ... .max))
        }
        #if DEBUG
        // Screenshot helper: pre-play N random moves so simulator captures
        // show a lived-in board (mirrors the phone's GRAVITILE_AUTOPLAY hook).
        if let moves = ProcessInfo.processInfo.environment["GRAVITILE_WATCH_SEED_MOVES"].flatMap(Int.init),
           moves > 0, game.moveCount == 0 {
            for _ in 0..<moves {
                guard let direction = Direction.allCases.shuffled().first(where: { direction in
                    var copy = game
                    return copy.applyMove(direction) != nil
                }) else { break }
                game.applyMove(direction)
            }
            checkpoint()
        }
        #endif
    }

    @discardableResult
    func apply(_ direction: Direction) -> MoveResult? {
        let result = game.applyMove(direction)
        if result != nil { checkpoint() }
        return result
    }

    func newGame() {
        game = GameState(mode: .zen, seed: UInt64.random(in: .min ... .max))
        checkpoint()
    }

    private func checkpoint() {
        bestScore = max(bestScore, game.score)
        defaults.set(bestScore, forKey: Self.bestKey)
        if let data = try? JSONEncoder().encode(game) {
            defaults.set(data, forKey: Self.gameKey)
        }
    }
}
