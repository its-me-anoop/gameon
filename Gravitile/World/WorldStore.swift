import Foundation
import Observation
import OrbitKit
import QuartzCore
import SwiftUI
import simd

/// What happened while the app was closed. Shown once, on return.
struct WelcomeBack: Equatable {
    var seconds: Double
    var gained: Resources
    var meteors: Int
    var meteorOre: Double
    var axisMovedDegrees: Double
}

/// A short-lived line of text that floats up over the world — placements,
/// meteors, quakes. Kept in the store so the renderer stays stateless.
struct Toast: Identifiable, Equatable {
    let id = UUID()
    var text: String
    var tone: Tone

    enum Tone { case neutral, good, warning }
}

/// The game: owns the world, drives its clock, and translates simulation events
/// into sound, haptics and text.
@Observable @MainActor
final class WorldStore {
    private(set) var world: World
    private(set) var records: Records
    var settings: Settings {
        didSet {
            guard settings != oldValue else { return }
            applySettings()
            storage.save(settings: settings)
        }
    }

    /// The machine the player has picked up, waiting for a tile.
    var armedKind: BuildingKind?
    var selectedTile: Int?
    var camera = CameraRig()
    var welcomeBack: WelcomeBack?
    var toasts: [Toast] = []
    var isShowingCollapse = false
    private(set) var lastCollapseGain: Int?

    let sound = SoundService()
    let haptics = HapticsService()
    let gameCenter = GameCenterService()
    private let storage: WorldStorage

    private var lastTick = CACurrentMediaTime()
    private var secondsSinceSave: Double = 0
    private var ticker: Timer?

    var palette: WorldPalette { Theme.palette(id: settings.themeID) }

    init(storage: WorldStorage = WorldStorage(), state: WorldState? = nil) {
        self.storage = storage
        // UI tests and manual debugging need a guaranteed-fresh world.
        let arguments = ProcessInfo.processInfo.arguments
        if arguments.contains("-gravitile-reset") {
            storage.wipe()
        }
        settings = storage.loadSettings()
        records = storage.loadRecords()
        let isDemo = arguments.contains("-gravitile-demo")
        world = World(state: isDemo ? Self.demoState() : state ?? storage.loadWorld() ?? WorldState())
        // Only now that every stored property is initialized: assigning to
        // `settings` fires its observer, which reads the rest of self.
        if isDemo { settings.hasSeenTutorial = true }
        Theme.current = Theme.palette(id: settings.themeID)
        applySettings()
    }

    // MARK: - Lifecycle

    func start() {
        gameCenter.authenticate()
        catchUp()
        lastTick = CACurrentMediaTime()
        ticker?.invalidate()
        // 20 Hz: the world turns once every two minutes, so this is far smoother
        // than the eye needs while leaving the main thread almost entirely free.
        ticker = Timer.scheduledTimer(withTimeInterval: 1.0 / 20, repeats: true) { [weak self] _ in
            Task { @MainActor in self?.tick() }
        }
    }

    func stop() {
        ticker?.invalidate()
        ticker = nil
        save()
    }

    private func catchUp() {
        let axisBefore = world.state.axis
        let (events, seconds) = world.catchUp(to: Date())
        guard seconds > 60 else { return }
        welcomeBack = WelcomeBack(
            seconds: seconds,
            gained: events.gained,
            meteors: events.meteorsAutoCollected,
            meteorOre: events.autoCollectedOre,
            axisMovedDegrees: angleBetween(axisBefore, world.state.axis) * 180 / .pi
        )
    }

    private func tick() {
        let now = CACurrentMediaTime()
        let delta = min(now - lastTick, 2)
        lastTick = now
        guard delta > 0 else { return }

        let events = world.advance(by: delta)
        world.state.lastPlayed = Date()

        if events.meteorSpawned != nil {
            sound.meteorApproach()
            haptics.approach()
        }
        if !events.quakedTiles.isEmpty {
            sound.quake()
            haptics.quake()
            post("Quake — \(events.quakedTiles.count) machines cracked", tone: .warning)
        }

        secondsSinceSave += delta
        if secondsSinceSave > 8 {
            save()
            secondsSinceSave = 0
        }
    }

    func save() {
        records.absorb(world)
        storage.save(world: world.state)
        storage.save(records: records)
        gameCenter.submit(records: records)
    }

    private func applySettings() {
        Theme.current = Theme.palette(id: settings.themeID)
        sound.isEnabled = settings.soundOn
        sound.isMusicEnabled = settings.musicOn
        haptics.isEnabled = settings.hapticsOn
    }

    // MARK: - Player actions

    /// A tap on the world. With a machine armed this places it; otherwise it
    /// selects the tile and opens its readout.
    func tapped(tile: Int) {
        if let kind = armedKind {
            place(kind, on: tile)
            return
        }
        if world.isCracked(tile) {
            world.repair(tile: tile)
            sound.repair()
            haptics.tap()
            post("Repaired", tone: .good)
            return
        }
        selectedTile = selectedTile == tile ? nil : tile
        sound.tap()
        haptics.tap()
    }

