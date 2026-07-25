import Testing
import Foundation
import simd
@testable import OrbitKit

@Suite struct WorldTests {
    private func newWorld() -> World {
        World(state: WorldState(seed: 42, now: Date(timeIntervalSince1970: 0)))
    }

    /// Everything affordable — for tests about physics rather than economy.
    /// Biomass stays at zero so collapse rewards remain predictable.
    private var stocked: Resources {
        Resources(ore: 999_999, charge: 999_999, water: 999_999, alloy: 999_999)
    }

    /// Index of a tile close to the current equator, where solar arrays work.
    private func equatorTile(_ world: World) -> Int {
        let axis = world.state.axis
        return world.geometry.tiles
            .min { abs(dot($0.center, axis)) < abs(dot($1.center, axis)) }!
            .index
    }

    @Test func aNewWorldIsEmptyAndAffordsItsFirstBuilding() {
        let world = newWorld()
        #expect(world.geometry.tileCount == 162)
        #expect(world.state.buildings.isEmpty)
        #expect(world.totalMass == 0)
        // An empty world is already spinning the way its own bulge wants to.
        #expect(world.wobbleDegrees < 1e-6)
        #expect(world.canPlace(.solarArray, on: 0))
        #expect(world.state.resources.ore == 70)
    }

    @Test func placingSpendsResourcesAndAddsMass() {
        var world = newWorld()
        let placedArray = world.place(.solarArray, on: 5)
        #expect(placedArray)
        #expect(world.state.resources.ore == 45)
        #expect(world.totalMass == BuildingKind.solarArray.baseMass)
        #expect(world.state.placements == 1)

        // The tile is taken, and there is no ore left for a second array.
        #expect(!world.canPlace(.oreMine, on: 5))
        let placedMine = world.place(.oreMine, on: 6)
        #expect(placedMine)
        #expect(!world.canPlace(.solarArray, on: 7))
    }

    @Test func lockedKindsCannotBePlacedBeforeTheirTier() {
        var world = newWorld()
        world.state.resources = stocked
        #expect(!world.isUnlocked(.greenhouse))
        let blocked = world.place(.greenhouse, on: 3)
        #expect(!blocked)
        world.state.tier = 1
        #expect(world.isUnlocked(.greenhouse))
        let allowed = world.place(.greenhouse, on: 3)
        #expect(allowed)
    }

    @Test func upgradingRaisesOutputAndMass() {
        var world = newWorld()
        let tile = equatorTile(world)
        world.place(.solarArray, on: tile)
        let before = world.readout(for: tile)
        world.state.resources = stocked
        let upgraded = world.upgrade(tile: tile)
        #expect(upgraded)
        let after = world.readout(for: tile)
        #expect(after.building?.level == 2)
        #expect(after.output.charge > before.output.charge * 1.5)
        #expect(after.building!.mass > before.building!.mass)
    }

    @Test func demolishingRefundsHalfAndRemovesMass() {
        var world = newWorld()
        world.place(.solarArray, on: 8)
        let afterPlacing = world.state.resources.ore
        let demolished = world.demolish(tile: 8)
        #expect(demolished)
        #expect(world.state.resources.ore == afterPlacing + 12.5)
        #expect(world.totalMass == 0)
    }

    @Test func solarArraysProduceChargeAndBuildStock() {
        var world = newWorld()
        world.place(.solarArray, on: equatorTile(world))
        let events = world.advance(by: 60)
        #expect(events.gained.charge > 20)
        #expect(world.state.resources.charge > 20)
    }

    @Test func polarSolarArraysProduceAlmostNothing() {
        var world = newWorld()
        let axis = world.state.axis
        let pole = world.geometry.nearestTile(to: axis)
        let equator = equatorTile(world)
        world.state.resources.ore = 9999
        world.place(.solarArray, on: pole)
        world.place(.solarArray, on: equator)
        // A fresh world spins perpendicular to its star, so the poles are
        // frozen and an array there is nearly useless.
        #expect(world.climate.declination == 0)
        #expect(world.readout(for: pole).output.charge < world.readout(for: equator).output.charge / 5)
    }

    @Test func starvedSmeltersSlowDownInsteadOfGoingNegative() {
        var world = newWorld()
        world.state.resources = Resources(ore: 200, charge: 100)
        world.place(.smelter, on: equatorTile(world))
        // No mines, no arrays: the smelter drains the stock and then stalls.
        world.advance(by: 600)
        #expect(world.state.resources.ore >= 0)
        #expect(world.state.resources.charge >= 0)
        #expect(world.state.resources.alloy > 0)
    }

