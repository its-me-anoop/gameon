#!/usr/bin/env swift
// Generates Gravitile's sound set as 16-bit mono WAV files.
// Usage: swift Tools/gensounds.swift Gravitile/Resources/Sounds
//
// Pure synthesis — sines, filtered noise and exponential envelopes. No source
// samples, so every asset in the app is license-clean and regenerable from
// this file. That is also the honest answer when a reviewer asks where the
// audio came from.

import Foundation

let sampleRate = 44_100.0

// MARK: - Primitives

var rngState: UInt64 = 0x9E37_79B9_7F4A_7C15
func whiteNoise() -> Double {
    rngState ^= rngState << 13
    rngState ^= rngState >> 7
    rngState ^= rngState << 17
    return Double(Int64(bitPattern: rngState)) / Double(Int64.max)
}

/// One tone with an exponential decay and an optional partial.
func tone(
    frequency: Double, duration: Double, attack: Double = 0.002,
    decay: Double = 14, partial: Double = 2.0, partialGain: Double = 0.3,
    detune: Double = 0, gain: Double = 0.6
) -> [Double] {
    let count = Int(duration * sampleRate)
    return (0..<count).map { index in
        let t = Double(index) / sampleRate
        let envelope = exp(-t * decay) * min(1, t / max(attack, 1e-4))
        var sample = sin(2 * .pi * frequency * t)
        if detune > 0 { sample = (sample + sin(2 * .pi * frequency * (1 + detune) * t)) / 2 }
        sample += partialGain * sin(2 * .pi * frequency * partial * t)
        return sample * envelope * gain
    }
}

/// A tone whose pitch glides — the backbone of the meteor and the collapse.
func sweep(
    from startFrequency: Double, to endFrequency: Double, duration: Double,
    curve: Double = 1, attack: Double = 0.01, release: Double = 0.25, gain: Double = 0.6
) -> [Double] {
    let count = Int(duration * sampleRate)
    var phase = 0.0
    return (0..<count).map { index in
        let t = Double(index) / sampleRate
        let progress = pow(t / duration, curve)
        let frequency = startFrequency + (endFrequency - startFrequency) * progress
        phase += 2 * .pi * frequency / sampleRate
        let attackEnvelope = min(1, t / max(attack, 1e-4))
        let releaseEnvelope = min(1, (duration - t) / max(release, 1e-4))
        return sin(phase) * attackEnvelope * releaseEnvelope * gain
    }
}

/// White noise through a one-pole lowpass whose cutoff moves over time.
func filteredNoise(
    duration: Double, startCutoff: Double, endCutoff: Double,
    attack: Double = 0.01, decay: Double = 4, gain: Double = 0.5
) -> [Double] {
    let count = Int(duration * sampleRate)
    var previous = 0.0
    return (0..<count).map { index in
        let t = Double(index) / sampleRate
        let progress = t / duration
        let cutoff = startCutoff + (endCutoff - startCutoff) * progress
        let alpha = min(1, 2 * .pi * cutoff / sampleRate)
        previous += alpha * (whiteNoise() - previous)
        let envelope = exp(-t * decay) * min(1, t / max(attack, 1e-4))
        return previous * envelope * gain
    }
}

func mix(_ tracks: [(offset: Double, samples: [Double])]) -> [Double] {
    let total = tracks.map { Int($0.offset * sampleRate) + $0.samples.count }.max() ?? 0
    var output = [Double](repeating: 0, count: total)
    for track in tracks {
        let start = Int(track.offset * sampleRate)
        for (index, sample) in track.samples.enumerated() {
            output[start + index] += sample
        }
    }
    return output
}

/// Normalizes to a headroom-safe peak so no effect clips against another.
func render(_ samples: [Double], peak: Double = 0.86) -> [Int16] {
    let maximum = samples.map(abs).max() ?? 1
    guard maximum > 1e-9 else { return samples.map { _ in 0 } }
    let scale = peak / maximum
    return samples.map { Int16(max(-1, min(1, $0 * scale)) * 32_000) }
}

func writeWAV(_ samples: [Int16], to url: URL) throws {
    var data = Data()
    func append(_ value: UInt32) {
        withUnsafeBytes(of: value.littleEndian) { data.append(contentsOf: $0) }
    }
    func append16(_ value: UInt16) {
        withUnsafeBytes(of: value.littleEndian) { data.append(contentsOf: $0) }
    }
    let byteCount = UInt32(samples.count * 2)
    data.append("RIFF".data(using: .ascii)!); append(36 + byteCount)
    data.append("WAVE".data(using: .ascii)!)
    data.append("fmt ".data(using: .ascii)!); append(16); append16(1); append16(1)
    append(UInt32(sampleRate)); append(UInt32(sampleRate * 2)); append16(2); append16(16)
    data.append("data".data(using: .ascii)!); append(byteCount)
    samples.withUnsafeBytes { data.append(contentsOf: $0) }
    try data.write(to: url)
}

