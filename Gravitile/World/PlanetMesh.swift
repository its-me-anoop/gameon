import CoreGraphics
import Foundation
import OrbitKit
import RealityKit
import UIKit
import simd

/// Procedural meshes for the world. Nothing here is loaded from a file: the
/// planet, its machines and its sky are all generated from the tiling, which is
/// what lets a collapse hand us a different world every time.
enum PlanetMesh {
    /// Surface radius of the planet in scene units. Everything else is
    /// expressed relative to it.
    static let surfaceRadius: Float = 1.0
    static let crustRadius: Float = 0.945

    // MARK: - Tiles

    /// One tile as a low prism: a flat top at the surface and short walls down
    /// to the crust, so the seams between tiles read as real gaps.
    static func tile(_ tile: PlanetTile, height: Float) -> MeshResource {
        let outer = surfaceRadius + height
        let inner = crustRadius
        // Pull each corner very slightly toward the middle so neighboring tiles
        // don't share an edge — the hairline gap is what makes the surface look
        // built rather than painted.
        let corners = tile.corners.map { corner -> SIMD3<Float> in
            let inset = normalize(corner + tile.center * 0.045)
            return SIMD3<Float>(inset) * outer
        }
        let skirt = tile.corners.map { corner -> SIMD3<Float> in
            let inset = normalize(corner + tile.center * 0.045)
            return SIMD3<Float>(inset) * inner
        }
        let center = SIMD3<Float>(tile.center) * outer
        let up = normalize(SIMD3<Float>(tile.center))

        var positions: [SIMD3<Float>] = []
        var normals: [SIMD3<Float>] = []
        var indices: [UInt32] = []

        // Top face as a fan around the tile's middle.
        positions.append(center)
        normals.append(up)
        for corner in corners {
            positions.append(corner)
            normals.append(up)
        }
        for i in 0..<corners.count {
            let next = (i + 1) % corners.count
            indices.append(contentsOf: [0, UInt32(i + 1), UInt32(next + 1)])
        }

        // Walls.
        for i in 0..<corners.count {
            let next = (i + 1) % corners.count
            let base = UInt32(positions.count)
            let outward = normalize(corners[i] + corners[next])
            positions.append(contentsOf: [corners[i], corners[next], skirt[next], skirt[i]])
            normals.append(contentsOf: Array(repeating: outward, count: 4))
            indices.append(contentsOf: [base, base + 3, base + 1])
            indices.append(contentsOf: [base + 1, base + 3, base + 2])
        }

        return build(positions: positions, normals: normals, indices: indices, name: "tile")
    }

    // MARK: - Rings and shafts

    /// A thin band around the world at a latitude — the frost line.
    static func latitudeRing(
        latitude: Double, radius: Float, width: Float, segments: Int = 96
    ) -> MeshResource {
        var positions: [SIMD3<Float>] = []
        var normals: [SIMD3<Float>] = []
        var indices: [UInt32] = []
        let half = Double(width) / 2

        for step in 0...segments {
            let theta = Double(step) / Double(segments) * 2 * .pi
            for edge in [latitude - half, latitude + half] {
                let point = SIMD3<Float>(
                    Float(cos(edge) * cos(theta)),
                    Float(sin(edge)),
                    Float(cos(edge) * sin(theta))
                )
                positions.append(point * radius)
                normals.append(normalize(point))
            }
        }
        for step in 0..<segments {
            let base = UInt32(step * 2)
            indices.append(contentsOf: [base, base + 1, base + 2])
            indices.append(contentsOf: [base + 1, base + 3, base + 2])
        }
        return build(positions: positions, normals: normals, indices: indices, name: "ring")
    }

    // MARK: - Sky

