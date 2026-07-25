import Testing
import Foundation
import simd
@testable import OrbitKit

@Suite struct GeometryTests {
    @Test(arguments: [1, 2, 3, 4, 5, 6])
    func tileCountMatchesGoldbergFormula(frequency: Int) {
        let geometry = PlanetGeometry.generate(frequency: frequency)
        #expect(geometry.tileCount == 10 * frequency * frequency + 2)
    }

    @Test func everyTilingHasExactlyTwelvePentagons() {
        for frequency in 2...5 {
            let geometry = PlanetGeometry.generate(frequency: frequency)
            let pentagons = geometry.tiles.filter(\.isPentagon)
            #expect(pentagons.count == 12, "frequency \(frequency)")
            #expect(geometry.tiles.allSatisfy { $0.corners.count == 5 || $0.corners.count == 6 })
        }
    }

    @Test func centersAndCornersLieOnTheUnitSphere() {
        let geometry = PlanetGeometry.generate(frequency: 4)
        for tile in geometry.tiles {
            #expect(abs(length(tile.center) - 1) < 1e-12)
            for corner in tile.corners {
                #expect(abs(length(corner) - 1) < 1e-12)
            }
        }
    }

    @Test func neighborRelationsAreSymmetricAndPlausible() {
        let geometry = PlanetGeometry.generate(frequency: 4)
        for tile in geometry.tiles {
            #expect(tile.neighbors.count == tile.corners.count)
            #expect(!tile.neighbors.contains(tile.index))
            for neighbor in tile.neighbors {
                #expect(geometry.tiles[neighbor].neighbors.contains(tile.index))
            }
        }
    }

    /// A seam or a duplicated vertex would show up immediately as missing area.
    @Test func tileAreasCoverTheWholeSphere() {
        for frequency in [2, 4, 6] {
            let geometry = PlanetGeometry.generate(frequency: frequency)
            let total = geometry.tiles.reduce(0) { $0 + $1.area }
            #expect(abs(total - 4 * .pi) < 1e-9, "frequency \(frequency)")
        }
    }

    @Test func cornersWindCounterClockwiseSeenFromOutside() {
        let geometry = PlanetGeometry.generate(frequency: 3)
        for tile in geometry.tiles {
            // Newell's method: the polygon normal must point away from the
            // planet's core, or the mesh would render inside-out.
            var normal = Vec3.zero
            let corners = tile.corners
            for i in 0..<corners.count {
                let current = corners[i]
                let next = corners[(i + 1) % corners.count]
                normal += cross(current, next)
            }
            #expect(dot(normalize(normal), tile.center) > 0.9)
        }
    }

    @Test func generationIsDeterministic() {
        let first = PlanetGeometry.generate(frequency: 4)
        let second = PlanetGeometry.generate(frequency: 4)
        #expect(first.tiles == second.tiles)
    }

    @Test func nearestTileFindsTheTileUnderAPoint() {
        let geometry = PlanetGeometry.generate(frequency: 4)
        for tile in geometry.tiles.prefix(30) {
            #expect(geometry.nearestTile(to: tile.center) == tile.index)
            // A point nudged toward a corner still belongs to the same tile.
            let nudged = normalize(tile.center * 8 + tile.corners[0])
            #expect(geometry.nearestTile(to: nudged) == tile.index)
        }
    }

    @Test func rayPickingHitsTheFacingTile() {
        let geometry = PlanetGeometry.generate(frequency: 4)
        let tile = geometry.tiles[37]
        let cameraOrigin = tile.center * 5
        let hit = raySphereHit(origin: cameraOrigin, direction: -tile.center, radius: 1)
        #expect(hit != nil)
        #expect(geometry.nearestTile(to: hit!) == tile.index)
    }

    @Test func rayMissesWhenPointedAway() {
        #expect(raySphereHit(origin: Vec3(0, 0, 5), direction: Vec3(0, 0, 1), radius: 1) == nil)
        #expect(raySphereHit(origin: Vec3(0, 3, 5), direction: Vec3(0, 0, -1), radius: 1) == nil)
    }
}
