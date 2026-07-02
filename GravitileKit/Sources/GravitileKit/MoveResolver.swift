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
