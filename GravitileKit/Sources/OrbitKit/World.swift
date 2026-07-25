import Foundation
import simd

/// A live world: persistent state plus the derived geometry, and every rule
/// that moves it forward. Pure Swift and fully deterministic — the renderer
/// reads from it and never writes simulation state back.
public struct World: Sendable {
    public private(set) var geometry: PlanetGeometry
    public var state: WorldState

    /// Tile counts per collapse tier (`10f²+2`): 162 → 252 → 362 → 492.
    public static let frequencies = [4, 5, 6, 7]

    public static func frequency(forTier tier: Int) -> Int {
        frequencies[min(max(tier, 0), frequencies.count - 1)]
    }

    public init(state: WorldState = WorldState()) {
        self.state = state
        geometry = PlanetGeometry.generate(frequency: Self.frequency(forTier: state.tier))
    }

    // MARK: - Derived world properties

    /// Star direction is fixed in world space; the axis is what moves.
    public static let starDirection = Vec3(1, 0, 0)

    /// Seconds per rotation. Short enough to see the world turn, long enough
    /// that the terminator sweep is calm rather than strobing.
    public static let dayLength: Double = 120

    public var climate: Climate {
        Climate(
            axis: state.axis,
            starDirection: Self.starDirection,
            greenhouse: min(0.12, 0.0015 * Double(state.gravity))
        )
    }

    /// Tiles carrying a building, in index order.
    ///
    /// Every sum over the world walks this, never the dictionary: hash order is
    /// an implementation detail, and floating-point addition is not
    /// associative, so iterating a dictionary would make the last few bits of
    /// the spin axis depend on Swift's bucket layout. Determinism is a
    /// promise this engine keeps.
    public var builtTiles: [Int] {
        (0..<geometry.tileCount).filter { state.buildings[$0] != nil }
    }

    /// Total mass of everything built — the world's score, and the input to
    /// both the axis solver and the collapse threshold.
    public var totalMass: Double {
        builtTiles.reduce(0) { $0 + (state.buildings[$1]?.mass ?? 0) }
    }

    /// Mass the planet carries in its own equatorial bulge, in building-mass
    /// units. A spinning body bulges around its current equator, and that bulge
    /// resists reorientation.
    ///
    /// It does not veto steering — the bulge re-forms around whatever axis the
    /// world ends up with, so any off-axis mass eventually reaches the equator.
    /// What it sets is the *rate*, and that is the pacing the game wants: a
    /// lone Solar Array (mass 1) needs half an hour to swing a world, while a
    /// Ballast (18) does it in five minutes. Weight is how fast you steer.
    public static let crustBulge: Double = 12

    public var massCovariance: Symmetric3 {
        var covariance = Symmetric3.zero
        for tile in builtTiles {
            guard let building = state.buildings[tile] else { continue }
            covariance.addOuterProduct(of: geometry.tiles[tile].center, weight: building.mass)
        }
        covariance.addOblateness(axis: state.axis, weight: Self.crustBulge / 2)
        return covariance
    }

    /// Where the mass distribution wants the world to spin. `nil` while the
    /// world is empty or perfectly balanced.
    public var targetAxis: Vec3? {
        AxisDynamics.targetAxis(covariance: massCovariance, currentAxis: state.axis)
    }

    public var wobbleDegrees: Double {
        AxisDynamics.wobbleDegrees(axis: state.axis, target: targetAxis)
    }

    /// Reciprocal settling time. Gravity makes worlds respond faster, which is
    /// a real quality-of-life reward: late worlds re-aim in seconds.
    public var settleRate: Double {
        (1.0 / 90.0) * (1 + 0.015 * Double(state.gravity))
    }

    public var yieldMultiplier: Double {
        1 + 0.02 * Double(state.gravity)
    }

    public var storageCaps: Resources {
        let scale = (1 + 0.03 * Double(state.gravity)) * pow(1.8, Double(state.tier))
        return Resources(
            ore: 2500, charge: 2000, water: 1500, alloy: 800, biomass: 500
        ) * scale
    }

