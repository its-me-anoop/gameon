import SwiftUI
import GravitileKit

/// A tile as the board renders it. Engine tile IDs are stable across moves,
/// which is what makes position animations possible.
struct TileViewState: Identifiable, Equatable {
    let id: Int
    var value: Int
    var coordinate: Coordinate
    var scale: CGFloat = 1
    var opacity: Double = 1
}

@Observable @MainActor
final class GameViewModel {
    private(set) var game: GameState
    private(set) var tiles: [TileViewState] = []
    private(set) var isAnimating = false
    /// Highest cascade round of the last move, for HUD flair ("CASCADE ×3").
    private(set) var lastCascadeHighlight = 0
    /// Merge cells of the current animation step, for particle bursts.
    private(set) var burstCells: [(Coordinate, Int)] = []
    var onMerge: ((Int) -> Void)?       // cascade round, for haptics/sound
    var onGameOver: (() -> Void)?
    var onMoveCommitted: ((GameState) -> Void)?   // checkpoint persistence

    var freeUndoLimit: Int
    private let reduceMotion: () -> Bool

    init(
        game: GameState,
        freeUndoLimit: Int = 1,
        reduceMotion: @escaping () -> Bool = { UIAccessibility.isReduceMotionEnabled }
    ) {
        self.game = game
        self.freeUndoLimit = freeUndoLimit
        self.reduceMotion = reduceMotion
        syncTilesToBoard()
    }

    var undosRemaining: Int { max(0, freeUndoLimit - game.undosUsed) }
    var canUndo: Bool { game.canUndo && undosRemaining > 0 && !isAnimating }

    func handleSwipe(_ direction: Direction) {
        guard !isAnimating, !game.isGameOver else { return }
        guard let result = game.applyMove(direction) else { return }
        lastCascadeHighlight = result.phases.filter { !$0.merges.isEmpty }.map(\.round).max() ?? 0
        onMoveCommitted?(game)
        Task { await animate(result) }
    }

    func undoTapped() {
        guard canUndo else { return }
        guard game.undo() else { return }
        onMoveCommitted?(game)
        withAnimation(.easeInOut(duration: 0.2)) {
            syncTilesToBoard()
        }
    }

    func replace(game newGame: GameState) {
        game = newGame
        lastCascadeHighlight = 0
        withAnimation(.easeInOut(duration: 0.25)) {
            syncTilesToBoard()
        }
    }

    // MARK: - Animation

    private func animate(_ result: MoveResult) async {
        isAnimating = true
        defer {
            isAnimating = false
            // Defensive reconciliation: display must exactly match the engine.
            syncTilesToBoard()
            if game.isGameOver { onGameOver?() }
        }

        if reduceMotion() {
            // Single crossfade to the final board instead of movement.
            let rounds = result.phases.filter { !$0.merges.isEmpty }.count
            if !result.slide.merges.isEmpty || rounds > 0 { onMerge?(max(1, rounds)) }
            withAnimation(.easeInOut(duration: 0.25)) {
                syncTilesToBoard()
            }
            try? await Task.sleep(for: .seconds(0.25))
            return
        }

        for step in AnimationPlanner.steps(for: result) {
            apply(step)
            try? await Task.sleep(for: .seconds(AnimationPlanner.duration(of: step) + 0.02))
        }
        burstCells = []
    }

    private func apply(_ step: AnimationStep) {
        switch step {
        case let .moves(moves, duration):
            withAnimation(.spring(duration: duration, bounce: 0.15)) {
                for move in moves {
                    if let index = tiles.firstIndex(where: { $0.id == move.tileID }) {
                        tiles[index].coordinate = move.to
                    }
                }
            }

        case let .merges(merges, round):
            let consumed = Set(merges.flatMap(\.consumedTileIDs))
            withAnimation(.easeOut(duration: AnimationPlanner.mergeDuration)) {
                for index in tiles.indices where consumed.contains(tiles[index].id) {
                    tiles[index].scale = 0.55
                    tiles[index].opacity = 0
                }
            }
            tiles.removeAll { consumed.contains($0.id) }
            for merge in merges {
                var tile = TileViewState(id: merge.resultTile.id, value: merge.resultTile.value, coordinate: merge.at)
                tile.scale = 0.6
                tiles.append(tile)
                if let index = tiles.firstIndex(where: { $0.id == tile.id }) {
                    withAnimation(.spring(duration: AnimationPlanner.mergeDuration * 2, bounce: 0.4)) {
                        tiles[index].scale = 1
                    }
                }
            }
            burstCells = merges.map { ($0.at, round) }
            onMerge?(round)

        case .gravityCue:
            break // The compass observes game.gravity directly; step is a timing beat.

        case let .spawn(spawns):
            for spawn in spawns {
                var tile = TileViewState(id: spawn.tile.id, value: spawn.tile.value, coordinate: spawn.enteredAt)
                tile.scale = 0.4
                tile.opacity = 0.4
                tiles.append(tile)
            }
            withAnimation(.spring(duration: AnimationPlanner.spawnDuration, bounce: 0.2)) {
                for spawn in spawns {
                    if let index = tiles.firstIndex(where: { $0.id == spawn.tile.id }) {
                        tiles[index].coordinate = spawn.restedAt
                        tiles[index].scale = 1
                        tiles[index].opacity = 1
                    }
                }
            }
        }
    }

    private func syncTilesToBoard() {
        tiles = game.board.tiles.map { coordinate, tile in
            TileViewState(id: tile.id, value: tile.value, coordinate: coordinate)
        }
    }
}
