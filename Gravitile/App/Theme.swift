import SwiftUI
import GravitileKit

// MARK: - OKLCH

extension Color {
    /// OKLCH → sRGB (Björn Ottosson's OKLab matrices). Palettes are authored
    /// in OKLCH so lightness steps stay perceptually even; this converter
    /// keeps the definitions readable instead of burying precomputed floats.
    /// SwiftUI-only — this file is shared with the watch target.
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

// MARK: - Palette

/// One complete color world: chrome, tile ramp, and text pairings (v1.3 spec
/// §4.2). Nothing is pure black or white; neutrals are tinted toward each
/// palette's anchor hue.
struct ThemePalette: Identifiable, Equatable, Sendable {
    let id: String
    let name: String
    let tagline: String
    /// Light palettes flip the system color scheme.
    let isLight: Bool
    let bgDeep: Color
    let bgBoard: Color
    let cellWell: Color
    let textPrimary: Color
    let textSecondary: Color
    let accent: Color
    let frost: Color
    /// Doubling-mode ramp keyed by tile value (2, 4, 8, …).
    let tileColors: [Int: Color]
    /// Values at or below this sit on light tile fills and take dark ink;
    /// everything above takes `tileLightInk`.
    let darkInkMax: Int
    let tileDarkInk: Color
    let tileLightInk: Color
}

extension ThemePalette {
    /// The original theme — values predate the OKLCH converter and stay
    /// byte-identical so nothing shipped shifts (OKLCH noted inline).
    static let ember = ThemePalette(
        id: "ember", name: "Ember", tagline: "Navy night, heat-ramp tiles",
        isLight: false,
        bgDeep: Color(red: 0.0236, green: 0.0361, blue: 0.0602),        // oklch(14% 0.015 260)
        bgBoard: Color(red: 0.0677, green: 0.0872, blue: 0.1188),       // oklch(20% 0.018 260)
        cellWell: Color(red: 0.1192, green: 0.1423, blue: 0.1799),      // oklch(26% 0.02 260)
        textPrimary: Color(red: 0.9228, green: 0.9358, blue: 0.9567),   // oklch(95% 0.008 260)
        textSecondary: Color(red: 0.6283, green: 0.6464, blue: 0.6757), // oklch(72% 0.012 260)
        accent: Color(red: 0.9369, green: 0.5224, blue: 0.1805),        // oklch(72% 0.16 55)
        frost: Color(red: 0.6377, green: 0.7817, blue: 0.9032),         // oklch(80% 0.06 240)
        tileColors: [
            2: Color(red: 0.9444, green: 0.8977, blue: 0.7211),      // oklch(92% 0.06 95)
            4: Color(red: 0.9641, green: 0.8268, blue: 0.5384),      // oklch(88% 0.10 85)
            8: Color(red: 0.9943, green: 0.7011, blue: 0.3285),      // oklch(82% 0.14 70)
            16: Color(red: 1.0000, green: 0.5615, blue: 0.1906),     // oklch(76% 0.17 55)
            32: Color(red: 0.9883, green: 0.4199, blue: 0.1997),     // oklch(70% 0.19 40)
            64: Color(red: 0.9452, green: 0.2652, blue: 0.2709),     // oklch(64% 0.21 25)
            128: Color(red: 0.8625, green: 0.1074, blue: 0.3606),    // oklch(57% 0.22 10)
            256: Color(red: 0.7485, green: 0.1092, blue: 0.4997),    // oklch(54% 0.21 350)
            512: Color(red: 0.5724, green: 0.1684, blue: 0.6573),    // oklch(50% 0.20 320)
            1024: Color(red: 0.3817, green: 0.2428, blue: 0.7466),   // oklch(48% 0.19 290)
            2048: Color(red: 0.1381, green: 0.3550, blue: 0.7828),   // oklch(50% 0.18 262)
            4096: Color(red: 0.0000, green: 0.4861, blue: 0.7348),   // oklch(55% 0.15 235)
            8192: Color(red: 0.0000, green: 0.6037, blue: 0.6943),   // oklch(62% 0.13 210)
            16384: Color(red: 0.0870, green: 0.6901, blue: 0.6080),  // oklch(68% 0.12 180)
            32768: Color(red: 0.4113, green: 0.7594, blue: 0.4947),  // oklch(74% 0.13 150)
            65536: Color(red: 0.7048, green: 0.7890, blue: 0.4293),  // oklch(80% 0.12 120)
        ],
        darkInkMax: 16,
        tileDarkInk: Color(red: 0.16, green: 0.13, blue: 0.08),
        tileLightInk: Color(red: 0.9228, green: 0.9358, blue: 0.9567)
    )