    public var offlineCap: Double {
        min(24 * 3600, 8 * 3600 + 360 * Double(state.gravity))
    }

    public func terrain(for tile: Int) -> TileTerrain {
        TileTerrain.derive(seed: state.seed, tile: tile)
    }

    public func isCracked(_ tile: Int) -> Bool {
        (state.crackedUntil[tile] ?? 0) > state.elapsed
    }

    // MARK: - Production

    /// Sum of Beacon bonuses reaching a tile from its neighbors.
    func neighborBonus(for tile: Int) -> Double {
        geometry.tiles[tile].neighbors.reduce(0) { total, neighbor in
            guard let building = state.buildings[neighbor], !isCracked(neighbor) else { return total }
            return total + building.kind.neighborBonus(atLevel: building.level)
        }
    }

    /// Everything the inspector and renderer need about one tile.
    public func readout(for tile: Int) -> TileReadout {
        let climate = self.climate
        let center = geometry.tiles[tile].center
        let temperature = climate.temperatureIndex(at: center)
        let solar = climate.solarFactor(at: center)
        let terrain = self.terrain(for: tile)
        let building = state.buildings[tile]
        let cracked = isCracked(tile)

        var output = Resources.zero
        var input = Resources.zero
        var multiplier = 0.0

        if let building, !cracked {
            multiplier = grossMultiplier(
                building: building, tile: tile, temperature: temperature,
                solar: solar, terrain: terrain
            )
            if let base = building.kind.baseOutput {
                output[base.kind] = base.rate * multiplier
            }
            input = building.kind.baseInput * building.kind.levelScale(building.level)
        }

        return TileReadout(
            tile: tile,
            building: building,
            terrain: terrain,
            temperatureIndex: temperature,
            solarFactor: solar,
            biome: Climate.biome(temperatureIndex: temperature),
            yieldMultiplier: multiplier,
            output: output,
            input: input,
            isCracked: cracked
        )
    }

    private func grossMultiplier(
        building: Building, tile: Int, temperature: Double,
        solar: Double, terrain: TileTerrain
    ) -> Double {
        let efficiency = building.kind.efficiency(temperatureIndex: temperature, solarFactor: solar)
        // Tiles differ slightly in area (twelve pentagons in every tiling);
        // scaling by area keeps them worth the same per unit of surface.
        let averageArea = 4 * Double.pi / Double(geometry.tileCount)
        let areaFactor = geometry.tiles[tile].area / averageArea

        var terrainFactor = 1.0
        switch building.kind {
        case .oreMine:
            terrainFactor = terrain.oreRichness * (1 + 0.2 * Double(state.tier))
        case .condenser:
            terrainFactor = terrain.hasIce ? 1.3 : 1.0
        default:
            break
        }

        return efficiency
            * terrainFactor
            * areaFactor
            * building.kind.levelScale(building.level)
            * (1 + neighborBonus(for: tile))
            * yieldMultiplier
    }

    /// Steady-state production and consumption of the whole world, per second,
    /// before starvation is applied. Shown in the HUD as the live rate.
    public func grossRates() -> (output: Resources, input: Resources) {
        var output = Resources.zero
        var input = Resources.zero
        for tile in builtTiles {
            let readout = self.readout(for: tile)
            output = output + readout.output
            input = input + readout.input
        }
        return (output, input)
    }

    /// Net per-second rate after starvation — what the HUD's arrows show.
    public func netRates() -> Resources {
        let flow = resolveFlow(stock: state.resources, dt: 1)
        return flow.net
    }

