import Testing
import Foundation
@testable import GravitileKit

@Suite struct BoulderTests {
    // MARK: - Merge exclusion

    @Test func icedTilesSlideButNeverMerge() {
        var board = Board()
        board[Coordinate(row: 4, col: 1)] = Tile(id: 900, value: 2, ice: 1)
        board[Coordinate(row: 4, col: 2)] = Tile(id: 901, value: 2)
        var game = GameState(testBoard: board, gravity: .down, mode: .zen, seed: 1)

        let result = game.applyMove(.left)!
        #expect(result.slide.merges.isEmpty)
        #expect(result.phases.allSatisfy { $0.merges.isEmpty })
        // Both tiles survived (plus one spawn).
        #expect(game.board.tiles.count == 3)
        #expect(game.board.tiles.contains { $0.1.id == 900 && $0.1.ice == 1 })
    }

    // MARK: - Chipping

    @Test func slidePhaseMergeChipsAdjacentBoulderAndScores() {
        var board = Board()
        board[Coordinate(row: 4, col: 0)] = Tile(id: 900, value: 4)
        board[Coordinate(row: 4, col: 1)] = Tile(id: 901, value: 4)
        board[Coordinate(row: 4, col: 2)] = Tile(id: 902, value: 2, ice: 2)
        var game = GameState(testBoard: board, gravity: .down, mode: .zen, seed: 1)

        let result = game.applyMove(.left)!
        // 4+4 merge lands at (4,0); the boulder slides to (4,1) beside it.
        #expect(result.slide.iceHits.count == 1)
        #expect(result.slide.iceHits.first?.tileID == 902)
        #expect(result.slide.iceHits.first?.hpAfter == 1)
        let boulder = game.board.tiles.first { $0.1.id == 902 }
        #expect(boulder?.1.ice == 1)
        // 8 for the merge + 10 per chip.
        #expect(game.score == 18)
    }

    @Test func boulderFreedInSlidePhaseMergesInTheCascade() {
        var board = Board()
        board[Coordinate(row: 4, col: 0)] = Tile(id: 900, value: 4)
        board[Coordinate(row: 4, col: 1)] = Tile(id: 901, value: 4)
        board[Coordinate(row: 4, col: 2)] = Tile(id: 902, value: 2, ice: 1)
        board[Coordinate(row: 4, col: 3)] = Tile(id: 903, value: 2)
        var game = GameState(testBoard: board, gravity: .down, mode: .zen, seed: 1)

        let result = game.applyMove(.left)!
        // Slide: 4+4 → 8@(4,0); boulder chips to 0 (freed).
        #expect(result.slide.iceHits.first?.hpAfter == 0)
        // Cascade round 1 (gravity now .left): freed 2 pairs with the other 2.
        let cascadeMerges = result.phases.flatMap(\.merges)
        #expect(cascadeMerges.contains { $0.resultTile.value == 4 })
        #expect(game.board.tiles.allSatisfy { $0.1.ice == 0 })
    }

    @Test func cascadeRoundMergesChipTooAndAreReported() {
        var board = Board()
        // Slide-left merges N+P at (4,1) → falls left → 4@(4,0),4@(4,1) →
        // round-1 merge at (4,0); boulder waits at (3,0), orthogonally adjacent.
        board[Coordinate(row: 4, col: 0)] = Tile(id: 900, value: 4)
        board[Coordinate(row: 4, col: 1)] = Tile(id: 901, value: 2)
        board[Coordinate(row: 4, col: 2)] = Tile(id: 902, value: 2)
        board[Coordinate(row: 3, col: 0)] = Tile(id: 903, value: 8, ice: 1)
        var game = GameState(testBoard: board, gravity: .down, mode: .zen, seed: 1)

        let result = game.applyMove(.left)!
        let cascadeHits = result.phases.flatMap(\.iceHits)
        #expect(cascadeHits.contains { $0.tileID == 903 && $0.hpAfter == 0 })
        let freed = game.board.tiles.first { $0.1.id == 903 }
        #expect(freed?.1.ice == 0)
    }

    // MARK: - Spawn schedule

