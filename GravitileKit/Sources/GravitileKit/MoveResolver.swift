/// Pure functions implementing the move pipeline from the design spec §3:
/// slide+merge (2048 semantics) → gravity rotation → fall → cascade rounds.
public enum MoveResolver {
    /// Slides every line toward `direction`, merging adjacent mergeable pairs
    /// once per move with the edge-nearest pair taking precedence (equal pairs
    /// under `.doubling`, target-sum pairs under `.sumTarget`). Merge results
    /// never re-merge within the same slide.
    public static func slide(
        _ board: Board, toward direction: Direction, nextTileID: inout Int,
        rule: MergeRule = .doubling
    ) -> SlideOutcome {
        var newBoard = Board()
        var moves: [TileMove] = []
        var merges: [MergeEvent] = []

        for line in Board.lines(toward: direction) {
            let tiles = line.compactMap { c in board[c].map { (c, $0) } }
            var write = 0
            var read = 0
            while read < tiles.count {
                let (from, tile) = tiles[read]
                let target = line[write]
                if read + 1 < tiles.count, rule.merges(tile.value, tiles[read + 1].1.value),
                   tile.ice == 0, tiles[read + 1].1.ice == 0 {
                    let (partnerFrom, partner) = tiles[read + 1]
                    let result = Tile(id: nextTileID, value: rule.mergedValue(tile.value, partner.value))
                    nextTileID += 1
                    newBoard[target] = result
                    if from != target {
                        moves.append(TileMove(tileID: tile.id, from: from, to: target))
                    }
                    moves.append(TileMove(tileID: partner.id, from: partnerFrom, to: target))
                    merges.append(MergeEvent(
                        consumedTileIDs: [tile.id, partner.id],
                        resultTile: result,
                        at: target,
                        points: result.value,
                        multiplier: 1
                    ))
                    read += 2
                } else {
                    newBoard[target] = tile
                    if from != target {
                        moves.append(TileMove(tileID: tile.id, from: from, to: target))
                    }
                    read += 1
                }
                write += 1
            }
        }
        let iceHits = chipIce(around: merges, on: &newBoard)
        let clears = bondClears(merges: merges, before: board, rule: rule, on: &newBoard)
        return SlideOutcome(board: newBoard, moves: moves, merges: merges, iceHits: iceHits, clears: clears)
    }

    /// Under `.sumTarget`, every merge result equals the target and pops off
    /// the board immediately; the following fall compacts the gap. Addends are
    /// looked up in the pre-merge board so the UI can show "3 + 7 = 10".
    static func bondClears(
        merges: [MergeEvent], before: Board, rule: MergeRule, on board: inout Board
    ) -> [ClearEvent] {
        guard case let .sumTarget(target) = rule else { return [] }
        var clears: [ClearEvent] = []
        for merge in merges where merge.resultTile.value == target {
            board[merge.at] = nil
            let addends = merge.consumedTileIDs.compactMap { id in
                before.tiles.first { $0.1.id == id }?.1.value
            }
            clears.append(ClearEvent(
                tileID: merge.resultTile.id, at: merge.at, value: target, addends: addends
            ))
        }
        return clears
    }

    /// Every merge chips one HP off each orthogonally-adjacent boulder.
    /// Freed tiles (HP 0) merge normally from the next pairing pass on.
    static func chipIce(around merges: [MergeEvent], on board: inout Board) -> [IceHit] {
        var hits: [IceHit] = []
        for merge in merges {
            for direction in Direction.allCases {
                let neighbor = merge.at.offset(by: direction)
                guard Board.contains(neighbor), var tile = board[neighbor], tile.ice > 0 else { continue }
                tile.ice -= 1
                board[neighbor] = tile
                hits.append(IceHit(tileID: tile.id, at: neighbor, hpAfter: tile.ice))
            }
        }
        return hits
    }

    /// Compacts all tiles toward `gravity` without merging.
    public static func fall(
        _ board: Board, gravity: Direction
    ) -> (board: Board, moves: [TileMove]) {
        var newBoard = Board()
        var moves: [TileMove] = []
        for line in Board.lines(toward: gravity) {
            var write = 0
            for coordinate in line {
                guard let tile = board[coordinate] else { continue }
                let target = line[write]
                newBoard[target] = tile
                if coordinate != target {
                    moves.append(TileMove(tileID: tile.id, from: coordinate, to: target))
                }
                write += 1
            }
        }
        return (newBoard, moves)
    }