    /// Resolves the production graph for one step. Raw producers run first,
    /// then each consumer is scaled by the fraction of its inputs that actually
    /// exist, so a starved smelter slows down instead of going negative.
    func resolveFlow(stock: Resources, dt: Double) -> (net: Resources, scales: [Int: Double]) {
        var chargeIncome = 0.0
        var oreIncome = 0.0
        var chargeDemand = 0.0
        var oreDemand = 0.0
        var waterDemand = 0.0

        var readouts: [TileReadout] = []
        readouts.reserveCapacity(state.buildings.count)

        for tile in builtTiles {
            let readout = self.readout(for: tile)
            readouts.append(readout)
            chargeIncome += readout.output.charge
            oreIncome += readout.output.ore
            chargeDemand += readout.input.charge
            oreDemand += readout.input.ore
            waterDemand += readout.input.water
        }

        func ratio(available: Double, demand: Double) -> Double {
            demand <= 1e-12 ? 1 : min(1, max(0, available / demand))
        }

        let chargeRatio = ratio(
            available: chargeIncome + stock.charge / max(dt, 1e-6), demand: chargeDemand
        )
        let oreRatio = ratio(
            available: oreIncome + stock.ore / max(dt, 1e-6), demand: oreDemand
        )

        // Water exists only after condensers have run at their charge-limited
        // rate, so greenhouses see the throttled supply.
        var waterIncome = 0.0
        for readout in readouts where readout.building?.kind == .condenser {
            waterIncome += readout.output.water * chargeRatio
        }
        let waterRatio = ratio(
            available: waterIncome + stock.water / max(dt, 1e-6), demand: waterDemand
        )

        var net = Resources.zero
        var scales: [Int: Double] = [:]
        scales.reserveCapacity(readouts.count)

        for readout in readouts {
            guard let kind = readout.building?.kind else { continue }
            let scale: Double
            switch kind {
            case .solarArray, .oreMine, .beacon, .ballast:
                scale = 1
            case .condenser:
                scale = chargeRatio
            case .smelter:
                scale = min(chargeRatio, oreRatio)
            case .greenhouse:
                scale = min(chargeRatio, waterRatio)
            }
            scales[readout.tile] = scale
            net = net + readout.output * scale - readout.input * scale
        }

        return (net, scales)
    }

    // MARK: - Building

    public func isUnlocked(_ kind: BuildingKind) -> Bool {
        state.tier >= kind.unlockTier || state.collapses >= kind.unlockTier
    }

    public func placementCost(_ kind: BuildingKind) -> Resources {
        kind.cost(forLevel: 1)
    }

    public func canPlace(_ kind: BuildingKind, on tile: Int) -> Bool {
        guard tile >= 0, tile < geometry.tileCount else { return false }
        guard state.buildings[tile] == nil, isUnlocked(kind) else { return false }
        return state.resources.covers(placementCost(kind))
    }

    @discardableResult
    public mutating func place(_ kind: BuildingKind, on tile: Int) -> Bool {
        guard canPlace(kind, on: tile) else { return false }
        state.resources = state.resources - placementCost(kind)
        state.buildings[tile] = Building(kind: kind)
        state.placements += 1
        refreshPeakMass()
        return true
    }

    public func canUpgrade(tile: Int) -> Bool {
        guard let building = state.buildings[tile] else { return false }
        return state.resources.covers(building.upgradeCost)
    }

    @discardableResult
    public mutating func upgrade(tile: Int) -> Bool {
        guard var building = state.buildings[tile], canUpgrade(tile: tile) else { return false }
        state.resources = state.resources - building.upgradeCost
        building.level += 1
        state.buildings[tile] = building
        refreshPeakMass()
        return true
    }

    /// Removing a building refunds half its build cost. Demolition is a
    /// steering tool too: taking mass off one side moves the axis as surely as
    /// adding it to the other.
    @discardableResult
    public mutating func demolish(tile: Int) -> Bool {
        guard let building = state.buildings[tile] else { return false }
        var refund = Resources.zero
        for level in 1...building.level {
            refund = refund + building.kind.cost(forLevel: level) * 0.5
        }
        state.buildings[tile] = nil
        state.resources = state.resources + refund
        state.resources.clamp(to: storageCaps)
        return true
    }

    @discardableResult
    public mutating func repair(tile: Int) -> Bool {
        guard isCracked(tile) else { return false }
        state.crackedUntil[tile] = nil
        return true
    }

