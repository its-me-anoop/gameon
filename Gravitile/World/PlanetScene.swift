import Foundation
import OrbitKit
import RealityKit
import SwiftUI
import simd

/// Owns the RealityKit entity tree and keeps it in step with the simulation.
///
/// The simulation never reads from here. This class only ever asks the world
/// what it looks like now and moves entities to match, which keeps the physics
/// testable and the renderer replaceable.
@MainActor
final class PlanetScene {
    let root = Entity()

    /// Carries the tiles and machines, and spins with the world.
    private let planetRoot = Entity()
    /// Aligned with the spin axis; holds the shaft and the frost rings. It does
    /// not spin, because the axis is what the spin is *about*.
    private let axisRig = Entity()
    /// Where the mass distribution wants the axis to be — the single piece of
    /// the interface that makes the mechanic legible.
    private let ghostRig = Entity()
    private let camera = PerspectiveCamera()
    private let sunlight = DirectionalLight()
    private let star = ModelEntity()
    private let meteor = ModelEntity()
    private var sky: ModelEntity?
    private var starLayers: [ModelEntity] = []

    private var tileEntities: [ModelEntity] = []
    private var machineEntities: [Int: ModelEntity] = [:]
    private var frostRings: [ModelEntity] = []

    private var biomeMaterials: [Biome: PhysicallyBasedMaterial] = [:]
    private var machineMaterials: [BuildingKind: PhysicallyBasedMaterial] = [:]
    private var selectedMaterial = PhysicallyBasedMaterial()
    private var crackedMaterial = PhysicallyBasedMaterial()

    private var tileBiomes: [Biome?] = []
    private var lastClimateAxis = Vec3(0, 1, 0)
    private var lastFrostAxis = Vec3(0, 1, 0)
    private var lastFrostLatitudes: [Double] = []
    private var lastSelection: Int?
    private var machineSignature = 0
    private var builtTier = -1
    private var paletteID = ""

    // MARK: - Setup

    func attach(to content: inout RealityViewCameraContent, world: World, palette: WorldPalette) {
        root.addChild(planetRoot)
        root.addChild(axisRig)
        root.addChild(ghostRig)
        root.addChild(star)
        root.addChild(sunlight)
        root.addChild(meteor)

        camera.camera.fieldOfViewInDegrees = 45
        camera.camera.near = 0.05
        camera.camera.far = 400
        root.addChild(camera)

        sunlight.light.intensity = 17_000
        sunlight.light.isRealWorldProxy = false
        sunlight.look(
            at: .zero, from: SIMD3<Float>(World.starDirection) * 20,
            relativeTo: nil
        )

        content.camera = .virtual
        content.entities.append(root)

        rebuild(world: world, palette: palette)
    }

    // MARK: - Full rebuild

    /// Rebuilds everything that only changes when the world itself changes:
    /// after a collapse (a new tiling) or a theme switch (new colors).
    func rebuild(world: World, palette: WorldPalette) {
        builtTier = world.state.tier
        paletteID = palette.id
        makeMaterials(palette)

        planetRoot.children.removeAll()
        tileEntities.removeAll()
        machineEntities.removeAll()
        frostRings.forEach { $0.removeFromParent() }
        frostRings.removeAll()
        tileBiomes = Array(repeating: nil, count: world.geometry.tileCount)

        // The crust, visible through the seams between tiles.
        var crust = PhysicallyBasedMaterial()
        crust.baseColor = .init(tint: UIColor(palette.crust))
        crust.roughness = 0.95
        crust.metallic = 0.0
        crust.emissiveColor = .init(color: UIColor(palette.crust))
        crust.emissiveIntensity = 0.28
        let crustEntity = ModelEntity(
            mesh: .generateSphere(radius: PlanetMesh.crustRadius), materials: [crust]
        )
        planetRoot.addChild(crustEntity)

        for tile in world.geometry.tiles {
            let terrain = world.terrain(for: tile.index)
            let height = Float(0.004 + terrain.roughness * 0.018)
            let entity = ModelEntity(
                mesh: PlanetMesh.tile(tile, height: height),
                materials: [biomeMaterials[.temperate] ?? PhysicallyBasedMaterial()]
            )
            planetRoot.addChild(entity)
            tileEntities.append(entity)
        }

        buildStarAndSky(world: world, palette: palette)
        machineSignature = 0
        lastClimateAxis = .zero
        lastFrostAxis = .zero
        syncMachines(world: world)
        syncClimate(world: world)
        syncFrostRings(world: world, palette: palette)
    }