    @Test func resourcesNeverExceedStorageCaps() {
        var world = newWorld()
        world.state.resources = stocked
        for tile in 0..<20 { world.place(.oreMine, on: tile) }
        world.advance(by: 20_000, mode: .offline)
        #expect(world.state.resources.ore <= world.storageCaps.ore + 1e-6)
    }

    /// The whole point of the game, as a test: put mass on one side and the
    /// axis comes around until that mass sits on the equator.
    @Test func heavyBuildingSteersTheAxisUntilItSitsOnTheEquator() {
        var world = newWorld()
        world.state.resources = stocked
        let pole = world.geometry.nearestTile(to: world.state.axis)
        world.place(.ballast, on: pole)

        let startingOffset = abs(dot(world.geometry.tiles[pole].center, world.state.axis))
        #expect(startingOffset > 0.9, "the ballast starts at the pole")
        #expect(world.wobbleDegrees > 70)

        world.advance(by: 900)

        let endingOffset = abs(dot(world.geometry.tiles[pole].center, world.state.axis))
        #expect(endingOffset < 0.05, "the ballast ends up on the equator")
        #expect(world.wobbleDegrees < 1)
    }

    @Test func steeringChangesWhatATileProduces() {
        var world = newWorld()
        world.state.resources = stocked
        let pole = world.geometry.nearestTile(to: world.state.axis)
        world.place(.solarArray, on: pole)
        let frozenOutput = world.readout(for: pole).output.charge

        // Pile ballast next to the array; the whole neighborhood swings toward
        // the equator and the array wakes up.
        for neighbor in world.geometry.tiles[pole].neighbors {
            world.place(.ballast, on: neighbor)
        }
        world.advance(by: 1200)

        #expect(world.readout(for: pole).output.charge > frozenOutput * 5)
    }

    /// Weight is how fast you steer. The crust's bulge does not veto a light
    /// building, it just makes it slow — a lone array needs half an hour to
    /// swing a world, a Ballast needs five minutes.
    @Test func heavierBuildingsSteerFaster() {
        func alignmentAfterFiveMinutes(of kind: BuildingKind) -> Double {
            var world = newWorld()
            world.state.resources = stocked
            let pole = world.geometry.nearestTile(to: world.state.axis)
            world.place(kind, on: pole)
            world.advance(by: 300)
            return abs(dot(world.geometry.tiles[pole].center, world.state.axis))
        }

        let array = alignmentAfterFiveMinutes(of: .solarArray)
        let smelter = alignmentAfterFiveMinutes(of: .smelter)
        let ballast = alignmentAfterFiveMinutes(of: .ballast)

        #expect(array > 0.9, "a lone array barely moves the world in five minutes")
        #expect(ballast < 0.3, "a ballast has it most of the way round")
        #expect(array > smelter)
        #expect(smelter > ballast)
    }

    @Test func aSettledWorldStopsQuaking() {
        var world = newWorld()
        world.state.resources = stocked
        for tile in 0..<12 { world.place(.solarArray, on: tile) }
        world.advance(by: 4000)
        #expect(world.wobbleDegrees < World.quakeWobbleThreshold)
        world.state.crackedUntil = [:]
        world.advance(by: 4000)
        #expect(world.state.crackedUntil.isEmpty)
    }

    @Test func steeringHardEnoughCracksTiles() {
        var world = newWorld()
        world.state.resources = stocked
        for tile in 0..<24 { world.place(.ballast, on: tile) }
        // Hold the world far from where it wants to spin, the way a player who
        // keeps rebuilding does, and the ground gives.
        for _ in 0..<40 {
            if let target = world.targetAxis {
                world.state.axis = rotate(target, about: normalize(cross(target, Vec3(0, 0, 1))), by: .pi / 2)
            }
            world.advance(by: 60)
            if !world.state.crackedUntil.isEmpty { break }
        }
        #expect(!world.state.crackedUntil.isEmpty)
    }

    @Test func crackedTilesProduceNothingAndHealOnTheirOwn() {
        var world = newWorld()
        let tile = equatorTile(world)
        world.place(.solarArray, on: tile)
        world.state.crackedUntil[tile] = world.state.elapsed + 300
        #expect(world.readout(for: tile).output.charge == 0)
        world.advance(by: 400)
        #expect(!world.isCracked(tile))
        #expect(world.readout(for: tile).output.charge > 0)
    }