    private mutating func refreshPeakMass() {
        let mass = totalMass
        state.peakMass = max(state.peakMass, mass)
        state.lifetimePeakMass = max(state.lifetimePeakMass, mass)
    }

    // MARK: - Meteors

    /// Collect the meteor on approach. Returns the ore gained, or `nil` if
    /// there was nothing to catch.
    @discardableResult
    public mutating func catchMeteor() -> Double? {
        guard let meteor = state.activeMeteor else { return nil }
        state.activeMeteor = nil
        state.meteorsCaught += 1
        state.resources.ore += meteor.reward
        state.resources.clamp(to: storageCaps)
        scheduleNextMeteor()
        return meteor.reward
    }

    private mutating func scheduleNextMeteor() {
        state.nextMeteorAt = state.elapsed + state.rng.double(in: 100...220)
    }

    private var meteorReward: Double {
        60 * (1 + 0.25 * Double(state.tier)) * (1 + 0.02 * Double(state.gravity))
    }

    // MARK: - Simulation

    public enum TickMode: Sendable {
        /// The player is watching: meteors wait to be tapped.
        case foreground
        /// Catching up after a relaunch: meteors are auto-collected at a
        /// fraction of their value, so being away is never free but never
        /// punishing either.
        case offline
    }

    public static let offlineMeteorFraction = 0.4

    /// Advance the world. Long stretches are broken into sub-steps so the axis
    /// drift, the climate it changes, and the production that depends on the
    /// climate all stay coupled.
    @discardableResult
    public mutating func advance(by seconds: Double, mode: TickMode = .foreground) -> TickEvents {
        guard seconds > 0 else { return TickEvents() }

        let preferredStep: Double = mode == .offline ? 60 : 1
        let stepCount = min(720, max(1, Int((seconds / preferredStep).rounded(.up))))
        let dt = seconds / Double(stepCount)

        var events = TickEvents()
        for _ in 0..<stepCount {
            step(dt: dt, mode: mode, events: &events)
        }
        refreshPeakMass()
        return events
    }

    private mutating func step(dt: Double, mode: TickMode, events: inout TickEvents) {
        state.elapsed += dt
        state.lifetimeElapsed += dt
        state.spinPhase = (state.spinPhase + 2 * .pi * dt / Self.dayLength)
            .truncatingRemainder(dividingBy: 2 * .pi)

        // 1. The axis drifts toward what the mass distribution wants.
        if let target = targetAxis {
            let before = wobbleDegrees
            state.axis = AxisDynamics.settle(
                axis: state.axis, toward: target, rate: settleRate, dt: dt
            )
            state.maxSettledWobble = max(state.maxSettledWobble, before)
        }

        // 2. Production, under the climate the new axis produces.
        let flow = resolveFlow(stock: state.resources, dt: dt)
        let before = state.resources
        state.resources = state.resources + flow.net * dt
        state.resources.clamp(to: storageCaps)
        for kind in ResourceKind.allCases {
            events.gained[kind] += max(0, state.resources[kind] - before[kind])
        }

        // 3. Cracked tiles heal on their own.
        for tile in state.crackedUntil.keys.sorted()
        where (state.crackedUntil[tile] ?? 0) <= state.elapsed {
            state.crackedUntil[tile] = nil
            events.repairedTiles.append(tile)
        }

        // 4. Meteors.
        if let meteor = state.activeMeteor, state.elapsed >= meteor.arrivesAt {
            state.activeMeteor = nil
            events.meteorMissed = true
            scheduleNextMeteor()
        }
        if state.activeMeteor == nil, state.elapsed >= state.nextMeteorAt {
            let target = Int(state.rng.next() % UInt64(max(1, geometry.tileCount)))
            let meteor = Meteor(
                id: state.rng.next(),
                targetTile: target,
                spawnedAt: state.elapsed,
                arrivesAt: state.elapsed + 7,
                reward: meteorReward
            )
            switch mode {
            case .foreground:
                state.activeMeteor = meteor
                events.meteorSpawned = meteor
            case .offline:
                let ore = meteor.reward * Self.offlineMeteorFraction
                state.resources.ore += ore
                state.resources.clamp(to: storageCaps)
                events.meteorsAutoCollected += 1
                events.autoCollectedOre += ore
                scheduleNextMeteor()
            }
        }

        // 5. Quakes: the price of steering hard.
        if state.elapsed >= state.nextQuakeCheckAt {
            state.nextQuakeCheckAt = state.elapsed + 60
            let wobble = wobbleDegrees
            if wobble > Self.quakeWobbleThreshold, !state.buildings.isEmpty {
                let chance = min(0.5, (wobble - Self.quakeWobbleThreshold) / 120)
                if state.rng.unitDouble() < chance {
                    let candidates = builtTiles
                    for _ in 0..<min(2, candidates.count) {
                        let pick = candidates[Int(state.rng.next() % UInt64(candidates.count))]
                        state.crackedUntil[pick] = state.elapsed + 300
                        events.quakedTiles.append(pick)
                    }
                }
            }
        }
    }

