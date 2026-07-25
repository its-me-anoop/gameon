import SwiftUI
import OrbitKit

/// One complete color world — chrome, climate bands and buildings. A theme
/// change repaints the planet itself, not just the interface around it.
///
/// Authored in OKLCH so lightness steps stay perceptually even, and nothing is
/// ever pure black or white: neutrals are tinted toward each palette's anchor.
struct WorldPalette: Identifiable, Equatable, Sendable {
    let id: String
    let name: String
    let tagline: String
    /// Light palettes flip the system color scheme.
    let isLight: Bool

    let space: Color
    let spaceGlow: Color
    let panel: Color
    let panelEdge: Color
    let textPrimary: Color
    let textSecondary: Color
    let accent: Color
    let warning: Color

    /// Ground color per climate band. The frost line is where `frozen` meets
    /// `tundra`, and players learn to read it before they learn its name.
    let biomes: [Biome: Color]
    /// The crust under the tiles, visible in the seams.
    let crust: Color
    let starTint: Color
    /// Solid, saturated colors for the machines standing on the ground.
    let buildings: [BuildingKind: Color]

    func color(for biome: Biome) -> Color { biomes[biome] ?? .gray }
    func color(for kind: BuildingKind) -> Color { buildings[kind] ?? accent }
}

extension WorldPalette {
    /// Shared across palettes: a machine keeps its identity color so players
    /// recognize their own layouts after switching themes.
    private static func machines(
        solar: Color, mine: Color, condenser: Color, smelter: Color,
        greenhouse: Color, beacon: Color, ballast: Color
    ) -> [BuildingKind: Color] {
        [
            .solarArray: solar, .oreMine: mine, .condenser: condenser,
            .smelter: smelter, .greenhouse: greenhouse, .beacon: beacon,
            .ballast: ballast,
        ]
    }

    /// Deep navy night, heat-ramp ground. The default world.
    static let ember = WorldPalette(
        id: "ember", name: "Ember", tagline: "Navy night, heat-ramp ground",
        isLight: false,
        space: Color(okL: 0.13, c: 0.014, h: 260),
        spaceGlow: Color(okL: 0.24, c: 0.04, h: 275),
        panel: Color(okL: 0.20, c: 0.018, h: 260),
        panelEdge: Color(okL: 0.31, c: 0.024, h: 260),
        textPrimary: Color(okL: 0.95, c: 0.008, h: 260),
        textSecondary: Color(okL: 0.72, c: 0.012, h: 260),
        accent: Color(okL: 0.78, c: 0.15, h: 62),
        warning: Color(okL: 0.68, c: 0.20, h: 22),
        biomes: [
            .frozen: Color(okL: 0.88, c: 0.03, h: 235),
            .tundra: Color(okL: 0.70, c: 0.05, h: 210),
            .temperate: Color(okL: 0.60, c: 0.10, h: 150),
            .arid: Color(okL: 0.63, c: 0.13, h: 70),
            .molten: Color(okL: 0.56, c: 0.20, h: 32),
        ],
        crust: Color(okL: 0.22, c: 0.03, h: 45),
        starTint: Color(okL: 0.96, c: 0.07, h: 85),
        buildings: machines(
            solar: Color(okL: 0.80, c: 0.15, h: 90),
            mine: Color(okL: 0.62, c: 0.09, h: 40),
            condenser: Color(okL: 0.82, c: 0.10, h: 220),
            smelter: Color(okL: 0.62, c: 0.19, h: 28),
            greenhouse: Color(okL: 0.74, c: 0.14, h: 145),
            beacon: Color(okL: 0.85, c: 0.13, h: 305),
            ballast: Color(okL: 0.42, c: 0.02, h: 260)
        )
    )

    /// Ocean deeps and coral heat.
    static let tidepool = WorldPalette(
        id: "tidepool", name: "Tidepool", tagline: "Ocean deeps, coral heat",
        isLight: false,
        space: Color(okL: 0.14, c: 0.02, h: 220),
        spaceGlow: Color(okL: 0.26, c: 0.05, h: 200),
        panel: Color(okL: 0.21, c: 0.025, h: 220),
        panelEdge: Color(okL: 0.32, c: 0.03, h: 220),
        textPrimary: Color(okL: 0.95, c: 0.008, h: 210),
        textSecondary: Color(okL: 0.72, c: 0.02, h: 210),
        accent: Color(okL: 0.80, c: 0.13, h: 190),
        warning: Color(okL: 0.70, c: 0.19, h: 25),
        biomes: [
            .frozen: Color(okL: 0.90, c: 0.04, h: 200),
            .tundra: Color(okL: 0.74, c: 0.07, h: 195),
            .temperate: Color(okL: 0.62, c: 0.11, h: 170),
            .arid: Color(okL: 0.66, c: 0.13, h: 85),
            .molten: Color(okL: 0.58, c: 0.20, h: 20),
        ],
        crust: Color(okL: 0.20, c: 0.03, h: 215),
        starTint: Color(okL: 0.95, c: 0.06, h: 95),
        buildings: machines(
            solar: Color(okL: 0.84, c: 0.13, h: 95),
            mine: Color(okL: 0.60, c: 0.08, h: 50),
            condenser: Color(okL: 0.86, c: 0.09, h: 215),
            smelter: Color(okL: 0.64, c: 0.18, h: 30),
            greenhouse: Color(okL: 0.76, c: 0.13, h: 150),
            beacon: Color(okL: 0.86, c: 0.12, h: 300),
            ballast: Color(okL: 0.40, c: 0.02, h: 220)
        )
    )