// MARK: - The set
//
// Everything is tuned around D minor pentatonic (D F G A C) so effects that
// land on top of each other, and on top of the ambient bed, stay consonant.

let D3 = 146.83, F3 = 174.61, G3 = 196.00, A3 = 220.00, C4 = 261.63
let D4 = 293.66, F4 = 349.23, A4 = 440.00, D5 = 587.33, A5 = 880.00

/// A machine set down: a wooden knock with a low body.
let place = mix([
    (0, filteredNoise(duration: 0.09, startCutoff: 2600, endCutoff: 500, decay: 42, gain: 0.5)),
    (0, tone(frequency: D3, duration: 0.22, decay: 26, partial: 2, partialGain: 0.22, gain: 0.55)),
    (0.004, tone(frequency: A3, duration: 0.14, decay: 34, partialGain: 0.1, gain: 0.28)),
])

/// Upgrade: two notes up the scale, the second brighter.
let upgrade = mix([
    (0, tone(frequency: A3, duration: 0.2, decay: 17, gain: 0.42)),
    (0.075, tone(frequency: D4, duration: 0.3, decay: 12, partial: 3, partialGain: 0.24, gain: 0.5)),
    (0.075, tone(frequency: A4, duration: 0.26, decay: 15, partialGain: 0.12, gain: 0.22)),
])

/// Demolish: the same shape, backwards and duller.
let demolish = mix([
    (0, filteredNoise(duration: 0.3, startCutoff: 1800, endCutoff: 260, decay: 11, gain: 0.55)),
    (0.02, tone(frequency: F3, duration: 0.24, decay: 16, gain: 0.3)),
    (0.09, tone(frequency: D3, duration: 0.3, decay: 13, gain: 0.34)),
])

/// A tick, barely there. It plays constantly, so it has to stay out of the way.
let tap = mix([
    (0, tone(frequency: D5, duration: 0.06, attack: 0.001, decay: 70, partialGain: 0.05, gain: 0.3)),
])

/// Denied: a short, flat, low buzz. Unpleasant on purpose, but not harsh.
let denied = mix([
    (0, tone(frequency: 116, duration: 0.14, decay: 24, partial: 1.5, partialGain: 0.5, gain: 0.45)),
    (0.055, tone(frequency: 104, duration: 0.16, decay: 22, partial: 1.5, partialGain: 0.5, gain: 0.4)),
])

/// Repair: a bright, clean ping that says "fixed".
let repair = mix([
    (0, tone(frequency: A4, duration: 0.26, decay: 13, partial: 3, partialGain: 0.2, gain: 0.4)),
    (0.045, tone(frequency: D5, duration: 0.3, decay: 11, partialGain: 0.15, gain: 0.34)),
])

/// A meteor on approach: a long falling whistle under a rising hiss.
let meteor = mix([
    (0, sweep(from: 1650, to: 340, duration: 1.5, curve: 1.7, attack: 0.18, release: 0.5, gain: 0.3)),
    (0, filteredNoise(duration: 1.5, startCutoff: 900, endCutoff: 4200, attack: 0.5, decay: 0.6, gain: 0.28)),
])

/// Caught: impact, then a scatter of ore.
let catchSound = mix([
    (0, filteredNoise(duration: 0.22, startCutoff: 5200, endCutoff: 700, decay: 26, gain: 0.7)),
    (0, tone(frequency: D3, duration: 0.34, decay: 15, partial: 1.5, partialGain: 0.3, gain: 0.6)),
    (0.05, tone(frequency: D5, duration: 0.24, decay: 18, gain: 0.22)),
    (0.1, tone(frequency: A5, duration: 0.2, decay: 20, gain: 0.16)),
    (0.16, tone(frequency: D5 * 1.5, duration: 0.18, decay: 22, gain: 0.12)),
])

/// A quake: low rumble, no pitch to speak of.
let quake = mix([
    (0, filteredNoise(duration: 1.1, startCutoff: 240, endCutoff: 90, attack: 0.08, decay: 3.4, gain: 0.9)),
    (0.03, tone(frequency: 58, duration: 0.9, attack: 0.05, decay: 4.5, partial: 1.5, partialGain: 0.4, gain: 0.5)),
])