    /// Deep ocean: aqua shallows through sand to coral depths.
    static let tidepool = ThemePalette(
        id: "tidepool", name: "Tidepool", tagline: "Ocean deeps, coral tiles",
        isLight: false,
        bgDeep: Color(okL: 0.15, c: 0.02, h: 220),
        bgBoard: Color(okL: 0.21, c: 0.025, h: 220),
        cellWell: Color(okL: 0.27, c: 0.03, h: 220),
        textPrimary: Color(okL: 0.95, c: 0.008, h: 210),
        textSecondary: Color(okL: 0.72, c: 0.02, h: 210),
        accent: Color(okL: 0.75, c: 0.13, h: 190),
        frost: Color(okL: 0.82, c: 0.05, h: 250),
        tileColors: [
            2: Color(okL: 0.90, c: 0.06, h: 190),
            4: Color(okL: 0.85, c: 0.09, h: 185),
            8: Color(okL: 0.78, c: 0.12, h: 180),
            16: Color(okL: 0.74, c: 0.12, h: 130),
            32: Color(okL: 0.76, c: 0.12, h: 95),
            64: Color(okL: 0.72, c: 0.14, h: 65),
            128: Color(okL: 0.66, c: 0.17, h: 40),
            256: Color(okL: 0.60, c: 0.19, h: 25),
            512: Color(okL: 0.55, c: 0.20, h: 0),
            1024: Color(okL: 0.50, c: 0.19, h: 330),
            2048: Color(okL: 0.48, c: 0.17, h: 300),
            4096: Color(okL: 0.50, c: 0.15, h: 270),
            8192: Color(okL: 0.55, c: 0.13, h: 245),
            16384: Color(okL: 0.62, c: 0.12, h: 220),
            32768: Color(okL: 0.70, c: 0.12, h: 200),
            65536: Color(okL: 0.78, c: 0.12, h: 180),
        ],
        darkInkMax: 32,
        tileDarkInk: Color(okL: 0.20, c: 0.03, h: 220),
        tileLightInk: Color(okL: 0.95, c: 0.008, h: 210)
    )

    /// Light warm cream with a garden ramp — the "morning" board.
    static let meadow = ThemePalette(
        id: "meadow", name: "Meadow", tagline: "Cream daylight, garden tiles",
        isLight: true,
        bgDeep: Color(okL: 0.96, c: 0.015, h: 95),
        bgBoard: Color(okL: 0.90, c: 0.02, h: 100),
        cellWell: Color(okL: 0.84, c: 0.025, h: 105),
        textPrimary: Color(okL: 0.25, c: 0.02, h: 130),
        textSecondary: Color(okL: 0.45, c: 0.03, h: 130),
        accent: Color(okL: 0.55, c: 0.14, h: 145),
        frost: Color(okL: 0.55, c: 0.09, h: 245),
        tileColors: [
            2: Color(okL: 0.88, c: 0.07, h: 110),
            4: Color(okL: 0.83, c: 0.10, h: 120),
            8: Color(okL: 0.76, c: 0.13, h: 135),
            16: Color(okL: 0.68, c: 0.14, h: 145),
            32: Color(okL: 0.58, c: 0.13, h: 155),
            64: Color(okL: 0.54, c: 0.12, h: 180),
            128: Color(okL: 0.51, c: 0.12, h: 220),
            256: Color(okL: 0.49, c: 0.14, h: 260),
            512: Color(okL: 0.47, c: 0.16, h: 300),
            1024: Color(okL: 0.45, c: 0.17, h: 330),
            2048: Color(okL: 0.48, c: 0.18, h: 355),
            4096: Color(okL: 0.55, c: 0.16, h: 20),
            8192: Color(okL: 0.60, c: 0.14, h: 45),
            16384: Color(okL: 0.66, c: 0.13, h: 70),
            32768: Color(okL: 0.72, c: 0.11, h: 90),
            65536: Color(okL: 0.80, c: 0.09, h: 105),
        ],
        darkInkMax: 16,
        tileDarkInk: Color(okL: 0.22, c: 0.03, h: 130),
        tileLightInk: Color(okL: 0.97, c: 0.005, h: 110)
    )