    /// Inward-facing sphere carrying the starfield texture.
    static func skyDome(radius: Float, rings: Int = 24, segments: Int = 48) -> MeshResource {
        var positions: [SIMD3<Float>] = []
        var normals: [SIMD3<Float>] = []
        var uvs: [SIMD2<Float>] = []
        var indices: [UInt32] = []

        for ring in 0...rings {
            let v = Float(ring) / Float(rings)
            let phi = Float.pi * v
            for segment in 0...segments {
                let u = Float(segment) / Float(segments)
                let theta = 2 * Float.pi * u
                let direction = SIMD3<Float>(
                    sin(phi) * cos(theta), cos(phi), sin(phi) * sin(theta)
                )
                positions.append(direction * radius)
                normals.append(-direction)
                uvs.append(SIMD2<Float>(u, v))
            }
        }
        for ring in 0..<rings {
            for segment in 0..<segments {
                let a = UInt32(ring * (segments + 1) + segment)
                let b = a + UInt32(segments + 1)
                // Wound inward: the camera sits inside this sphere.
                indices.append(contentsOf: [a, a + 1, b])
                indices.append(contentsOf: [a + 1, b + 1, b])
            }
        }

        var descriptor = MeshDescriptor(name: "sky")
        descriptor.positions = MeshBuffers.Positions(positions)
        descriptor.normals = MeshBuffers.Normals(normals)
        descriptor.textureCoordinates = MeshBuffers.TextureCoordinates(uvs)
        descriptor.primitives = .triangles(indices)
        return (try? MeshResource.generate(from: [descriptor])) ?? .generateSphere(radius: radius)
    }

    /// Equirectangular sky: a soft band of galactic light across the middle and
    /// a scatter of stars, drawn once at launch. Deterministic from the seed, so
    /// a world's sky belongs to that world.
    static func skyTexture(
        seed: UInt64, space: UIColor, glow: UIColor, star: UIColor, width: Int = 2048
    ) -> TextureResource? {
        let height = width / 2
        let format = UIGraphicsImageRendererFormat()
        format.opaque = true
        format.scale = 1
        let renderer = UIGraphicsImageRenderer(
            size: CGSize(width: width, height: height), format: format
        )
        let image = renderer.image { context in
            let cg = context.cgContext
            space.setFill()
            cg.fill(CGRect(x: 0, y: 0, width: width, height: height))

            // A diagonal wash of galactic light, so the sky has a direction
            // instead of being an even sprinkle.
            cg.saveGState()
            cg.translateBy(x: CGFloat(width) / 2, y: CGFloat(height) / 2)
            cg.rotate(by: -0.32)
            let bandHeight = CGFloat(height) * 0.72
            if let gradient = CGGradient(
                colorsSpace: CGColorSpaceCreateDeviceRGB(),
                colors: [
                    space.withAlphaComponent(0).cgColor,
                    glow.withAlphaComponent(0.85).cgColor,
                    space.withAlphaComponent(0).cgColor,
                ] as CFArray,
                locations: [0, 0.5, 1]
            ) {
                cg.clip(to: CGRect(
                    x: -CGFloat(width), y: -bandHeight / 2,
                    width: CGFloat(width) * 2, height: bandHeight
                ))
                cg.drawLinearGradient(
                    gradient,
                    start: CGPoint(x: 0, y: -bandHeight / 2),
                    end: CGPoint(x: 0, y: bandHeight / 2),
                    options: []
                )
            }
            cg.restoreGState()

        }
        guard let cgImage = image.cgImage else { return nil }
        return try? TextureResource.generate(from: cgImage, options: .init(semantic: .color))
    }