/// The collapse: everything falls inward, and the new core lands.
let collapse = mix([
    (0, sweep(from: 780, to: 62, duration: 1.9, curve: 2.2, attack: 0.25, release: 0.35, gain: 0.5)),
    (0, filteredNoise(duration: 1.9, startCutoff: 3200, endCutoff: 160, attack: 0.4, decay: 1.1, gain: 0.4)),
    (1.86, tone(frequency: 47, duration: 1.5, attack: 0.004, decay: 3.2, partial: 2, partialGain: 0.3, gain: 1.0)),
    (1.9, tone(frequency: D4, duration: 1.3, attack: 0.02, decay: 3.6, partial: 1.5, partialGain: 0.3, gain: 0.3)),
    (2.02, tone(frequency: A4, duration: 1.2, attack: 0.03, decay: 3.4, gain: 0.2)),
])

/// The ambient bed: slow detuned pads on the pentatonic, arranged so the loop
/// point falls where every voice is near silence.
func ambientBed(duration: Double) -> [Double] {
    let count = Int(duration * sampleRate)
    let voices: [(frequency: Double, period: Double, phase: Double, gain: Double)] = [
        (D3 / 2, 21.0, 0.00, 0.55),
        (A3 / 2, 27.0, 0.35, 0.34),
        (F3, 33.0, 0.62, 0.22),
        (C4, 39.0, 0.18, 0.15),
        (D4, 45.0, 0.80, 0.10),
    ]
    var samples = [Double](repeating: 0, count: count)
    for voice in voices {
        var phase = 0.0
        var detunedPhase = 0.0
        for index in 0..<count {
            let t = Double(index) / sampleRate
            // Each voice swells on its own period; the periods are coprime-ish
            // so the texture never repeats inside the loop.
            let swell = pow(max(0, sin(2 * .pi * (t / voice.period + voice.phase))), 2.4)
            phase += 2 * .pi * voice.frequency / sampleRate
            detunedPhase += 2 * .pi * voice.frequency * 1.004 / sampleRate
            samples[index] += (sin(phase) + 0.6 * sin(detunedPhase)) * swell * voice.gain
        }
    }
    // Fade the seam so the loop is inaudible.
    let fade = Int(2.5 * sampleRate)
    for index in 0..<fade {
        let gain = Double(index) / Double(fade)
        samples[index] *= gain
        samples[count - 1 - index] *= gain
    }
    return samples
}

// MARK: - Write

let arguments = CommandLine.arguments
let directory = URL(
    fileURLWithPath: arguments.count > 1 ? arguments[1] : "Gravitile/Resources/Sounds"
)
try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)

let set: [(String, [Double])] = [
    ("place", place),
    ("upgrade", upgrade),
    ("demolish", demolish),
    ("tap", tap),
    ("denied", denied),
    ("repair", repair),
    ("meteor", meteor),
    ("catch", catchSound),
    ("quake", quake),
    ("collapse", collapse),
    ("drift", ambientBed(duration: 64)),
]

/// The ambient bed is a minute of audio; as 16-bit PCM that is megabytes of
/// app for something the ear cannot tell from 64 kbps AAC. Everything else is
/// under a second and stays uncompressed.
func compress(_ wav: URL, to m4a: URL) -> Bool {
    let process = Process()
    process.executableURL = URL(fileURLWithPath: "/usr/bin/afconvert")
    process.arguments = ["-f", "m4af", "-d", "aac", "-b", "64000", wav.path, m4a.path]
    try? process.run()
    process.waitUntilExit()
    return process.terminationStatus == 0
}

for (name, samples) in set {
    let peak = name == "drift" ? 0.5 : (name == "tap" ? 0.4 : 0.86)
    let wav = directory.appendingPathComponent("\(name).wav")
    try writeWAV(render(samples, peak: peak), to: wav)
    let seconds = String(format: "%.1f", Double(samples.count) / sampleRate)

    if name == "drift" {
        let m4a = directory.appendingPathComponent("\(name).m4a")
        if compress(wav, to: m4a) {
            try? FileManager.default.removeItem(at: wav)
            let size = (try? FileManager.default.attributesOfItem(atPath: m4a.path)[.size] as? Int) ?? 0
            print("wrote \(name).m4a  (\(seconds)s, \((size ?? 0) / 1024) KB)")
            continue
        }
        print("afconvert unavailable — left \(name).wav uncompressed")
    }
    print("wrote \(name).wav  (\(seconds)s)")
}
