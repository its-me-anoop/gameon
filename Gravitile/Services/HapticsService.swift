import CoreHaptics
import UIKit

/// CoreHaptics wrapper. Merge taps sharpen and intensify with cascade round;
/// everything degrades to a no-op on unsupported hardware.
@MainActor
final class HapticsService {
    private var engine: CHHapticEngine?
    var isEnabled = true

    init() {
        guard CHHapticEngine.capabilitiesForHardware().supportsHaptics else { return }
        engine = try? CHHapticEngine()
        engine?.resetHandler = { [weak self] in
            Task { @MainActor in try? self?.engine?.start() }
        }
        try? engine?.start()
    }

    func merge(round: Int) {
        guard isEnabled else { return }
        let intensity = min(1.0, 0.45 + Double(round) * 0.18)
        let sharpness = min(1.0, 0.3 + Double(round) * 0.2)
        play(events: [
            CHHapticEvent(eventType: .hapticTransient, parameters: [
                CHHapticEventParameter(parameterID: .hapticIntensity, value: Float(intensity)),
                CHHapticEventParameter(parameterID: .hapticSharpness, value: Float(sharpness)),
            ], relativeTime: 0),
        ])
    }

    func gameOver() {
        guard isEnabled else { return }
        play(events: (0..<3).map { index in
            CHHapticEvent(eventType: .hapticTransient, parameters: [
                CHHapticEventParameter(parameterID: .hapticIntensity, value: 0.8 - Float(index) * 0.2),
                CHHapticEventParameter(parameterID: .hapticSharpness, value: 0.4),
            ], relativeTime: Double(index) * 0.12)
        })
    }

    private func play(events: [CHHapticEvent]) {
        guard let engine else { return }
        guard let pattern = try? CHHapticPattern(events: events, parameters: []) else { return }
        try? engine.makePlayer(with: pattern).start(atTime: CHHapticTimeImmediate)
    }
}