    /// Stars as geometry rather than pixels.
    ///
    /// A texture can't win here: the visible field of view covers only a few
    /// hundred texels of any sky texture small enough to ship, so painted stars
    /// arrive as soft squares. Quads facing the world stay sharp at any zoom
    /// and cost one draw call per brightness layer.
    static func starLayers(
        seed: UInt64, radius: Float, layers: Int = 3
    ) -> [(mesh: MeshResource, brightness: Double)] {
        var rng = SplitMix64(seed: seed &+ 0x5741_5253)
        var positions = [[SIMD3<Float>]](repeating: [], count: layers)
        var sizes = [[Float]](repeating: [], count: layers)

        // The visible frustum is a small slice of the sky, so a convincing
        // field needs far more stars than it looks like it should.
        for _ in 0..<4200 {
            // Uniform on the sphere: cosine-distributed latitude, so stars don't
            // bunch at the poles the way naive angle pairs do.
            let z = rng.double(in: -1...1)
            let theta = rng.double(in: 0...(2 * .pi))
            let ring = (1 - z * z).squareRoot()
            let direction = SIMD3<Float>(
                Float(ring * cos(theta)), Float(z), Float(ring * sin(theta))
            )
            let roll = rng.unitDouble()
            let layer = roll > 0.93 ? 0 : (roll > 0.62 ? 1 : 2)
            positions[layer].append(direction * radius)
            sizes[layer].append(Float(rng.double(in: 0.035...0.095)) * (layer == 0 ? 2.2 : 1))
        }

        return (0..<layers).map { layer in
            var vertices: [SIMD3<Float>] = []
            var normals: [SIMD3<Float>] = []
            var indices: [UInt32] = []

            for (point, size) in zip(positions[layer], sizes[layer]) {
                let inward = normalize(-point)
                let (e1, e2) = Vec3(inward).tangentBasis()
                let right = SIMD3<Float>(e1) * size
                let up = SIMD3<Float>(e2) * size
                let base = UInt32(vertices.count)
                vertices.append(contentsOf: [
                    point - right - up, point + right - up,
                    point + right + up, point - right + up,
                ])
                normals.append(contentsOf: Array(repeating: inward, count: 4))
                indices.append(contentsOf: [base, base + 1, base + 2, base, base + 2, base + 3])
            }

            let brightness = [1.0, 0.66, 0.4][min(layer, 2)]
            return (
                build(positions: vertices, normals: normals, indices: indices, name: "stars\(layer)"),
                brightness
            )
        }
    }

    // MARK: - Machines

    /// Low-poly machine, built from primitives and standing on +Y.
    @MainActor
    static func building(_ kind: BuildingKind) -> MeshResource {
        switch kind {
        case .solarArray:
            return .generateBox(size: SIMD3<Float>(0.09, 0.012, 0.065), cornerRadius: 0.004)
        case .oreMine:
            return .generateCone(height: 0.075, radius: 0.038)
        case .condenser:
            return .generateCylinder(height: 0.075, radius: 0.026)
        case .smelter:
            return .generateBox(size: SIMD3<Float>(0.062, 0.062, 0.062), cornerRadius: 0.008)
        case .greenhouse:
            return .generateSphere(radius: 0.038)
        case .beacon:
            return .generateCylinder(height: 0.13, radius: 0.011)
        case .ballast:
            return .generateBox(size: SIMD3<Float>(0.085, 0.05, 0.085), cornerRadius: 0.006)
        }
    }

    /// Vertical offset that puts a machine's base on the ground.
    static func buildingLift(_ kind: BuildingKind) -> Float {
        switch kind {
        case .solarArray: 0.012
        case .oreMine: 0.038
        case .condenser: 0.038
        case .smelter: 0.031
        case .greenhouse: 0.026
        case .beacon: 0.065
        case .ballast: 0.025
        }
    }

    // MARK: - Plumbing

    private static func build(
        positions: [SIMD3<Float>], normals: [SIMD3<Float>],
        indices: [UInt32], name: String
    ) -> MeshResource {
        var descriptor = MeshDescriptor(name: name)
        descriptor.positions = MeshBuffers.Positions(positions)
        descriptor.normals = MeshBuffers.Normals(normals)
        descriptor.primitives = .triangles(indices)
        return (try? MeshResource.generate(from: [descriptor]))
            ?? .generateSphere(radius: 0.01)
    }
}