    @Test func zenNeverSpawnsBoulders() {
        var game = GameState(mode: .zen, seed: 11)
        var made = 0
        outer: while made < 60 {
            for direction in Direction.allCases {
                var copy = game
                guard copy.applyMove(direction) != nil else { continue }
                let result = game.applyMove(direction)!
                #expect(result.spawns.allSatisfy { $0.tile.ice == 0 })
                made += 1
                continue outer
            }
            break
        }
        #expect(made == 60)
    }

    @Test func endlessSpawnsBouldersOnCadenceFromMoveForty() {
        var game = GameState(mode: .endless, seed: 11)
        var bouldersSeen: [Int] = []
        var made = 0
        outer: while made < 70 {
            for direction in Direction.allCases {
                var copy = game
                guard copy.applyMove(direction) != nil else { continue }
                let movesPlayed = game.moveCount
                let result = game.applyMove(direction)!
                if let first = result.spawns.first, first.tile.ice > 0 {
                    bouldersSeen.append(movesPlayed)
                }
                // Never more than one boulder per move.
                #expect(result.spawns.filter { $0.tile.ice > 0 }.count <= 1)
                made += 1
                continue outer
            }
            break
        }
        #expect(bouldersSeen.contains(40), "first boulder lands at move 40")
        #expect(bouldersSeen.allSatisfy { $0 >= 40 && ($0 - 40) % 12 == 0 })
        #expect(bouldersSeen.count >= 2)
    }

    @Test func dailySpawnsExactlyTwoSeedDeterminedBoulders() {
        let seed = DailySeed.seed(forPuzzleNumber: 3)
        var game = GameState(mode: .daily(puzzleNumber: 3), seed: seed)
        var boulders = 0
        outer: while !game.isGameOver {
            for direction in Direction.allCases {
                var copy = game
                guard copy.applyMove(direction) != nil else { continue }
                let result = game.applyMove(direction)!
                boulders += result.spawns.filter { $0.tile.ice > 0 }.count
                continue outer
            }
            break
        }
        if game.moveCount == GameMode.dailyMoveBudget {
            #expect(boulders == 2, "a completed daily meets exactly two boulders")
        } else {
            #expect(boulders <= 2)
        }
    }

    @Test func identicalSeedsProduceIdenticalDailyBoulders() {
        let seed = DailySeed.seed(forPuzzleNumber: 7)
        var a = GameState(mode: .daily(puzzleNumber: 7), seed: seed)
        var b = GameState(mode: .daily(puzzleNumber: 7), seed: seed)
        for _ in 0..<20 {
            guard let direction = Direction.allCases.first(where: { d in
                var copy = a
                return copy.applyMove(d) != nil
            }) else { break }
            let ra = a.applyMove(direction)
            let rb = b.applyMove(direction)
            #expect(ra == rb)
        }
        #expect(a == b)
    }

    // MARK: - RNG stream integrity

    @Test func boulderSpawnDrawsExactlyTheSameRandomsAsANormalSpawn() {
        var board = Board()
        board[Coordinate(row: 4, col: 0)] = Tile(id: 900, value: 2)

        var rngA = SplitMix64(seed: 9)
        var idA = 50
        let (_, eventA) = MoveResolver.spawn(on: board, gravity: .down, rng: &rngA, nextTileID: &idA)!

        var rngB = SplitMix64(seed: 9)
        var idB = 50
        let (_, eventB) = MoveResolver.spawn(on: board, gravity: .down, rng: &rngB, nextTileID: &idB, ice: 2)!

        #expect(eventA.tile.value == eventB.tile.value)
        #expect(eventA.restedAt == eventB.restedAt)
        #expect(eventB.tile.ice == 2)
        #expect(rngA == rngB, "boulder spawns must not disturb the seeded stream")
    }

    // MARK: - Persistence

    @Test func v11TileJSONDecodesWithIceZero() throws {
        let tile = try JSONDecoder().decode(Tile.self, from: Data(#"{"id":7,"value":128}"#.utf8))
        #expect(tile.ice == 0)
    }

    @Test func icedTilesRoundTripThroughCodable() throws {
        var board = Board()
        board[Coordinate(row: 2, col: 2)] = Tile(id: 900, value: 4, ice: 2)
        let game = GameState(testBoard: board, gravity: .down, mode: .endless, seed: 1)
        let data = try JSONEncoder().encode(game)
        let back = try JSONDecoder().decode(GameState.self, from: data)
        #expect(back == game)
        #expect(back.board[Coordinate(row: 2, col: 2)]?.ice == 2)
    }
}