    public static let quakeWobbleThreshold: Double = 25

    /// Catch up after the app was closed. Returns the events plus the clamped
    /// duration, so the welcome-back panel can say how much was credited.
    public mutating func catchUp(to now: Date) -> (events: TickEvents, seconds: Double) {
        let raw = now.timeIntervalSince(state.lastPlayed)
        state.lastPlayed = now
        guard raw > 1 else { return (TickEvents(), 0) }
        let seconds = min(raw, offlineCap)
        let events = advance(by: seconds, mode: .offline)
        return (events, seconds)
    }

    // MARK: - Collapse

    /// Mass needed to collapse. The step per tier is deliberately gentler than
    /// the growth a bigger world affords, so later collapses arrive faster in
    /// real terms rather than turning into a wall.
    public var collapseThreshold: Double {
        350 * pow(3.5, Double(state.tier))
    }

    public var collapseProgress: Double {
        min(1, totalMass / collapseThreshold)
    }

    public var canCollapse: Bool { totalMass >= collapseThreshold }

    /// Gravity that collapsing right now would grant. Sub-linear in mass, so
    /// overshooting the threshold is worth something but never everything.
    /// Banked Biomass is folded in as a bonus, which is what makes the
    /// greenhouse chain worth building once it unlocks.
    public var pendingGravity: Int {
        guard canCollapse else { return 0 }
        let massTerm = pow(totalMass / collapseThreshold, 0.6)
        let biomassBonus = 1 + state.resources.biomass / 500
        return max(1, Int(10 * massTerm * biomassBonus))
    }

    /// Compress the world: the surface is lost, Gravity is permanent, and the
    /// next world is bigger and richer.
    @discardableResult
    public mutating func collapse(now: Date = Date()) -> Int {
        guard canCollapse else { return 0 }
        let gained = pendingGravity

        if state.firstCollapseSeconds == nil {
            state.firstCollapseSeconds = state.lifetimeElapsed
        }
        state.gravity += gained
        state.lifetimeGravity += gained
        state.collapses += 1
        state.tier += 1
        state.buildings = [:]
        state.crackedUntil = [:]
        state.activeMeteor = nil
        state.resources = WorldState.startingResources
        state.elapsed = 0
        state.peakMass = 0
        state.nextMeteorAt = 45
        state.nextQuakeCheckAt = 60
        state.rng = SplitMix64(seed: state.seed &+ UInt64(state.collapses) &* 0x9E37_79B9)
        state.lastPlayed = now

        geometry = PlanetGeometry.generate(frequency: Self.frequency(forTier: state.tier))
        return gained
    }

    // MARK: - Picking

    /// Tile under a camera ray, for tap-to-place. Analytic, so it needs no
    /// physics bodies and can be tested without a renderer.
    public func tile(hitBy origin: Vec3, direction: Vec3, radius: Double = 1) -> Int? {
        guard let hit = raySphereHit(origin: origin, direction: direction, radius: radius) else {
            return nil
        }
        return geometry.nearestTile(to: hit)
    }
}
