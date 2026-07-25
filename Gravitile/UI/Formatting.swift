import Foundation

/// Compact numbers for a game whose quantities span six orders of magnitude.
/// Small values keep a decimal so early progress is visible; large ones don't,
/// because nobody reads the tenths of a million.
enum Format {
    static func quantity(_ value: Double) -> String {
        let magnitude = abs(value)
        return switch magnitude {
        case ..<10: String(format: "%.1f", value)
        case ..<1_000: String(Int(value.rounded()))
        case ..<1_000_000: String(format: "%.1fK", value / 1_000)
        case ..<1_000_000_000: String(format: "%.2fM", value / 1_000_000)
        default: String(format: "%.2fB", value / 1_000_000_000)
        }
    }

    /// Signed per-second rate, e.g. "+2.4/s".
    static func rate(_ value: Double) -> String {
        guard abs(value) >= 0.05 else { return "—" }
        let sign = value > 0 ? "+" : "−"
        return "\(sign)\(quantity(abs(value)))/s"
    }

    static func duration(_ seconds: Double) -> String {
        let total = Int(seconds.rounded())
        let hours = total / 3600
        let minutes = (total % 3600) / 60
        if hours > 0 { return minutes > 0 ? "\(hours)h \(minutes)m" : "\(hours)h" }
        if minutes > 0 { return "\(minutes)m" }
        return "\(total)s"
    }

    static func degrees(_ value: Double) -> String {
        "\(Int(value.rounded()))°"
    }

    static func temperature(_ celsius: Double) -> String {
        "\(Int(celsius.rounded()))°C"
    }
}