    private func makeMaterials(_ palette: WorldPalette) {
        func ground(_ color: Color, emissive: Float) -> PhysicallyBasedMaterial {
            var material = PhysicallyBasedMaterial()
            let uiColor = UIColor(color)
            material.baseColor = .init(tint: uiColor)
            material.roughness = 0.88
            material.metallic = 0.0
            // A little emission so the night side stays readable instead of
            // going to a flat black the player cannot plan on.
            material.emissiveColor = .init(color: uiColor)
            material.emissiveIntensity = emissive
            return material
        }

        biomeMaterials = Dictionary(
            uniqueKeysWithValues: Biome.allCases.map { ($0, ground(palette.color(for: $0), emissive: 0.085)) }
        )
        machineMaterials = Dictionary(
            uniqueKeysWithValues: BuildingKind.allCases.map { kind in
                var material = ground(palette.color(for: kind), emissive: 0.3)
                material.roughness = 0.4
                material.metallic = 0.35
                return (kind, material)
            }
        )
        selectedMaterial = ground(palette.accent, emissive: 0.75)
        crackedMaterial = ground(palette.warning, emissive: 0.4)
    }

    private func buildStarAndSky(world: World, palette: WorldPalette) {
        var starMaterial = UnlitMaterial(color: UIColor(palette.starTint))
        starMaterial.color = .init(tint: UIColor(palette.starTint))
        star.model = ModelComponent(mesh: .generateSphere(radius: 2.6), materials: [starMaterial])
        star.position = SIMD3<Float>(World.starDirection) * 46

        var meteorMaterial = UnlitMaterial(color: UIColor(palette.warning))
        meteorMaterial.color = .init(tint: UIColor(palette.warning))
        meteor.model = ModelComponent(mesh: .generateSphere(radius: 0.045), materials: [meteorMaterial])
        meteor.isEnabled = false

        sky?.removeFromParent()
        starLayers.forEach { $0.removeFromParent() }
        starLayers.removeAll()

        let dome = ModelEntity(mesh: PlanetMesh.skyDome(radius: 120), materials: [
            starfieldMaterial(seed: world.state.seed, palette: palette),
        ])
        root.addChild(dome)
        sky = dome

        for layer in PlanetMesh.starLayers(seed: world.state.seed, radius: 90) {
            var material = UnlitMaterial(
                color: UIColor(palette.textPrimary.opacity(layer.brightness))
            )
            material.faceCulling = .none
            let entity = ModelEntity(mesh: layer.mesh, materials: [material])
            root.addChild(entity)
            starLayers.append(entity)
        }

        axisRig.children.removeAll()
        ghostRig.children.removeAll()

        var shaftMaterial = UnlitMaterial(color: UIColor(palette.accent))
        shaftMaterial.color = .init(tint: UIColor(palette.accent.opacity(0.85)))
        let shaft = ModelEntity(
            mesh: .generateCylinder(height: 3.3, radius: 0.008), materials: [shaftMaterial]
        )
        axisRig.addChild(shaft)
        for end in [Float(1.65), -1.65] {
            let cap = ModelEntity(
                mesh: .generateSphere(radius: 0.026), materials: [shaftMaterial]
            )
            cap.position = SIMD3<Float>(0, end, 0)
            axisRig.addChild(cap)
        }

        // The ghost has to be legible at a glance — it is the only thing on
        // screen that says "your world is turning, and this is where to".
        var ghostMaterial = UnlitMaterial(color: UIColor(palette.textPrimary.opacity(0.8)))
        ghostMaterial.color = .init(tint: UIColor(palette.textPrimary.opacity(0.8)))
        let ghost = ModelEntity(
            mesh: .generateCylinder(height: 3.05, radius: 0.0075), materials: [ghostMaterial]
        )
        ghostRig.addChild(ghost)
        for end in [Float(1.52), -1.52] {
            let cap = ModelEntity(
                mesh: .generateSphere(radius: 0.021), materials: [ghostMaterial]
            )
            cap.position = SIMD3<Float>(0, end, 0)
            ghostRig.addChild(cap)
        }
    }

