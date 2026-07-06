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
    /// Boulder ice HP; 0 renders a normal tile.
    var ice: Int = 0
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
    /// Floating "+N" deltas above the score badge; self-expire.
    private(set) var scorePops: [ScorePop] = []
    /// Board shake for big cascades: one whole travel unit per shake.
    private(set) var shakeTravel: CGFloat = 0
    private(set) var shakeMagnitude: CGFloat = 0
    /// Small board push toward the new gravity edge during the rotation cue.
    private(set) var boardNudge: CGSize = .zero
    /// Milestone value currently being celebrated (first 256/512/… of a game).
    private(set) var celebrationValue: Int?
    /// The celebrated milestone also banked a stasis charge (endless only).
    private(set) var celebrationEarnedCharge = false
    /// Stasis is armed: the next swipe holds gravity in place.
    private(set) var stasisArmed = false
    private var milestones: MilestoneTracker
    private var nextPopID = 0
    /// Bumped whenever the board is wholesale replaced (new game, undo).
    /// In-flight fire-and-forget animation tasks check it before touching
    /// tiles — tile IDs restart per game, so stale tasks would otherwise
    /// mis-scale a *new* game's tiles.
    private var boardGeneration = 0
    var onMerge: ((Int) -> Void)?       // cascade round, for haptics/sound
    var onRotation: (() -> Void)?       // gravity turned — whoosh + tick
    var onLanding: (() -> Void)?        // a fall settled — thock
    var onMilestone: ((Int) -> Void)?   // first big tile of the game
    var onIceChip: ((Int) -> Void)?     // hpAfter; 0 = shattered free
    var onBoulderSpawned: (() -> Void)? // first-sighting hint hook
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
        milestones = MilestoneTracker(alreadyReached: game.bestTile)
        syncTilesToBoard()
    }

    var undosRemaining: Int { max(0, freeUndoLimit - game.undosUsed) }
    var canUndo: Bool { game.canUndo && undosRemaining > 0 && !isAnimating }

    func handleSwipe(_ direction: Direction) {
        guard !isAnimating, !game.isGameOver else { return }
        let useStasis = stasisArmed && game.canUseStasis
        let chargesBefore = game.stasisCharges
        guard let result = game.applyMove(direction, stasis: useStasis) else { return }
        stasisArmed = false
        celebrationEarnedCharge = game.stasisCharges > chargesBefore
        lastCascadeHighlight = result.phases.filter { !$0.merges.isEmpty }.map(\.round).max() ?? 0
        onMoveCommitted?(game)
        Task { await animate(result) }
    }

    /// Arm/disarm stasis for the next swipe. No-ops (and disarms) when the
    /// mode forbids it or no charge is banked.
    func toggleStasis() {
        guard game.canUseStasis, !game.isGameOver else {
            stasisArmed = false
            return
        }
        stasisArmed.toggle()
    }

    func undoTapped() {
        guard canUndo else { return }
        guard game.undo() else { return }
        boardGeneration += 1
        onMoveCommitted?(game)
        withAnimation(.easeInOut(duration: 0.2)) {
            syncTilesToBoard()
        }
    }

    func replace(game newGame: GameState) {
        game = newGame
        lastCascadeHighlight = 0
        boardGeneration += 1
        milestones = MilestoneTracker(alreadyReached: newGame.bestTile)
        scorePops = []
        celebrationValue = nil
        celebrationEarnedCharge = false
        stasisArmed = false
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
            // Single crossfade to the final board instead of movement — no
            // shake, nudge, or pops; milestones still sound and announce.
            let rounds = result.phases.filter { !$0.merges.isEmpty }.count
            if !result.slide.merges.isEmpty || rounds > 0 { onMerge?(max(1, rounds)) }
            withAnimation(.easeInOut(duration: 0.25)) {
                syncTilesToBoard()
            }
            try? await Task.sleep(for: .seconds(0.25))
            checkMilestone()
            return
        }

        var rotated = false
        for step in AnimationPlanner.steps(for: result) {
            // The nudge springs back as the tumble begins.
            if rotated, boardNudge != .zero {
                withAnimation(.spring(duration: 0.3, bounce: 0.5)) { boardNudge = .zero }
            }
            apply(step, isFall: rotated)
            if case .gravityCue = step { rotated = true }
            try? await Task.sleep(for: .seconds(AnimationPlanner.duration(of: step) + 0.02))
        }
        burstCells = []
        checkMilestone()
    }

    private func apply(_ step: AnimationStep, isFall: Bool) {
        switch step {
        case let .moves(moves, duration):
            withAnimation(.spring(duration: duration, bounce: 0.15)) {
                for move in moves {
                    if let index = tiles.firstIndex(where: { $0.id == move.tileID }) {
                        tiles[index].coordinate = move.to
                    }
                }
            }
            if isFall, !moves.isEmpty {
                onLanding?()
                squashOnLanding(moves.map(\.tileID), fallDuration: duration)
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
            addScorePop(points: merges.reduce(0) { $0 + $1.points }, round: round)
            if round >= 2 {
                shakeMagnitude = min(CGFloat(round) * 1.6 + 1, 8)
                withAnimation(.easeOut(duration: 0.4)) { shakeTravel += 1 }
            }

        case let .gravityCue(direction):
            // The compass observes game.gravity directly; here the board leans
            // toward the new edge so the turn is felt, not just seen. (Stasis
            // moves never reach here — the planner emits no cue for them.)
            onRotation?()
            withAnimation(.easeOut(duration: AnimationPlanner.cueDuration)) {
                boardNudge = direction.nudgeOffset
            }

        case let .iceChips(hits):
            for hit in hits {
                if let index = tiles.firstIndex(where: { $0.id == hit.tileID }) {
                    tiles[index].ice = hit.hpAfter
                    pulse(tileID: hit.tileID)
                }
                if hit.hpAfter == 0 {
                    burstCells.append((hit.at, 1))
                }
                onIceChip?(hit.hpAfter)
            }

        case let .spawn(spawns):
            if spawns.contains(where: { $0.tile.ice > 0 }) {
                onBoulderSpawned?()
            }
            for spawn in spawns {
                var tile = TileViewState(id: spawn.tile.id, value: spawn.tile.value, coordinate: spawn.enteredAt)
                tile.scale = 0.4
                tile.opacity = 0.4
                tile.ice = spawn.tile.ice
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

    private func addScorePop(points: Int, round: Int) {
        guard points > 0 else { return }
        let pop = ScorePop(id: nextPopID, points: points, round: round)
        nextPopID += 1
        scorePops.append(pop)
        Task { @MainActor in
            try? await Task.sleep(for: .seconds(0.95))
            scorePops.removeAll { $0.id == pop.id }
        }
    }

    /// Brief settle pulse on tiles that just landed, timed to the fall's end.
    /// Fire-and-forget: lookups are id-based, so tiles consumed by a later
    /// merge simply drop out of the pulse.
    private func squashOnLanding(_ tileIDs: [Int], fallDuration: TimeInterval) {
        let generation = boardGeneration
        Task { @MainActor in
            try? await Task.sleep(for: .seconds(fallDuration * 0.75))
            guard generation == boardGeneration else { return }
            withAnimation(.easeOut(duration: 0.06)) { setScale(0.93, for: tileIDs) }
            try? await Task.sleep(for: .seconds(0.07))
            guard generation == boardGeneration else { return }
            withAnimation(.spring(duration: 0.2, bounce: 0.45)) { setScale(1, for: tileIDs) }
        }
    }

    private func setScale(_ scale: CGFloat, for tileIDs: [Int]) {
        for id in tileIDs {
            if let index = tiles.firstIndex(where: { $0.id == id }) {
                tiles[index].scale = scale
            }
        }
    }

    /// Quick chip pulse on a boulder, generation-guarded like the squash.
    private func pulse(tileID: Int) {
        let generation = boardGeneration
        withAnimation(.easeOut(duration: 0.05)) { setScale(1.08, for: [tileID]) }
        Task { @MainActor in
            try? await Task.sleep(for: .seconds(0.06))
            guard generation == boardGeneration else { return }
            withAnimation(.spring(duration: 0.15, bounce: 0.4)) { setScale(1, for: [tileID]) }
        }
    }

    private func checkMilestone() {
        guard let value = milestones.newlyReached(bestTile: game.bestTile) else { return }
        withAnimation(.spring(duration: 0.35, bounce: 0.4)) { celebrationValue = value }
        onMilestone?(value)
        Task { @MainActor in
            try? await Task.sleep(for: .seconds(1.3))
            if celebrationValue == value {
                withAnimation(.easeOut(duration: 0.3)) { celebrationValue = nil }
            }
        }
    }

    private func syncTilesToBoard() {
        tiles = game.board.tiles.map { coordinate, tile in
            TileViewState(id: tile.id, value: tile.value, coordinate: coordinate, ice: tile.ice)
        }
    }
}