    /// Cream daylight — the world seen from a bright observatory.
    static let meadow = WorldPalette(
        id: "meadow", name: "Meadow", tagline: "Cream daylight, garden ground",
        isLight: true,
        space: Color(okL: 0.95, c: 0.014, h: 95),
        spaceGlow: Color(okL: 0.88, c: 0.035, h: 120),
        panel: Color(okL: 0.99, c: 0.006, h: 95),
        panelEdge: Color(okL: 0.86, c: 0.02, h: 105),
        textPrimary: Color(okL: 0.25, c: 0.02, h: 130),
        textSecondary: Color(okL: 0.46, c: 0.03, h: 130),
        accent: Color(okL: 0.52, c: 0.15, h: 145),
        warning: Color(okL: 0.55, c: 0.19, h: 28),
        biomes: [
            .frozen: Color(okL: 0.92, c: 0.02, h: 230),
            .tundra: Color(okL: 0.80, c: 0.05, h: 195),
            .temperate: Color(okL: 0.68, c: 0.13, h: 140),
            .arid: Color(okL: 0.74, c: 0.12, h: 80),
            .molten: Color(okL: 0.60, c: 0.19, h: 35),
        ],
        crust: Color(okL: 0.45, c: 0.04, h: 90),
        starTint: Color(okL: 0.90, c: 0.09, h: 80),
        buildings: machines(
            solar: Color(okL: 0.62, c: 0.16, h: 85),
            mine: Color(okL: 0.48, c: 0.09, h: 45),
            condenser: Color(okL: 0.58, c: 0.12, h: 230),
            smelter: Color(okL: 0.52, c: 0.19, h: 30),
            greenhouse: Color(okL: 0.55, c: 0.15, h: 150),
            beacon: Color(okL: 0.55, c: 0.16, h: 310),
            ballast: Color(okL: 0.38, c: 0.02, h: 120)
        )
    )

    /// Polar dusk with a mint-to-magenta sky.
    static let aurora = WorldPalette(
        id: "aurora", name: "Aurora", tagline: "Pine dusk, polar sky",
        isLight: false,
        space: Color(okL: 0.13, c: 0.02, h: 165),
        spaceGlow: Color(okL: 0.28, c: 0.06, h: 155),
        panel: Color(okL: 0.20, c: 0.025, h: 165),
        panelEdge: Color(okL: 0.31, c: 0.03, h: 170),
        textPrimary: Color(okL: 0.95, c: 0.008, h: 160),
        textSecondary: Color(okL: 0.72, c: 0.015, h: 160),
        accent: Color(okL: 0.83, c: 0.14, h: 160),
        warning: Color(okL: 0.70, c: 0.20, h: 15),
        biomes: [
            .frozen: Color(okL: 0.90, c: 0.04, h: 250),
            .tundra: Color(okL: 0.74, c: 0.08, h: 215),
            .temperate: Color(okL: 0.64, c: 0.12, h: 165),
            .arid: Color(okL: 0.66, c: 0.13, h: 95),
            .molten: Color(okL: 0.58, c: 0.21, h: 350),
        ],
        crust: Color(okL: 0.21, c: 0.03, h: 170),
        starTint: Color(okL: 0.95, c: 0.06, h: 120),
        buildings: machines(
            solar: Color(okL: 0.86, c: 0.13, h: 105),
            mine: Color(okL: 0.60, c: 0.08, h: 60),
            condenser: Color(okL: 0.86, c: 0.10, h: 240),
            smelter: Color(okL: 0.64, c: 0.20, h: 5),
            greenhouse: Color(okL: 0.78, c: 0.14, h: 155),
            beacon: Color(okL: 0.84, c: 0.14, h: 320),
            ballast: Color(okL: 0.40, c: 0.02, h: 170)
        )
    )

