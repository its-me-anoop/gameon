import Testing
import Foundation
import simd
@testable import OrbitKit

@Suite struct ClimateTests {
    /// Axis perpendicular to the star: the familiar world, hot at the equator
    /// and frozen at both poles.
    private var upright: Climate {
        Climate(axis: Vec3(0, 1, 0), starDirection: Vec3(1, 0, 0))
    }

    /// Axis aimed straight at the star: one pole in permanent day.
    private var staring: Climate {
        Climate(axis: Vec3(1, 0, 0), starDirection: Vec3(1, 0, 0))
    }

    @Test func uprightWorldsPeakAtTheEquator() {
        #expect(abs(upright.declination) < 1e-9)
        let equator = upright.insolation(latitude: 0)
        #expect(abs(equator - Climate.referenceInsolation) < 1e-9)
        #expect(upright.insolation(latitude: .pi / 4) < equator)
        #expect(upright.insolation(latitude: .pi / 2) < equator / 10)
    }

    @Test func uprightWorldsAreSymmetricAboutTheEquator() {
        for degrees in stride(from: 5.0, through: 85.0, by: 10) {
            let latitude = degrees * .pi / 180
            let north = upright.insolation(latitude: latitude)
            let south = upright.insolation(latitude: -latitude)
            #expect(abs(north - south) < 1e-12)
        }
    }

    @Test func aimingTheAxisAtTheStarTriplesPeakYield() {
        // Permanent day beats a day/night cycle by a factor of π.
        let staringPole = staring.solarFactor(at: Vec3(1, 0, 0))
        let uprightEquator = upright.solarFactor(at: Vec3(1, 0, 0))
        #expect(abs(uprightEquator - 1) < 1e-9)
        #expect(staringPole > 3.0)
        #expect(staringPole < 3.2)
    }

    @Test func theHemisphereFacingAwayFromTheStarGoesDark() {
        #expect(staring.insolation(at: Vec3(-1, 0, 0)) == 0)
        #expect(staring.biome(at: Vec3(-1, 0, 0)) == .frozen)
    }

    @Test func uprightWorldsHaveTwoFrostLinesAndTiltedOnesHaveFewer() {
        let uprightLines = upright.frostLatitudes()
        #expect(uprightLines.count == 2)
        #expect(abs(uprightLines[0] + uprightLines[1]) < 1e-6, "symmetric about the equator")

        let staringLines = staring.frostLatitudes()
        #expect(staringLines.count == 1)
    }

    @Test func frostLatitudeSitsExactlyOnTheFreezingContour() {
        for line in upright.frostLatitudes() {
            let index = upright.temperatureIndex(insolation: upright.insolation(latitude: line))
            #expect(abs(index - Climate.freezingIndex) < 1e-4)
        }
    }

    @Test func biomeBandsAreOrderedByTemperature() {
        #expect(Climate.biome(temperatureIndex: 0.0) == .frozen)
        #expect(Climate.biome(temperatureIndex: 0.25) == .tundra)
        #expect(Climate.biome(temperatureIndex: 0.45) == .temperate)
        #expect(Climate.biome(temperatureIndex: 0.7) == .arid)
        #expect(Climate.biome(temperatureIndex: 0.95) == .molten)
    }

    @Test func greenhouseWarmsEveryLatitude() {
        let warm = Climate(axis: Vec3(0, 1, 0), starDirection: Vec3(1, 0, 0), greenhouse: 0.1)
        for degrees in stride(from: -80.0, through: 80.0, by: 20) {
            let position = rotate(Vec3(1, 0, 0), about: Vec3(0, 0, 1), by: degrees * .pi / 180)
            #expect(warm.temperatureIndex(at: position) > upright.temperatureIndex(at: position))
        }
    }

    @Test func condensersPeakOnTheFrostLineAndSmeltersInTheHeat() {
        let onFrost = BuildingKind.condenser.efficiency(
            temperatureIndex: Climate.freezingIndex + 0.04, solarFactor: 1
        )
        let inHeat = BuildingKind.condenser.efficiency(temperatureIndex: 0.8, solarFactor: 1)
        #expect(onFrost > 0.99)
        #expect(inHeat < 0.05)

        #expect(BuildingKind.smelter.efficiency(temperatureIndex: 0.2, solarFactor: 1) == 0)
        #expect(BuildingKind.smelter.efficiency(temperatureIndex: 0.9, solarFactor: 1) > 1)
    }
}