    private func starfieldMaterial(seed: UInt64, palette: WorldPalette) -> RealityKit.Material {
        var material: UnlitMaterial
        if let texture = PlanetMesh.skyTexture(
            seed: seed,
            space: UIColor(palette.space),
            glow: UIColor(palette.spaceGlow),
            star: UIColor(palette.textPrimary)
        ) {
            material = UnlitMaterial(texture: texture)
        } else {
            material = UnlitMaterial(color: UIColor(palette.space))
        }
        // The camera sits inside this sphere, so the faces it can see are the
        // back ones. Disabling culling outright is cheaper than being clever
        // about winding for a mesh drawn once.
        material.faceCulling = .none
        return material
    }

    // MARK: - Per-frame sync

    func update(
        world: World, palette: WorldPalette, selection: Int?, rig: CameraRig
    ) {
        if world.state.tier != builtTier || palette.id != paletteID {
            rebuild(world: world, palette: palette)
        }

        let axis = SIMD3<Float>(world.state.axis)
        planetRoot.transform.rotation = simd_quatf(angle: Float(world.state.spinPhase), axis: axis)
        axisRig.transform.rotation = simd_quatf(from: SIMD3<Float>(0, 1, 0), to: axis)
        if let target = world.targetAxis {
            ghostRig.isEnabled = world.wobbleDegrees > 1.5
            ghostRig.transform.rotation = simd_quatf(
                from: SIMD3<Float>(0, 1, 0), to: SIMD3<Float>(target)
            )
        } else {
            ghostRig.isEnabled = false
        }

        syncMachines(world: world)
        syncClimate(world: world)
        syncFrostRings(world: world, palette: palette)
        syncSelection(selection)
        syncMeteor(world: world)

        camera.look(
            at: .zero, from: SIMD3<Float>(rig.position),
            upVector: SIMD3<Float>(0, 1, 0), relativeTo: nil
        )
    }

    /// Repaints tiles whose climate band changed. Recomputing every tile every
    /// frame would be wasted work — the axis moves slowly, so this only runs
    /// once the world has actually turned far enough to matter.
    private func syncClimate(world: World) {
        guard dot(world.state.axis, lastClimateAxis) < 0.99999 else { return }
        lastClimateAxis = world.state.axis

        let climate = world.climate
        for tile in world.geometry.tiles {
            let biome = climate.biome(at: tile.center)
            guard tileBiomes[tile.index] != biome else { continue }
            tileBiomes[tile.index] = biome
            if lastSelection != tile.index, !world.isCracked(tile.index) {
                tileEntities[tile.index].model?.materials = [
                    biomeMaterials[biome] ?? PhysicallyBasedMaterial(),
                ]
            }
        }
    }

    private func syncFrostRings(world: World, palette: WorldPalette) {
        guard dot(world.state.axis, lastFrostAxis) < 0.9999 else { return }
        lastFrostAxis = world.state.axis

        let latitudes = world.climate.frostLatitudes()
        guard latitudes != lastFrostLatitudes else { return }
        lastFrostLatitudes = latitudes

        frostRings.forEach { $0.removeFromParent() }
        frostRings.removeAll()

        var material = UnlitMaterial(color: UIColor(palette.color(for: .frozen)))
        material.color = .init(tint: UIColor(palette.color(for: .frozen).opacity(0.9)))
        for latitude in latitudes {
            let ring = ModelEntity(
                mesh: PlanetMesh.latitudeRing(
                    latitude: latitude, radius: PlanetMesh.surfaceRadius + 0.026, width: 0.012
                ),
                materials: [material]
            )
            axisRig.addChild(ring)
            frostRings.append(ring)
        }
    }

