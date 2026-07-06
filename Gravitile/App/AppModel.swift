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
    let gameCenter = GameCenterService()
    private let persistence: PersistenceService
    private let now: () -> Date

    var isPlus: Bool { store.isPlus }

    init(persistence: PersistenceService = PersistenceService(), now: @escaping () -> Date = { Date() }) {
        self.persistence = persistence
        self.now = now
        if ProcessInfo.processInfo.arguments.contains("-gravitile-reset") {
            // UI tests launch with a clean slate.
            let fresh = PersistedState()
            persistence.save(fresh)
            persisted = fresh
        } else {
            persisted = persistence.load()
        }
        haptics = HapticsService()
        sounds = SoundService()
        haptics.isEnabled = persisted.settings.hapticsOn
        sounds.isEnabled = persisted.settings.soundOn
        sounds.isMusicEnabled = persisted.settings.musicOn
    }

    // MARK: - Settings

    var settings: Settings {
        get { persisted.settings }
        set {
            persisted.settings = newValue
            haptics.isEnabled = newValue.hapticsOn
            sounds.isEnabled = newValue.soundOn
            sounds.isMusicEnabled = newValue.musicOn
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

    /// Resumes the saved zen game, or starts a fresh one.
    func zenGame() -> GameState {
        if let saved = persisted.zenGame, !saved.isGameOver { return saved }
        return newZenGame()
    }

    func newZenGame() -> GameState {
        let game = GameState(mode: .zen, seed: UInt64.random(in: UInt64.min...UInt64.max))
        persisted.zenGame = game
        save()
        return game
    }

    /// Resumes the saved sprint game, or starts a fresh one.
    func sprintGame() -> GameState {
        if let saved = persisted.sprintGame, !saved.isGameOver { return saved }
        return newSprintGame()
    }

    func newSprintGame() -> GameState {
        let game = GameState(mode: .sprint, seed: UInt64.random(in: UInt64.min...UInt64.max))
        persisted.sprintGame = game
        save()
        return game
    }

    /// "New Game" from inside a running game keeps the player in their mode.
    func newGame(like mode: GameMode) -> GameState {
        switch mode {
        case .endless: newEndlessGame()
        case .zen: newZenGame()
        case .sprint: newSprintGame()
        case .daily: dailyGame(puzzleNumber: todayPuzzleNumber)
        }
    }

    /// Called continuously as the game progresses so force-quits lose nothing.
    func checkpoint(_ game: GameState) {
        switch game.mode {
        case .endless:
            persisted.endlessGame = game
            persisted.bestEndlessScore = max(persisted.bestEndlessScore, game.score)
        case .zen:
            persisted.zenGame = game
            persisted.bestZenTile = max(persisted.bestZenTile, game.bestTile)
        case .sprint:
            persisted.sprintGame = game
            persisted.bestSprintScore = max(persisted.bestSprintScore, game.score)
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
        persisted.stats.bestCascadeRound = max(persisted.stats.bestCascadeRound, game.bestCascadeRound)
        persisted.bestTileEver = max(persisted.bestTileEver, game.bestTile)

        switch game.mode {
        case .endless:
            persisted.bestEndlessScore = max(persisted.bestEndlessScore, game.score)
            persisted.endlessGame = nil
        case .zen:
            persisted.bestZenTile = max(persisted.bestZenTile, game.bestTile)
            persisted.zenGame = nil
        case .sprint:
            persisted.bestSprintScore = max(persisted.bestSprintScore, game.score)
            persisted.sprintGame = nil
        case let .daily(puzzleNumber, _):
            persisted.dailyRecords[puzzleNumber] = DailyRecord(
                puzzleNumber: puzzleNumber, score: game.score, bestTile: game.bestTile,
                cascadeCount: game.cascadeCount, completedAt: now()
            )
            // Today extends the streak directly; archive completions of the
            // next-in-sequence puzzle count too (Plus's "catch up on a missed
            // day") — StreakState ignores older/replayed puzzles itself.
            if puzzleNumber <= todayPuzzleNumber {
                persisted.streak.recordCompletion(puzzleNumber: puzzleNumber, on: now())
                gameCenter.submitStreak(persisted.streak)
            }
            persisted.dailyGame = nil
        }
        gameCenter.submit(game: game)
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