    /// Pine dusk with a mint → magenta sky ramp.
    static let aurora = ThemePalette(
        id: "aurora", name: "Aurora", tagline: "Pine dusk, polar-sky tiles",
        isLight: false,
        bgDeep: Color(okL: 0.14, c: 0.02, h: 160),
        bgBoard: Color(okL: 0.20, c: 0.025, h: 165),
        cellWell: Color(okL: 0.26, c: 0.03, h: 170),
        textPrimary: Color(okL: 0.95, c: 0.008, h: 160),
        textSecondary: Color(okL: 0.72, c: 0.015, h: 160),
        accent: Color(okL: 0.80, c: 0.13, h: 160),
        frost: Color(okL: 0.82, c: 0.05, h: 240),
        tileColors: [
            2: Color(okL: 0.90, c: 0.06, h: 155),
            4: Color(okL: 0.85, c: 0.09, h: 160),
            8: Color(okL: 0.78, c: 0.12, h: 165),
            16: Color(okL: 0.72, c: 0.13, h: 180),
            32: Color(okL: 0.66, c: 0.12, h: 200),
            64: Color(okL: 0.60, c: 0.13, h: 230),
            128: Color(okL: 0.55, c: 0.15, h: 260),
            256: Color(okL: 0.52, c: 0.18, h: 290),
            512: Color(okL: 0.50, c: 0.20, h: 320),
            1024: Color(okL: 0.52, c: 0.21, h: 345),
            2048: Color(okL: 0.56, c: 0.21, h: 5),
            4096: Color(okL: 0.62, c: 0.19, h: 20),
            8192: Color(okL: 0.68, c: 0.16, h: 35),
            16384: Color(okL: 0.75, c: 0.13, h: 55),
            32768: Color(okL: 0.82, c: 0.11, h: 80),
            65536: Color(okL: 0.88, c: 0.09, h: 100),
        ],
        darkInkMax: 16,
        tileDarkInk: Color(okL: 0.20, c: 0.03, h: 170),
        tileLightInk: Color(okL: 0.95, c: 0.008, h: 160)
    )

    /// Light candy palette — the natural companion to Math Pop.
    static let sorbet = ThemePalette(
        id: "sorbet", name: "Sorbet", tagline: "Blush light, candy tiles",
        isLight: true,
        bgDeep: Color(okL: 0.97, c: 0.01, h: 340),
        bgBoard: Color(okL: 0.91, c: 0.02, h: 330),
        cellWell: Color(okL: 0.85, c: 0.03, h: 325),
        textPrimary: Color(okL: 0.28, c: 0.05, h: 330),
        textSecondary: Color(okL: 0.48, c: 0.06, h: 330),
        accent: Color(okL: 0.62, c: 0.19, h: 350),
        frost: Color(okL: 0.55, c: 0.09, h: 250),
        tileColors: [
            2: Color(okL: 0.88, c: 0.06, h: 10),
            4: Color(okL: 0.84, c: 0.09, h: 25),
            8: Color(okL: 0.85, c: 0.10, h: 60),
            16: Color(okL: 0.87, c: 0.10, h: 95),
            32: Color(okL: 0.82, c: 0.12, h: 130),
            64: Color(okL: 0.76, c: 0.12, h: 165),
            128: Color(okL: 0.72, c: 0.11, h: 210),
            256: Color(okL: 0.60, c: 0.13, h: 255),
            512: Color(okL: 0.55, c: 0.16, h: 290),
            1024: Color(okL: 0.52, c: 0.18, h: 320),
            2048: Color(okL: 0.55, c: 0.19, h: 345),
            4096: Color(okL: 0.60, c: 0.18, h: 5),
            8192: Color(okL: 0.66, c: 0.15, h: 30),
            16384: Color(okL: 0.73, c: 0.12, h: 55),
            32768: Color(okL: 0.80, c: 0.10, h: 80),
            65536: Color(okL: 0.86, c: 0.08, h: 100),
        ],
        darkInkMax: 128,
        tileDarkInk: Color(okL: 0.24, c: 0.05, h: 330),
        tileLightInk: Color(okL: 0.97, c: 0.005, h: 340)
    )
}

// MARK: - Theme facade

/// Design tokens, backed by the active palette. Statics keep every call site
/// (`Theme.bgDeep`…) unchanged; palette switches happen only on the Settings
/// screen, so no game screen is ever live across a switch (v1.3 spec §4.2).
@MainActor
enum Theme {
    static var current: ThemePalette = .ember

