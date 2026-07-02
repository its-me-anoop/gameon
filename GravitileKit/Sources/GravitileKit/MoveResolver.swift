/// Pure functions implementing the move pipeline from the design spec §3:
/// slide+merge (2048 semantics) → gravity rotation → fall → cascade rounds.
public enum MoveResolver {
    /// Slides every line toward `direction`, merging adjacent equal pairs once
    /// per move with the edge-nearest pair taking precedence. Merge results
    /// never re-merge within the same slide.
    public static func slide(
        _ board: Board, toward direction: Direction, nextTileID: inout Int
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
                if read + 1 < tiles.count, tiles[read + 1].1.value == tile.value {
                    let (partnerFrom, partner) = tiles[read + 1]
                    let result = Tile(id: nextTileID, value: tile.value * 2)
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
        return SlideOutcome(board: newBoard, moves: moves, merges: merges)
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
        rng: inout SplitMix64, nextTileID: inout Int, spawnCount: Int = 1
    ) -> MoveResult? {
        let slide = slide(board, toward: swipe, nextTileID: &nextTileID)
        guard slide.changed else {
            // Roll back IDs consumed by a no-op slide (there are none: an
            // unchanged slide performs no merges), keeping inputs untouched.
            return nil
        }

        let newGravity = gravity.rotatedClockwise
        var phases: [CascadePhase] = []
        var (current, initialFalls) = fall(slide.board, gravity: newGravity)
        phases.append(CascadePhase(falls: initialFalls, merges: [], round: 0))

        var round = 1
        var cascadePoints = 0
        while let (merged, merges) = cascadeRound(
            current, gravity: newGravity, round: round, nextTileID: &nextTileID
        ) {
            cascadePoints += merges.reduce(0) { $0 + $1.points }
            let (settled, falls) = fall(merged, gravity: newGravity)
            phases.append(CascadePhase(falls: falls, merges: merges, round: round))
            current = settled
            round += 1
        }

        var spawnEvents: [SpawnEvent] = []
        for _ in 0..<max(1, spawnCount) {
            guard let (withSpawn, event) = spawn(
                on: current, gravity: newGravity, rng: &rng, nextTileID: &nextTileID
            ) else { break }
            current = withSpawn
            spawnEvents.append(event)
        }

        let slidePoints = slide.merges.reduce(0) { $0 + $1.points }
        return MoveResult(
            swipe: swipe,
            slide: slide,
            newGravity: newGravity,
            phases: phases,
            spawns: spawnEvents,
            scoreDelta: slidePoints + cascadePoints,
            finalBoard: current
        )
    }

    /// Spawns one tile (90% value 2, 10% value 4) in a seeded-random column
    /// that has room. The tile enters at the opposite-gravity edge and rests
    /// at the deepest empty cell; spawn landings never merge.
    static func spawn(
        on board: Board, gravity: Direction, rng: inout SplitMix64, nextTileID: inout Int
    ) -> (Board, SpawnEvent)? {
        let lines = Board.lines(toward: gravity)
        let openLines = lines.filter { line in line.contains { board[$0] == nil } }
        guard !openLines.isEmpty else { return nil }

        let line = openLines[Int(rng.next() % UInt64(openLines.count))]
        let value = rng.next() % 10 < 9 ? 2 : 4
        let tile = Tile(id: nextTileID, value: value)
        nextTileID += 1

        // Settled boards have all tiles packed toward the gravity edge, so the
        // first empty cell walking from the edge is where the spawn rests.
        let restIndex = line.firstIndex { board[$0] == nil }!
        var newBoard = board
        newBoard[line[restIndex]] = tile
        let event = SpawnEvent(tile: tile, enteredAt: line.last!, restedAt: line[restIndex])
        return (newBoard, event)
    }

    /// One cascade round: along each gravity line, merge adjacent equal pairs
    /// (edge-nearest first, each tile in at most one merge). Returns nil when
    /// the board is already stable. Boards passed in must be settled (no gaps
    /// below tiles), which `fall` guarantees.
    public static func cascadeRound(
        _ board: Board, gravity: Direction, round: Int, nextTileID: inout Int
    ) -> (board: Board, merges: [MergeEvent])? {
        var newBoard = board
        var merges: [MergeEvent] = []
        for line in Board.lines(toward: gravity) {
            var index = 0
            while index + 1 < line.count {
                guard
                    let lower = newBoard[line[index]],
                    let upper = newBoard[line[index + 1]],
                    lower.value == upper.value
                else {
                    index += 1
                    continue
                }
                let result = Tile(id: nextTileID, value: lower.value * 2)
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