    /// Runs the full pipeline for one swipe. Returns nil when the swipe is
    /// illegal (the slide phase changes nothing); in that case no state —
    /// including the RNG and tile counter — is consumed.
    public static func resolveMove(
        board: Board, swipe: Direction, gravity: Direction,
        rng: inout SplitMix64, nextTileID: inout Int, spawnCount: Int = 1,
        rotateGravity: Bool = true, boulderIce: Int = 0, rule: MergeRule = .doubling
    ) -> MoveResult? {
        let slide = slide(board, toward: swipe, nextTileID: &nextTileID, rule: rule)
        guard slide.changed else {
            // Roll back IDs consumed by a no-op slide (there are none: an
            // unchanged slide performs no merges), keeping inputs untouched.
            return nil
        }

        let newGravity = rotateGravity ? gravity.rotatedClockwise : gravity
        var phases: [CascadePhase] = []
        var (current, initialFalls) = fall(slide.board, gravity: newGravity)
        phases.append(CascadePhase(falls: initialFalls, merges: [], round: 0))

        var round = 1
        var cascadePoints = 0
        while let (merged, merges) = cascadeRound(
            current, gravity: newGravity, round: round, nextTileID: &nextTileID, rule: rule
        ) {
            var chipped = merged
            let clears = bondClears(merges: merges, before: current, rule: rule, on: &chipped)
            let iceHits = chipIce(around: merges, on: &chipped)
            cascadePoints += merges.reduce(0) { $0 + $1.points } + iceHits.count * 10 * round
            let (settled, falls) = fall(chipped, gravity: newGravity)
            phases.append(CascadePhase(
                falls: falls, merges: merges, round: round, iceHits: iceHits, clears: clears
            ))
            current = settled
            round += 1
        }

        var spawnEvents: [SpawnEvent] = []
        for _ in 0..<max(1, spawnCount) {
            guard let (withSpawn, event) = spawn(
                on: current, gravity: newGravity, rng: &rng, nextTileID: &nextTileID,
                ice: spawnEvents.isEmpty ? boulderIce : 0, rule: rule
            ) else { break }
            current = withSpawn
            spawnEvents.append(event)
        }

        let slidePoints = slide.merges.reduce(0) { $0 + $1.points } + slide.iceHits.count * 10
        return MoveResult(
            swipe: swipe,
            slide: slide,
            newGravity: newGravity,
            phases: phases,
            spawns: spawnEvents,
            scoreDelta: slidePoints + cascadePoints,
            finalBoard: current,
            heldGravity: !rotateGravity
        )
    }

    /// Spawns one tile in a seeded-random column that has room — 90% value 2 /
    /// 10% value 4 under `.doubling`, uniform over the stage's bond range under
    /// `.sumTarget`. Either way the draw consumes exactly two randoms (line,
    /// value), so rules can't skew each other's streams. The tile enters at the
    /// opposite-gravity edge and rests at the deepest empty cell; spawn
    /// landings never merge.
    static func spawn(
        on board: Board, gravity: Direction, rng: inout SplitMix64, nextTileID: inout Int,
        ice: Int = 0, rule: MergeRule = .doubling
    ) -> (Board, SpawnEvent)? {
        let lines = Board.lines(toward: gravity)
        let openLines = lines.filter { line in line.contains { board[$0] == nil } }
        guard !openLines.isEmpty else { return nil }

        let line = openLines[Int(rng.next() % UInt64(openLines.count))]
        let value: Int
        switch rule {
        case .doubling:
            value = rng.next() % 10 < 9 ? 2 : 4
        case let .sumTarget(target):
            let range = MathProgression.spawnRange(for: target)
            value = range.lowerBound + Int(rng.next() % UInt64(range.count))
        }
        // Boulders draw exactly the same randoms as normal spawns so the
        // seeded stream — and every pre-boulder golden game — is untouched.
        let tile = Tile(id: nextTileID, value: value, ice: ice)
        nextTileID += 1

        // Settled boards have all tiles packed toward the gravity edge, so the
        // first empty cell walking from the edge is where the spawn rests.
        let restIndex = line.firstIndex { board[$0] == nil }!
        var newBoard = board
        newBoard[line[restIndex]] = tile
        let event = SpawnEvent(tile: tile, enteredAt: line.last!, restedAt: line[restIndex])
        return (newBoard, event)
    }

    /// One cascade round: along each gravity line, merge adjacent mergeable
    /// pairs (edge-nearest first, each tile in at most one merge). Returns nil
    /// when the board is already stable. Boards passed in must be settled (no
    /// gaps below tiles), which `fall` guarantees.
    public static func cascadeRound(
        _ board: Board, gravity: Direction, round: Int, nextTileID: inout Int,
        rule: MergeRule = .doubling
    ) -> (board: Board, merges: [MergeEvent])? {
        var newBoard = board
        var merges: [MergeEvent] = []
        for line in Board.lines(toward: gravity) {
            var index = 0
            while index + 1 < line.count {
                guard
                    let lower = newBoard[line[index]],
                    let upper = newBoard[line[index + 1]],
                    rule.merges(lower.value, upper.value),
                    lower.ice == 0, upper.ice == 0
                else {
                    index += 1
                    continue
                }
                let result = Tile(id: nextTileID, value: rule.mergedValue(lower.value, upper.value))
                nextTileID += 1
                newBoard[line[index]] = result
                newBoard[line[index + 1]] = nil
                merges.append(MergeEvent(
                    consumedTileIDs: [lower.id, upper.id],
                    resultTile: result,
                    at: line[index],
                    points: result.value * round,
                    multiplier: round
                ))
                index += 2
            }
        }
        return merges.isEmpty ? nil : (newBoard, merges)
    }
}