    private func syncMachines(world: World) {
        var signature = Hasher()
        for tile in world.builtTiles {
            signature.combine(tile)
            signature.combine(world.state.buildings[tile]?.kind)
            signature.combine(world.state.buildings[tile]?.level)
        }
        let value = signature.finalize()
        guard value != machineSignature else { return }
        machineSignature = value

        let wanted = Set(world.builtTiles)
        for (tile, entity) in machineEntities where !wanted.contains(tile) {
            entity.removeFromParent()
            machineEntities[tile] = nil
        }

        for tile in world.builtTiles {
            guard let building = world.state.buildings[tile] else { continue }
            let center = world.geometry.tiles[tile].center
            let terrain = world.terrain(for: tile)
            let ground = PlanetMesh.surfaceRadius + Float(0.004 + terrain.roughness * 0.018)
            // Levels read as height: a level-4 machine is visibly a tower.
            let scale = Float(1 + 0.16 * Double(building.level - 1))

            let entity: ModelEntity
            if let existing = machineEntities[tile], existing.name == building.kind.rawValue {
                entity = existing
            } else {
                machineEntities[tile]?.removeFromParent()
                entity = ModelEntity(
                    mesh: PlanetMesh.building(building.kind),
                    materials: [machineMaterials[building.kind] ?? PhysicallyBasedMaterial()]
                )
                entity.name = building.kind.rawValue
                planetRoot.addChild(entity)
                machineEntities[tile] = entity
                entity.scale = .init(repeating: 0.01)
            }

            let up = normalize(SIMD3<Float>(center))
            entity.transform.rotation = simd_quatf(from: SIMD3<Float>(0, 1, 0), to: up)
            entity.position = up * (ground + PlanetMesh.buildingLift(building.kind) * scale)
            var target = entity.transform
            target.scale = .init(repeating: scale)
            entity.move(to: target, relativeTo: entity.parent, duration: 0.28, timingFunction: .easeOut)
        }
    }

    private func syncSelection(_ selection: Int?) {
        guard selection != lastSelection else { return }
        if let previous = lastSelection, previous < tileEntities.count {
            let biome = tileBiomes[previous] ?? .temperate
            tileEntities[previous].model?.materials = [
                biomeMaterials[biome] ?? PhysicallyBasedMaterial(),
            ]
        }
        if let selection, selection < tileEntities.count {
            tileEntities[selection].model?.materials = [selectedMaterial]
        }
        lastSelection = selection
    }

    private func syncMeteor(world: World) {
        guard let position = Self.meteorPosition(world: world) else {
            meteor.isEnabled = false
            return
        }
        meteor.isEnabled = true
        meteor.position = SIMD3<Float>(position)
        let progress = world.state.activeMeteor.map { $0.progress(at: world.state.elapsed) } ?? 1
        meteor.scale = .init(repeating: Float(1 + (1 - progress) * 0.6))
    }

    /// Where the incoming meteor is right now, in world space.
    ///
    /// Static and pure so the interface can put a tappable target over it using
    /// exactly the position the renderer drew, with no second source of truth.
    static func meteorPosition(world: World) -> Vec3? {
        guard let incoming = world.state.activeMeteor else { return nil }
        let progress = incoming.progress(at: world.state.elapsed)
        let target = rotate(
            world.geometry.tiles[incoming.targetTile].center,
            about: world.state.axis, by: world.state.spinPhase
        )
        // Comes in over the world's shoulder so it is always seen against the
        // sky rather than emerging from behind the planet.
        let entry = normalize(target + Vec3(0.55, 0.8, 0.2)) * 3.6
        let eased = progress * progress
        return entry * (1 - eased) + target * 1.03 * eased
    }

    // MARK: - Picking

    /// Tile under a view-space point, or `nil` if the tap missed the world.
    func tile(at point: CGPoint, viewSize: CGSize, rig: CameraRig, world: World) -> Int? {
        let ray = rig.ray(at: point, in: viewSize)
        guard let hit = raySphereHit(
            origin: ray.origin, direction: ray.direction,
            radius: Double(PlanetMesh.surfaceRadius)
        ) else { return nil }
        // The tap arrives in world space; the tiles live in the planet's frame,
        // which has spun since.
        let unspun = planetRoot.transform.rotation.inverse.act(SIMD3<Float>(hit))
        return world.geometry.nearestTile(to: Vec3(unspun))
    }
}