    func place(_ kind: BuildingKind, on tile: Int) {
        guard world.state.buildings[tile] == nil else {
            post("That ground is taken", tone: .warning)
            sound.denied()
            return
        }
        guard world.canPlace(kind, on: tile) else {
            post("Not enough \(shortfall(for: world.placementCost(kind)))", tone: .warning)
            sound.denied()
            return
        }
        let wobbleBefore = world.wobbleDegrees
        world.place(kind, on: tile)
        selectedTile = tile
        sound.place()
        haptics.place()
        save()

        let wobble = world.wobbleDegrees
        if wobble > wobbleBefore + 6 {
            post("Wobble \(Int(wobble))° — the world is turning", tone: .neutral)
        }
    }

    func upgradeSelected() {
        guard let tile = selectedTile, let building = world.state.buildings[tile] else { return }
        guard world.canUpgrade(tile: tile) else {
            post("Not enough \(shortfall(for: building.upgradeCost))", tone: .warning)
            sound.denied()
            return
        }
        world.upgrade(tile: tile)
        sound.upgrade()
        haptics.place()
        save()
    }

    func demolishSelected() {
        guard let tile = selectedTile else { return }
        world.demolish(tile: tile)
        sound.demolish()
        haptics.tap()
        save()
    }

    func catchMeteor() {
        guard let reward = world.catchMeteor() else { return }
        sound.meteorCatch()
        haptics.impact()
        post("+\(Int(reward)) ore", tone: .good)
        save()
    }

    func collapse() {
        guard world.canCollapse else { return }
        let gained = world.collapse()
        lastCollapseGain = gained
        selectedTile = nil
        armedKind = nil
        sound.collapse()
        haptics.collapse()
        save()
    }

    /// Which resource is missing, for a message that says something useful.
    private func shortfall(for cost: Resources) -> String {
        let missing = ResourceKind.allCases
            .filter { world.state.resources[$0] < cost[$0] }
            .map(\.displayName)
        return missing.isEmpty ? "resources" : missing.joined(separator: " and ")
    }

    func post(_ text: String, tone: Toast.Tone) {
        let toast = Toast(text: text, tone: tone)
        toasts.append(toast)
        Task { @MainActor in
            try? await Task.sleep(for: .seconds(2.4))
            toasts.removeAll { $0.id == toast.id }
        }
    }

    // MARK: - Readouts for the interface

    var selectedReadout: TileReadout? {
        selectedTile.map { world.readout(for: $0) }
    }

    var netRates: Resources { world.netRates() }

    /// Machines the player can pick up right now, in tray order.
    var availableKinds: [BuildingKind] {
        BuildingKind.allCases.filter { world.isUnlocked($0) }
    }

    /// A believable mid-game world for screenshots and for looking at the thing
    /// while building it. Machines go where the climate suits them, and a
    /// cluster of ballast sits off-axis so the world is visibly mid-turn.
    static func demoState() -> WorldState {
        var state = WorldState(seed: 20_260_725)
        state.gravity = 24
        state.resources = Resources(ore: 1840, charge: 1310, water: 640, alloy: 420)
        var world = World(state: state)

        let climate = world.climate
        func rank(_ kind: BuildingKind) -> [Int] {
            world.geometry.tiles
                .filter { world.state.buildings[$0.index] == nil }
                .sorted {
                    kind.efficiency(
                        temperatureIndex: climate.temperatureIndex(at: $0.center),
                        solarFactor: climate.solarFactor(at: $0.center)
                    ) > kind.efficiency(
                        temperatureIndex: climate.temperatureIndex(at: $1.center),
                        solarFactor: climate.solarFactor(at: $1.center)
                    )
                }
                .map(\.index)
        }

        for kind in [BuildingKind.solarArray, .oreMine, .condenser, .smelter] {
            let count = kind == .solarArray ? 14 : kind == .oreMine ? 12 : 6
            for tile in rank(kind).prefix(count) {
                world.state.buildings[tile] = Building(kind: kind, level: kind == .solarArray ? 2 : 1)
            }
        }
        // Ballast on one flank: the world is caught mid-turn, which is the
        // whole game in a single frame.
        let flank = world.geometry.tiles
            .filter { world.state.buildings[$0.index] == nil }
            .sorted { dot($0.center, world.state.axis) > dot($1.center, world.state.axis) }
            .prefix(5)
        for tile in flank {
            world.state.buildings[tile.index] = Building(kind: .ballast, level: 1)
        }

        world.state.placements = world.state.buildings.count
        world.state.meteorsCaught = 37
        return world.state
    }

    func reset() {
        storage.wipe()
        settings = Settings()
        records = Records()
        world = World(state: WorldState())
        selectedTile = nil
        armedKind = nil
    }
}
