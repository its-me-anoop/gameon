import CoreHaptics
import UIKit

/// CoreHaptics wrapper. Everything degrades to a no-op on hardware without
/// haptics, and every call is safe to make from anywhere.
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

    /// Machine set down: a firm, short knock.
    func place() {
        transient(intensity: 0.7, sharpness: 0.55)
    }

    func tap() {
        transient(intensity: 0.3, sharpness: 0.35)
    }

    /// Meteor caught — the most physical moment in the game.
    func impact() {
        transient(intensity: 1.0, sharpness: 0.8)
    }

    /// Meteor entering the atmosphere: a rising rumble.
    func approach() {
        continuousRumble(duration: 0.6, intensity: 0.35, sharpness: 0.15)
    }

    func quake() {
        continuousRumble(duration: 0.9, intensity: 0.75, sharpness: 0.1)
    }

    /// The world imploding, and then the new one arriving.
    func collapse() {
        guard isEnabled, let engine else { return }
        let events: [CHHapticEvent] = [
            CHHapticEvent(eventType: .hapticContinuous, parameters: [
                CHHapticEventParameter(parameterID: .hapticIntensity, value: 0.5),
                CHHapticEventParameter(parameterID: .hapticSharpness, value: 0.1),
            ], relativeTime: 0, duration: 0.8),
            CHHapticEvent(eventType: .hapticTransient, parameters: [
                CHHapticEventParameter(parameterID: .hapticIntensity, value: 1.0),
                CHHapticEventParameter(parameterID: .hapticSharpness, value: 0.6),
            ], relativeTime: 0.85),
        ]
        try? engine.makePlayer(with: CHHapticPattern(events: events, parameters: []))
            .start(atTime: CHHapticTimeImmediate)
    }

    private func transient(intensity: Float, sharpness: Float) {
        play(events: [
            CHHapticEvent(eventType: .hapticTransient, parameters: [
                CHHapticEventParameter(parameterID: .hapticIntensity, value: intensity),
                CHHapticEventParameter(parameterID: .hapticSharpness, value: sharpness),
            ], relativeTime: 0),
        ])
    }

    private func continuousRumble(duration: TimeInterval, intensity: Float, sharpness: Float) {
        play(events: [
            CHHapticEvent(eventType: .hapticContinuous, parameters: [
                CHHapticEventParameter(parameterID: .hapticIntensity, value: intensity),
                CHHapticEventParameter(parameterID: .hapticSharpness, value: sharpness),
            ], relativeTime: 0, duration: duration),
        ])
    }

    private func play(events: [CHHapticEvent]) {
        guard isEnabled, let engine else { return }
        guard let pattern = try? CHHapticPattern(events: events, parameters: []) else { return }
        try? engine.makePlayer(with: pattern).start(atTime: CHHapticTimeImmediate)
    }
}