    static let palettes: [ThemePalette] = [.ember, .tidepool, .meadow, .aurora, .sorbet]

    static func palette(id: String) -> ThemePalette {
        palettes.first { $0.id == id } ?? .ember
    }

    // MARK: Chrome

    static var bgDeep: Color { current.bgDeep }
    static var bgBoard: Color { current.bgBoard }
    static var cellWell: Color { current.cellWell }
    static var textPrimary: Color { current.textPrimary }
    static var textSecondary: Color { current.textSecondary }
    static var accent: Color { current.accent }
    /// Boulder frost — cool counterpoint, used only on ice.
    static var frost: Color { current.frost }

    // MARK: Tiles

    static func tileColor(for value: Int) -> Color {
        current.tileColors[value] ?? current.tileColors[65536]!
    }

    /// Light values sit on light tiles and need dark ink; saturated tiles
    /// take near-white. Both pairings meet contrast per palette.
    static func tileTextColor(for value: Int) -> Color {
        value <= current.darkInkMax ? current.tileDarkInk : current.tileLightInk
    }

    // MARK: Math Pop tiles

    /// Cuisenaire rod colors — the classroom standard for number learning —
    /// adjusted for on-screen contrast. Identical across palettes so a digit
    /// always keeps its color. Values ≥ 10 exist only as the popping target
    /// tile, which flashes gold.
    private static let mathTileColors: [Int: Color] = [
        1: Color(okL: 0.93, c: 0.04, h: 95),    // white rod → warm cream
        2: Color(okL: 0.62, c: 0.20, h: 25),    // red
        3: Color(okL: 0.80, c: 0.14, h: 135),   // light green
        4: Color(okL: 0.65, c: 0.14, h: 300),   // purple
        5: Color(okL: 0.86, c: 0.12, h: 95),    // yellow
        6: Color(okL: 0.55, c: 0.13, h: 150),   // dark green
        7: Color(okL: 0.45, c: 0.02, h: 260),   // black rod → graphite
        8: Color(okL: 0.58, c: 0.13, h: 55),    // brown → terracotta
        9: Color(okL: 0.58, c: 0.15, h: 250),   // blue
    ]
    private static let mathTargetFlash = Color(okL: 0.82, c: 0.14, h: 85)

    static func mathTileColor(for value: Int) -> Color {
        mathTileColors[value] ?? mathTargetFlash
    }

    static func mathTileTextColor(for value: Int) -> Color {
        switch value {
        case 1, 3, 5: Color(okL: 0.22, c: 0.03, h: 95)
        case ..<10: Color(okL: 0.97, c: 0.005, h: 95)
        default: Color(okL: 0.22, c: 0.03, h: 95)   // gold flash takes dark ink
        }
    }

    // MARK: Typography

    /// Display face: Unbounded (OFL). Falls back to system if unavailable.
    static func display(_ size: CGFloat, weight: Font.Weight = .bold) -> Font {
        .custom("Unbounded", size: size).weight(weight)
    }

    /// Tile numerals stay with SF Rounded for tight legibility at small sizes.
    static func tileNumeral(_ size: CGFloat) -> Font {
        .system(size: size, weight: .heavy, design: .rounded)
    }
}
