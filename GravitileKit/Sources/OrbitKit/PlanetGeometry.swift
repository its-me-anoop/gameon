import simd

/// One surface cell of the world: a hexagon, or one of the twelve pentagons
/// that any sphere tiling of hexagons must contain.
public struct PlanetTile: Sendable, Equatable {
    public let index: Int
    /// Unit-sphere position of the tile's middle.
    public let center: Vec3
    /// Polygon boundary on the unit sphere, wound counter-clockwise as seen
    /// from outside so mesh triangles face the camera without a flip.
    public let corners: [Vec3]
    public let neighbors: [Int]
    /// Solid angle in steradians. Pentagons are slightly smaller than hexagons,
    /// and yields scale with area so no tile is quietly worth more than another.
    public let area: Double

    public var isPentagon: Bool { corners.count == 5 }
}

/// A Goldberg polyhedron — the dual of a subdivided icosahedron. `frequency`
/// controls resolution; the tiling has exactly `10·f² + 2` tiles, of which
/// twelve are pentagons.
///
/// Generation is deterministic: vertex positions are computed once from a
/// canonical identifier, never twice from two adjoining faces, so shared
/// corners are bit-identical and the dual has no seams.
public struct PlanetGeometry: Sendable {
    public let frequency: Int
    public let tiles: [PlanetTile]

    public var tileCount: Int { tiles.count }

    // MARK: Icosahedron seed

    private static let icosahedronVertices: [Vec3] = {
        let t = (1.0 + 5.0.squareRoot()) / 2.0
        let raw: [Vec3] = [
            Vec3(-1, t, 0), Vec3(1, t, 0), Vec3(-1, -t, 0), Vec3(1, -t, 0),
            Vec3(0, -1, t), Vec3(0, 1, t), Vec3(0, -1, -t), Vec3(0, 1, -t),
            Vec3(t, 0, -1), Vec3(t, 0, 1), Vec3(-t, 0, -1), Vec3(-t, 0, 1),
        ]
        return raw.map { normalize($0) }
    }()

    /// Counter-clockwise from outside.
    private static let icosahedronFaces: [(Int, Int, Int)] = [
        (0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11),
        (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6), (7, 1, 8),
        (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9),
        (4, 9, 5), (2, 4, 11), (6, 2, 10), (8, 6, 7), (9, 8, 1),
    ]

    // MARK: Generation

