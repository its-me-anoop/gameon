import Foundation
import OrbitKit
import simd

/// Balance harness: plays a plausible strategy at accelerated time and reports
/// how long the arc takes, so the economy is tuned against numbers instead of
/// vibes.
///
///     swift run --package-path GravitileKit -c release OrbitSim [runs] [hours]

let arguments = CommandLine.arguments
let runs = arguments.count > 1 ? Int(arguments[1]) ?? 20 : 20
let hours = arguments.count > 2 ? Double(arguments[2]) ?? 4 : 4

/// Shares of the built world a competent player aims for. The bot always buys
/// whatever it is furthest below, which keeps the whole production chain fed
/// instead of spamming the cheapest thing.
let targetMix: [BuildingKind: Double] = [
    .oreMine: 0.32, .solarArray: 0.28, .condenser: 0.14,
    .smelter: 0.13, .ballast: 0.07, .greenhouse: 0.04, .beacon: 0.02,
]

struct RunResult {
    var peakMass = 0.0
    var collapses = 0
    var firstCollapse: Double?
    var peakWobble = 0.0
    var buildings = 0
    var maxLevel = 1
    var finalDeclination = 0.0
    var stalledOn: String = "—"
}

func bestTile(for kind: BuildingKind, in world: World) -> Int? {
    var best: (tile: Int, score: Double)?
    for tile in world.geometry.tiles.indices where world.state.buildings[tile] == nil {
        let readout = world.readout(for: tile)
        let score: Double
        switch kind {
        case .ballast:
            // Ballast goes on the day/night terminator. Mass wants the equator,
            // so loading the terminator tips the axis toward the star — which
            // is how a player buys a permanently lit cap and a permanently
            // frozen hemisphere at the same time.
            score = 1 - abs(dot(world.geometry.tiles[tile].center, World.starDirection))
        default:
            score = kind.efficiency(
                temperatureIndex: readout.temperatureIndex, solarFactor: readout.solarFactor
            ) * (kind == .oreMine ? readout.terrain.oreRichness : 1)
        }
        if best == nil || score > best!.score { best = (tile, score) }
    }
    return best.map(\.tile)
}

func playOneRun(seed: UInt64, hours: Double) -> RunResult {
    var world = World(state: WorldState(seed: seed, now: Date(timeIntervalSince1970: 0)))
    var result = RunResult()
    let tickSeconds = 5.0
    let totalTicks = Int(hours * 3600 / tickSeconds)
    var starved: [ResourceKind: Int] = [:]

    for tick in 0..<totalTicks {
        world.advance(by: tickSeconds)
        result.peakWobble = max(result.peakWobble, world.wobbleDegrees)
        if world.state.activeMeteor != nil, tick % 2 == 0 { world.catchMeteor() }

        guard tick % 3 == 0 else { continue }

        // Buy the kind we are furthest below target on, among what we can pay
        // for and place.
        let counts = world.state.buildings.values.reduce(into: [BuildingKind: Int]()) {
            $0[$1.kind, default: 0] += 1
        }
        let total = max(1, world.state.buildings.count)
        let wanted = targetMix
            .filter { world.isUnlocked($0.key) }
            .map { kind, share -> (BuildingKind, Double) in
                (kind, share - Double(counts[kind] ?? 0) / Double(total))
            }
            .sorted { $0.1 > $1.1 }

        var bought = false
        for (kind, _) in wanted {
            guard world.state.resources.covers(world.placementCost(kind)) else {
                for kindShortage in world.placementCost(kind).nonZeroKinds
                where world.state.resources[kindShortage] < world.placementCost(kind)[kindShortage] {
                    starved[kindShortage, default: 0] += 1
                }
                continue
            }
            guard let tile = bestTile(for: kind, in: world) else { continue }
            world.place(kind, on: tile)
            bought = true
            break
        }

        // Out of room or out of options: grow upward instead.
        if !bought {
            let candidates = world.state.buildings
                .filter { world.canUpgrade(tile: $0.key) }
                .sorted { $0.value.level < $1.value.level }
            if let target = candidates.first {
                world.upgrade(tile: target.key)
                result.maxLevel = max(result.maxLevel, target.value.level + 1)
            }
        }

        if world.canCollapse {
            if result.firstCollapse == nil { result.firstCollapse = world.state.lifetimeElapsed }
            world.collapse(now: Date(timeIntervalSince1970: 0))
        }
    }

    result.peakMass = world.state.lifetimePeakMass
    result.collapses = world.state.collapses
    result.buildings = world.state.buildings.count
    result.finalDeclination = world.climate.declination * 180 / .pi
    if let worst = starved.max(by: { $0.value < $1.value }) {
        result.stalledOn = "\(worst.key.displayName) (\(worst.value) blocked buys)"
    }
    return result
}

var results: [RunResult] = []
for run in 0..<runs {
    results.append(playOneRun(seed: UInt64(run &* 2_654_435_761 &+ 12345), hours: hours))
}

func percentile(_ values: [Double], _ p: Double) -> Double {
    guard !values.isEmpty else { return 0 }
    let sorted = values.sorted()
    let index = min(sorted.count - 1, max(0, Int((Double(sorted.count - 1) * p).rounded())))
    return sorted[index]
}

func f(_ value: Double) -> String { String(format: "%.1f", value) }

let masses = results.map(\.peakMass)
let collapses = results.map { Double($0.collapses) }
let firstCollapses = results.compactMap(\.firstCollapse)
let wobbles = results.map(\.peakWobble)
let declinations = results.map { abs($0.finalDeclination) }

print("OrbitSim — \(runs) runs × \(f(hours))h of simulated play")
print("")
print("peak mass        p10 \(f(percentile(masses, 0.1)))   median \(f(percentile(masses, 0.5)))   p90 \(f(percentile(masses, 0.9)))")
print("collapses        median \(f(percentile(collapses, 0.5)))   max \(Int(collapses.max() ?? 0))")
if firstCollapses.isEmpty {
    print("first collapse   never reached in \(f(hours))h — economy is too slow")
} else {
    print("first collapse   median \(f(percentile(firstCollapses, 0.5) / 60)) min   (\(firstCollapses.count)/\(runs) runs reached it)")
}
print("peak wobble      median \(f(percentile(wobbles, 0.5)))°   max \(f(wobbles.max() ?? 0))°")
print("final tilt       median \(f(percentile(declinations, 0.5)))° declination")
print("top level        max \(results.map(\.maxLevel).max() ?? 1)")
print("bottleneck       \(results.last?.stalledOn ?? "—")")