    @Test func meteorsArriveAndCanBeCaught() {
        var world = newWorld()
        let events = world.advance(by: 50)
        #expect(events.meteorSpawned != nil)
        #expect(world.state.activeMeteor != nil)

        let before = world.state.resources.ore
        let reward = world.catchMeteor()
        #expect(reward != nil)
        #expect(world.state.resources.ore == before + reward!)
        #expect(world.state.activeMeteor == nil)
        #expect(world.state.meteorsCaught == 1)
    }

    @Test func uncaughtMeteorsBurnUpWithoutPunishment() {
        var world = newWorld()
        world.advance(by: 50)
        #expect(world.state.activeMeteor != nil)
        let events = world.advance(by: 30)
        #expect(events.meteorMissed)
        #expect(world.state.activeMeteor == nil)
        #expect(world.state.crackedUntil.isEmpty)
    }

    @Test func offlineCatchUpCreditsMeteorsAtAFractionAndIsCapped() {
        var world = newWorld()
        world.place(.oreMine, on: 3)
        let start = world.state.lastPlayed
        let (events, seconds) = world.catchUp(to: start.addingTimeInterval(48 * 3600))
        #expect(seconds == world.offlineCap)
        #expect(events.meteorsAutoCollected > 0)
        #expect(events.autoCollectedOre > 0)
        #expect(world.state.activeMeteor == nil)
    }

    @Test func collapseRequiresMassThenResetsAndRewards() {
        var world = newWorld()
        #expect(!world.canCollapse)
        #expect(world.pendingGravity == 0)

        world.state.resources = stocked
        var tile = 0
        while world.totalMass < world.collapseThreshold, tile < world.geometry.tileCount {
            world.place(.ballast, on: tile)
            tile += 1
        }
        #expect(world.canCollapse)
        let expected = world.pendingGravity
        #expect(expected >= 10)

        let gained = world.collapse(now: Date(timeIntervalSince1970: 5000))
        #expect(gained == expected)
        #expect(world.state.gravity == expected)
        #expect(world.state.collapses == 1)
        #expect(world.state.tier == 1)
        #expect(world.state.buildings.isEmpty)
        #expect(world.geometry.tileCount == 252)
        #expect(world.state.resources == WorldState.startingResources)
        #expect(world.state.firstCollapseSeconds != nil)
        #expect(world.collapseThreshold == 350 * 3.5)
    }

    @Test func gravityImprovesYieldSettlingAndOfflineCap() {
        var world = newWorld()
        let baseline = (world.yieldMultiplier, world.settleRate, world.offlineCap)
        world.state.gravity = 50
        #expect(world.yieldMultiplier > baseline.0)
        #expect(world.settleRate > baseline.1)
        #expect(world.offlineCap > baseline.2)
        #expect(World(state: {
            var state = WorldState(seed: 1)
            state.gravity = 10_000
            return state
        }()).offlineCap == 24 * 3600)
    }

    @Test func simulationIsDeterministicForAGivenSeed() {
        func run() -> WorldState {
            var world = World(state: WorldState(seed: 7, now: Date(timeIntervalSince1970: 0)))
            world.state.resources = stocked
            for tile in [4, 9, 15, 22] { world.place(.solarArray, on: tile) }
            world.place(.ballast, on: 40)
            world.advance(by: 1800)
            world.catchMeteor()
            world.advance(by: 600)
            return world.state
        }
        #expect(run() == run())
    }

    @Test func stateRoundTripsThroughCoding() throws {
        var world = newWorld()
        world.place(.solarArray, on: 11)
        world.advance(by: 300)

        let data = try JSONEncoder().encode(world.state)
        let decoded = try JSONDecoder().decode(WorldState.self, from: data)
        #expect(decoded == world.state)

        let restored = World(state: decoded)
        #expect(restored.geometry.tileCount == world.geometry.tileCount)
        #expect(restored.totalMass == world.totalMass)
    }

    @Test func beaconsLiftTheirNeighbors() {
        var world = newWorld()
        world.state.resources = stocked
        world.state.tier = 1
        let tile = equatorTile(world)
        let plain = world.readout(for: tile)
        world.place(.solarArray, on: tile)
        let solo = world.readout(for: tile).output.charge
        world.place(.beacon, on: world.geometry.tiles[tile].neighbors[0])
        let boosted = world.readout(for: tile).output.charge
        #expect(plain.output.charge == 0)
        #expect(boosted > solo * 1.1)
    }

    @Test func pickingConvertsACameraRayIntoATile() {
        let world = newWorld()
        let tile = world.geometry.tiles[100]
        let hit = world.tile(hitBy: tile.center * 4, direction: -tile.center)
        #expect(hit == 100)
        #expect(world.tile(hitBy: Vec3(0, 0, 4), direction: Vec3(0, 0, 1)) == nil)
    }
}
