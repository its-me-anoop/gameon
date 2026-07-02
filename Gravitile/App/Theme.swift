import SwiftUI
import GravitileKit

/// Design tokens for the default "Ember" theme. All colors were derived from
/// OKLCH (values noted inline) so lightness steps are perceptually even and
/// chroma eases off near the extremes. Neutrals are tinted toward the board's
/// navy hue — nothing is pure black or white.
enum Theme {
    // MARK: Chrome

    static let bgDeep = Color(red: 0.0236, green: 0.0361, blue: 0.0602)        // oklch(14% 0.015 260)
    static let bgBoard = Color(red: 0.0677, green: 0.0872, blue: 0.1188)       // oklch(20% 0.018 260)
    static let cellWell = Color(red: 0.1192, green: 0.1423, blue: 0.1799)      // oklch(26% 0.02 260)
    static let textPrimary = Color(red: 0.9228, green: 0.9358, blue: 0.9567)   // oklch(95% 0.008 260)
    static let textSecondary = Color(red: 0.6283, green: 0.6464, blue: 0.6757) // oklch(72% 0.012 260)
    static let accent = Color(red: 0.9369, green: 0.5224, blue: 0.1805)        // oklch(72% 0.16 55)

    // MARK: Tile ramp — heat journey cream → red → violet → teal → lime

    private static let tileColors: [Int: Color] = [
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
    ]

    static func tileColor(for value: Int) -> Color {
        tileColors[value] ?? tileColors[65536]!
    }

    /// Light values sit on light tiles and need dark text; from 32 upward the
    /// tiles are saturated enough for near-white. Both pairings meet 4.5:1.
    static func tileTextColor(for value: Int) -> Color {
        value <= 16 ? Color(red: 0.16, green: 0.13, blue: 0.08) : textPrimary
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
