import Foundation
import GravitileKit

// Monte-Carlo balance harness. Not shipped in the app — used to tune spawn
// rates and validate game length / score spread before UI work.
// Usage: swift run -c release BalanceSim [games]

struct GameSummary {
    let moves: Int
    let score: Int
    let bestTile: Int
    let cascades: Int
    let hitCap: Bool
}

enum Policy: String {
    case random
    case greedy

    func chooseMove(for game: GameState, rng: inout SplitMix64) -> Direction? {
        let legal = Direction.allCases.filter { direction in
            var copy = game
            return copy.applyMove(direction) != nil
        }
        guard !legal.isEmpty else { return nil }
        switch self {
        case .random:
            return legal[Int(rng.next() % UInt64(legal.count))]
        case .greedy:
            // 1-ply lookahead: maximize immediate score delta.
            return legal.max { a, b in
                var ca = game, cb = game
                let sa = ca.applyMove(a)?.scoreDelta ?? -1
                let sb = cb.applyMove(b)?.scoreDelta ?? -1
                return sa < sb
            }
        }
    }
}

func play(mode: GameMode, seed: UInt64, policy: Policy, moveCap: Int) -> GameSummary {
    var game = GameState(mode: mode, seed: seed)
    var policyRNG = SplitMix64(seed: seed ^ 0xDEAD_BEEF)
    while !game.isGameOver && game.moveCount < moveCap {
        guard let move = policy.chooseMove(for: game, rng: &policyRNG) else { break }
        game.applyMove(move)
    }
    return GameSummary(
        moves: game.moveCount, score: game.score,
        bestTile: game.bestTile, cascades: game.cascadeCount,
        hitCap: game.moveCount >= moveCap
    )
}

func percentile(_ sorted: [Int], _ p: Double) -> Int {
    guard !sorted.isEmpty else { return 0 }
    let index = Int(Double(sorted.count - 1) * p)
    return sorted[index]
}

func report(_ label: String, _ summaries: [GameSummary]) {
    let moves = summaries.map(\.moves).sorted()
    let scores = summaries.map(\.score).sorted()
    let tiles = summaries.map(\.bestTile).sorted()
    let cascadesPerMove = summaries.map { Double($0.cascades) / Double(max(1, $0.moves)) }
    let meanCascadeRate = cascadesPerMove.reduce(0, +) / Double(cascadesPerMove.count)
    let capped = summaries.filter(\.hitCap).count
    print("""
    \(label)
      moves   p10/median/p90: \(percentile(moves, 0.1)) / \(percentile(moves, 0.5)) / \(percentile(moves, 0.9))
      score   p10/median/p90: \(percentile(scores, 0.1)) / \(percentile(scores, 0.5)) / \(percentile(scores, 0.9))
      bestTile median/p90:    \(percentile(tiles, 0.5)) / \(percentile(tiles, 0.9))
      cascade rounds per move: \(String(format: "%.2f", meanCascadeRate))
      hit move cap:            \(capped)/\(summaries.count)
    """)
}

let games = CommandLine.arguments.count > 1 ? Int(CommandLine.arguments[1]) ?? 500 : 500
let cap = CommandLine.arguments.count > 2 ? Int(CommandLine.arguments[2]) ?? 1000 : 1000
print("Simulating \(games) games per configuration (endless capped at \(cap) moves)…\n")

for policy in [Policy.random, .greedy] {
    let endless = (0..<games).map {
        play(mode: .endless, seed: UInt64($0 + 1), policy: policy, moveCap: cap)
    }
    report("ENDLESS / \(policy.rawValue)", endless)
    let daily = (0..<games).map {
        play(mode: .daily(puzzleNumber: 1, moveBudget: 40), seed: UInt64($0 + 1), policy: policy, moveCap: cap)
    }
    report("DAILY(40) / \(policy.rawValue)", daily)
    print("")
}