    /// Blush light and candy machines.
    static let sorbet = WorldPalette(
        id: "sorbet", name: "Sorbet", tagline: "Blush light, candy machines",
        isLight: true,
        space: Color(okL: 0.96, c: 0.012, h: 340),
        spaceGlow: Color(okL: 0.89, c: 0.04, h: 320),
        panel: Color(okL: 0.99, c: 0.005, h: 340),
        panelEdge: Color(okL: 0.87, c: 0.025, h: 330),
        textPrimary: Color(okL: 0.28, c: 0.05, h: 330),
        textSecondary: Color(okL: 0.48, c: 0.06, h: 330),
        accent: Color(okL: 0.58, c: 0.19, h: 350),
        warning: Color(okL: 0.55, c: 0.20, h: 30),
        biomes: [
            .frozen: Color(okL: 0.93, c: 0.03, h: 255),
            .tundra: Color(okL: 0.84, c: 0.06, h: 215),
            .temperate: Color(okL: 0.76, c: 0.12, h: 155),
            .arid: Color(okL: 0.80, c: 0.12, h: 75),
            .molten: Color(okL: 0.64, c: 0.20, h: 15),
        ],
        crust: Color(okL: 0.48, c: 0.06, h: 330),
        starTint: Color(okL: 0.92, c: 0.08, h: 60),
        buildings: machines(
            solar: Color(okL: 0.66, c: 0.16, h: 80),
            mine: Color(okL: 0.52, c: 0.10, h: 40),
            condenser: Color(okL: 0.60, c: 0.13, h: 235),
            smelter: Color(okL: 0.56, c: 0.20, h: 20),
            greenhouse: Color(okL: 0.58, c: 0.15, h: 155),
            beacon: Color(okL: 0.58, c: 0.18, h: 305),
            ballast: Color(okL: 0.40, c: 0.03, h: 330)
        )
    )
}

// MARK: - Facade

/// Design tokens backed by the active palette. Statics keep call sites short;
/// the palette only changes on the Settings screen.
@MainActor
enum Theme {
    static var current: WorldPalette = .ember

    static let palettes: [WorldPalette] = [.ember, .tidepool, .meadow, .aurora, .sorbet]

    static func palette(id: String) -> WorldPalette {
        palettes.first { $0.id == id } ?? .ember
    }

    static var space: Color { current.space }
    static var spaceGlow: Color { current.spaceGlow }
    static var panel: Color { current.panel }
    static var panelEdge: Color { current.panelEdge }
    static var textPrimary: Color { current.textPrimary }
    static var textSecondary: Color { current.textSecondary }
    static var accent: Color { current.accent }
    static var warning: Color { current.warning }

    static func color(for biome: Biome) -> Color { current.color(for: biome) }
    static func color(for kind: BuildingKind) -> Color { current.color(for: kind) }

    static func color(for resource: ResourceKind) -> Color {
        switch resource {
        case .ore: current.color(for: .oreMine)
        case .charge: current.color(for: .solarArray)
        case .water: current.color(for: .condenser)
        case .alloy: current.color(for: .smelter)
        case .biomass: current.color(for: .greenhouse)
        }
    }

    /// Display face: Unbounded (OFL). Falls back to the system face if missing.
    static func display(_ size: CGFloat, weight: Font.Weight = .bold) -> Font {
        .custom("Unbounded", size: size).weight(weight)
    }

    /// Numerals stay on SF Rounded — they change every frame and need to be
    /// legible at a glance, which a display face is not.
    static func numeral(_ size: CGFloat, weight: Font.Weight = .semibold) -> Font {
        .system(size: size, weight: weight, design: .rounded)
    }
}

// MARK: - OKLCH

extension Color {
    /// OKLCH → sRGB (Björn Ottosson's OKLab matrices). Palettes are authored in
    /// OKLCH so lightness steps stay perceptually even instead of clumping.
    init(okL L: Double, c C: Double, h hDegrees: Double) {
        let h = hDegrees * .pi / 180
        let a = C * cos(h)
        let b = C * sin(h)

        let l0 = L + 0.3963377774 * a + 0.2158037573 * b
        let m0 = L - 0.1055613458 * a - 0.0638541728 * b
        let s0 = L - 0.0894841775 * a - 1.2914855480 * b
        let l = l0 * l0 * l0
        let m = m0 * m0 * m0
        let s = s0 * s0 * s0

        func gamma(_ x: Double) -> Double {
            let clamped = min(max(x, 0), 1)
            return clamped <= 0.0031308
                ? 12.92 * clamped
                : 1.055 * pow(clamped, 1 / 2.4) - 0.055
        }
        self.init(
            red: gamma(4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s),
            green: gamma(-1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s),
            blue: gamma(-0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s)
        )
    }
}