    public static func generate(frequency f: Int) -> PlanetGeometry {
        precondition(f >= 1, "frequency must be positive")

        // Canonical vertex identifiers: the 12 corners, then (f-1) interior
        // points per edge, then the interior lattice of each face.
        var edgeIndex: [Int64: Int] = [:]
        var edgeEndpoints: [(Int, Int)] = []
        for face in icosahedronFaces {
            for pair in [(face.0, face.1), (face.1, face.2), (face.2, face.0)] {
                let key = edgeKey(pair.0, pair.1)
                if edgeIndex[key] == nil {
                    edgeIndex[key] = edgeEndpoints.count
                    edgeEndpoints.append((min(pair.0, pair.1), max(pair.0, pair.1)))
                }
            }
        }

        let perEdge = f - 1
        let perFace = max(0, (f - 1) * (f - 2) / 2)
        let edgeBase = 12
        let faceBase = edgeBase + edgeEndpoints.count * perEdge
        let vertexCount = faceBase + icosahedronFaces.count * perFace

        var positions = [Vec3](repeating: .zero, count: vertexCount)
        for i in 0..<12 { positions[i] = icosahedronVertices[i] }
        for (edge, endpoints) in edgeEndpoints.enumerated() {
            let a = icosahedronVertices[endpoints.0]
            let b = icosahedronVertices[endpoints.1]
            for step in 1..<max(f, 1) where step <= perEdge {
                let t = Double(step), rest = Double(f - step)
                positions[edgeBase + edge * perEdge + (step - 1)] = normalize(a * rest + b * t)
            }
        }

        /// Identifier of the lattice point with barycentric weights
        /// `(u, v, w)`, `u + v + w == f`, on `face`.
        func vertexID(face: Int, u: Int, v: Int, w: Int) -> Int {
            let (ia, ib, ic) = icosahedronFaces[face]
            if u == f { return ia }
            if v == f { return ib }
            if w == f { return ic }
            if w == 0 { return edgePointID(ia, ib, stepsFromFirst: v) }
            if u == 0 { return edgePointID(ib, ic, stepsFromFirst: w) }
            if v == 0 { return edgePointID(ic, ia, stepsFromFirst: u) }
            return faceBase + face * perFace + interiorOffset(v: v, w: w)
        }

        /// `steps` counts from `p` toward `q`; the stored order is by ascending
        /// icosahedron index, so mirror when the caller's order is reversed.
        func edgePointID(_ p: Int, _ q: Int, stepsFromFirst steps: Int) -> Int {
            let edge = edgeIndex[edgeKey(p, q)]!
            let t = p < q ? steps : f - steps
            return edgeBase + edge * perEdge + (t - 1)
        }

        /// Row-major order over the strictly-interior lattice.
        func interiorOffset(v: Int, w: Int) -> Int {
            var offset = 0
            for row in 1..<v { offset += max(0, f - 1 - row) }
            return offset + (w - 1)
        }

        var triangles: [(Int, Int, Int)] = []
        triangles.reserveCapacity(20 * f * f)
        for (faceIndex, face) in icosahedronFaces.enumerated() {
            let a = icosahedronVertices[face.0]
            let b = icosahedronVertices[face.1]
            let c = icosahedronVertices[face.2]

            for v in 0...f {
                for w in 0...(f - v) {
                    let u = f - v - w
                    let id = vertexID(face: faceIndex, u: u, v: v, w: w)
                    if id >= faceBase {
                        positions[id] = normalize(
                            a * Double(u) + b * Double(v) + c * Double(w)
                        )
                    }
                }
            }

            for v in 0..<f {
                for w in 0..<(f - v) {
                    let p00 = vertexID(face: faceIndex, u: f - v - w, v: v, w: w)
                    let p10 = vertexID(face: faceIndex, u: f - v - w - 1, v: v + 1, w: w)
                    let p01 = vertexID(face: faceIndex, u: f - v - w - 1, v: v, w: w + 1)
                    triangles.append((p00, p10, p01))
                    if v + w + 2 <= f {
                        let p11 = vertexID(face: faceIndex, u: f - v - w - 2, v: v + 1, w: w + 1)
                        triangles.append((p10, p11, p01))
                    }
                }
            }
        }

        // Dual: every icosphere vertex becomes a tile whose corners are the
        // centroids of the triangles around it.
        var incident = [[Int]](repeating: [], count: vertexCount)
        var adjacency = [Set<Int>](repeating: [], count: vertexCount)
        for (index, tri) in triangles.enumerated() {
            for (vertex, others) in [
                (tri.0, (tri.1, tri.2)), (tri.1, (tri.2, tri.0)), (tri.2, (tri.0, tri.1)),
            ] {
                incident[vertex].append(index)
                adjacency[vertex].insert(others.0)
                adjacency[vertex].insert(others.1)
            }
        }

        let centroids = triangles.map { tri in
            normalize(positions[tri.0] + positions[tri.1] + positions[tri.2])
        }

        var tiles: [PlanetTile] = []
        tiles.reserveCapacity(vertexCount)
        for vertex in 0..<vertexCount {
            let center = positions[vertex]
            let (e1, e2) = center.tangentBasis()
            let corners = incident[vertex]
                .map { centroids[$0] }
                .sorted { lhs, rhs in
                    atan2(dot(lhs, e2), dot(lhs, e1)) < atan2(dot(rhs, e2), dot(rhs, e1))
                }
            tiles.append(
                PlanetTile(
                    index: vertex,
                    center: center,
                    corners: corners,
                    neighbors: adjacency[vertex].sorted(),
                    area: sphericalPolygonArea(corners)
                )
            )
        }

        return PlanetGeometry(frequency: f, tiles: tiles)
    }

    private static func edgeKey(_ a: Int, _ b: Int) -> Int64 {
        Int64(min(a, b)) << 32 | Int64(max(a, b))
    }

    // MARK: Queries

    /// Index of the tile whose center is closest to `point` — the last step of
    /// tap picking, after the ray hits the sphere.
    public func nearestTile(to point: Vec3) -> Int {
        let p = normalize(point)
        var best = 0
        var bestDot = -Double.infinity
        for tile in tiles {
            let d = dot(tile.center, p)
            if d > bestDot {
                bestDot = d
                best = tile.index
            }
        }
        return best
    }
}

/// Area of a convex spherical polygon by angle excess.
func sphericalPolygonArea(_ points: [Vec3]) -> Double {
    let n = points.count
    guard n >= 3 else { return 0 }
    var sum = 0.0
    for i in 0..<n {
        let previous = points[(i + n - 1) % n]
        let current = points[i]
        let next = points[(i + 1) % n]
        let toPrevious = normalize(previous - current * dot(previous, current))
        let toNext = normalize(next - current * dot(next, current))
        sum += acos(min(max(dot(toPrevious, toNext), -1), 1))
    }
    return sum - Double(n - 2) * .pi
}
