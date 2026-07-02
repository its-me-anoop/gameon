import SwiftUI
import GravitileKit

/// Root app state: persisted data, services, and game lifecycle. Owned by the
/// app and injected through the environment.
@Observable @MainActor
final class AppModel {
    private(set) var persisted: PersistedState
    let haptics: HapticsService
    let sounds: SoundService
    let store = StoreService()
    private let persistence: PersistenceService
    private let now: () -> Date

    var isPlus: Bool { store.isPlus }

    init(persistence: PersistenceService = PersistenceService(), now: @escaping () -> Date = { Date() }) {
        self.persistence = persistence
        self.now = now
        persisted = persistence.load()
        haptics = HapticsService()
        sounds = SoundService()
        haptics.isEnabled = persisted.settings.hapticsOn
        sounds.isEnabled = persisted.settings.soundOn
    }

    // MARK: - Settings

    var settings: Settings {
        get { persisted.settings }
        set {
            persisted.settings = newValue
            haptics.isEnabled = newValue.hapticsOn
            sounds.isEnabled = newValue.soundOn
            save()
        }
    }

    // MARK: - Endless lifecycle

    /// Resumes the saved endless game, or starts a fresh one.
    func endlessGame() -> GameState {
        if let saved = persisted.endlessGame, !saved.isGameOver {
            return saved
        }
        return newEndlessGame()
    }

    func newEndlessGame() -> GameState {
        let game = GameState(mode: .endless, seed: UInt64.random(in: UInt64.min...UInt64.max))
        persisted.endlessGame = game
        save()
        return game
    }

    /// Called continuously as the game progresses so force-quits lose nothing.
    func checkpoint(_ game: GameState) {
        switch game.mode {
        case .endless:
            persisted.endlessGame = game
            persisted.bestEndlessScore = max(persisted.bestEndlessScore, game.score)
        case .daily:
            persisted.dailyGame = game
        }
        persisted.bestTileEver = max(persisted.bestTileEver, game.bestTile)
        save()
    }

    func recordGameEnd(_ game: GameState) {
        persisted.stats.gamesPlayed += 1
        persisted.stats.totalScore += game.score
        persisted.stats.totalCascades += game.cascadeCount
        persisted.bestTileEver = max(persisted.bestTileEver, game.bestTile)

        switch game.mode {
        case .endless:
            persisted.bestEndlessScore = max(persisted.bestEndlessScore, game.score)
            persisted.endlessGame = nil
        case let .daily(puzzleNumber, _):
            persisted.dailyRecords[puzzleNumber] = DailyRecord(
                puzzleNumber: puzzleNumber, score: game.score, bestTile: game.bestTile,
                cascadeCount: game.cascadeCount, completedAt: now()
            )
            if puzzleNumber == todayPuzzleNumber {
                persisted.streak.recordCompletion(puzzleNumber: puzzleNumber, on: now())
            }
            persisted.dailyGame = nil
        }
        save()
    }

    // MARK: - Daily

    var todayPuzzleNumber: Int { DailySeed.puzzleNumber(for: now()) }

    var todayRecord: DailyRecord? { persisted.dailyRecords[todayPuzzleNumber] }

    /// Resumes today's in-progress daily or starts it. Past puzzles (archive)
    /// always start fresh.
    func dailyGame(puzzleNumber: Int) -> GameState {
        if let saved = persisted.dailyGame,
           case let .daily(number, _) = saved.mode,
           number == puzzleNumber, !saved.isGameOver {
            return saved
        }
        let game = GameState(
            mode: .daily(puzzleNumber: puzzleNumber),
            seed: DailySeed.seed(forPuzzleNumber: puzzleNumber)
        )
        if puzzleNumber == todayPuzzleNumber {
            persisted.dailyGame = game
            save()
        }
        return game
    }

    private func save() {
        persistence.save(persisted)
    }
}
