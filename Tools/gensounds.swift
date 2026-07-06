#!/usr/bin/env swift
// Generates Gravitile's sound effects as 16-bit mono WAV files.
// Usage: swift Tools/gensounds.swift Gravitile/Resources/Sounds
// Pure synthesis (sine + partials with exponential decay) — no source samples,
// so assets are license-clean and regenerable.

import Foundation

let sampleRate = 44_100.0

func synth(frequency: Double, duration: Double, partial: Double = 2.0, noise: Double = 0.0) -> [Int16] {
    let count = Int(duration * sampleRate)
    var rngState: UInt64 = 0x9E3779B97F4A7C15
    func whiteNoise() -> Double {
        rngState ^= rngState << 13; rngState ^= rngState >> 7; rngState ^= rngState << 17
        return Double(Int64(bitPattern: rngState)) / Double(Int64.max)
    }
    return (0..<count).map { i in
        let t = Double(i) / sampleRate
        let envelope = exp(-t * 14) * (1 - exp(-t * 900)) // fast attack, exp decay
        let fundamental = sin(2 * .pi * frequency * t)
        let overtone = 0.35 * sin(2 * .pi * frequency * partial * t)
        let hiss = noise * whiteNoise()
        let sample = (fundamental + overtone + hiss) * envelope * 0.6
        return Int16(max(-1, min(1, sample)) * 32_000)
    }
}

func mix(_ tracks: [(offset: Double, samples: [Int16])]) -> [Int16] {
    let total = tracks.map { Int($0.offset * sampleRate) + $0.samples.count }.max() ?? 0
    var out = [Double](repeating: 0, count: total)
    for track in tracks {
        let start = Int(track.offset * sampleRate)
        for (i, sample) in track.samples.enumerated() {
            out[start + i] += Double(sample)
        }
    }
    return out.map { Int16(max(-32_000, min(32_000, $0))) }
}

func writeWAV(_ samples: [Int16], to url: URL) throws {
    var data = Data()
    func append(_ value: UInt32) { withUnsafeBytes(of: value.littleEndian) { data.append(contentsOf: $0) } }
    func append16(_ value: UInt16) { withUnsafeBytes(of: value.littleEndian) { data.append(contentsOf: $0) } }
    let byteCount = UInt32(samples.count * 2)
    data.append("RIFF".data(using: .ascii)!); append(36 + byteCount)
    data.append("WAVE".data(using: .ascii)!)
    data.append("fmt ".data(using: .ascii)!); append(16); append16(1); append16(1)
    append(UInt32(sampleRate)); append(UInt32(sampleRate * 2)); append16(2); append16(16)
    data.append("data".data(using: .ascii)!); append(byteCount)
    samples.withUnsafeBytes { data.append(contentsOf: $0) }
    try data.write(to: url)
}

/// Tone with controllable envelope — the fixed-envelope `synth` stays
/// untouched so the original effect files regenerate byte-identically.
func tone(
    frequency: Double, duration: Double, attack: Double = 0.002,
    decayRate: Double = 14, partial: Double = 2.0, partialGain: Double = 0.35,
    detune: Double = 0, gain: Double = 0.6
) -> [Int16] {
    let count = Int(duration * sampleRate)
    return (0..<count).map { i in
        let t = Double(i) / sampleRate
        let envelope = exp(-t * decayRate) * min(1, t / max(attack, 1e-4))
        var sample = sin(2 * .pi * frequency * t)
        if detune > 0 { sample = (sample + sin(2 * .pi * frequency * (1 + detune) * t)) / 2 }
        sample += partialGain * sin(2 * .pi * frequency * partial * t)
        return Int16(max(-1, min(1, sample * envelope * gain)) * 32_000)
    }
}

/// Band-ish noise sweep for the gravity-rotation whoosh: white noise through a
/// crude one-pole lowpass whose cutoff falls with an amplitude swell.
func noiseSweep(duration: Double, gain: Double = 0.5) -> [Int16] {
    let count = Int(duration * sampleRate)
    var rngState: UInt64 = 0x2545F4914F6CDD1D
    var lowpassState = 0.0
    return (0..<count).map { i in
        let t = Double(i) / sampleRate
        let progress = t / duration
        rngState ^= rngState << 13; rngState ^= rngState >> 7; rngState ^= rngState << 17
        let noise = Double(Int64(bitPattern: rngState)) / Double(Int64.max)
        // Cutoff sweeps 0.35 → 0.04; swell peaks a third of the way through.
        let alpha = 0.35 - 0.31 * progress
        lowpassState += alpha * (noise - lowpassState)
        let swell = sin(.pi * min(1, progress * 1.15))
        return Int16(max(-1, min(1, lowpassState * swell * gain * 2.2)) * 32_000)
    }
}

/// 48-second seamless generative ambient loop. Four slow pad chords
/// (Am–F–C–G, low register, detuned sine pairs) that each swell in and out —
/// the loop point lands in a trough so it wraps cleanly. A sparse pentatonic
/// bell keeps it from feeling static. Deliberately quiet: the app plays it at
/// low volume under gameplay.
func ambientLoop() -> [Int16] {
    let chordSeconds = 6.0
    let chords: [[Double]] = [
        [110.00, 130.81, 164.81],  // Am: A2 C3 E3
        [87.31, 110.00, 130.81],   // F:  F2 A2 C3
        [130.81, 164.81, 196.00],  // C:  C3 E3 G3
        [98.00, 123.47, 146.83],   // G:  G2 B2 D3
    ]
    let sequence = chords + chords // 8 bars, 48 s
    let total = Int(Double(sequence.count) * chordSeconds * sampleRate)
    var mixBuffer = [Double](repeating: 0, count: total)

    for (index, chord) in sequence.enumerated() {
        let start = Int(Double(index) * chordSeconds * sampleRate)
        let count = Int(chordSeconds * sampleRate)
        for (noteIndex, frequency) in chord.enumerated() {
            let level = 0.16 / Double(chord.count) * (noteIndex == 0 ? 1.3 : 1.0)
            for i in 0..<count {
                let t = Double(i) / sampleRate
                // Full swell within the chord's window: silent → peak → silent.
                let envelope = pow(sin(.pi * t / chordSeconds), 1.6)
                let a = sin(2 * .pi * frequency * t)
                let b = sin(2 * .pi * frequency * 1.004 * t)  // slow beating
                let shimmer = 0.18 * sin(2 * .pi * frequency * 2.0 * t)
                mixBuffer[start + i] += (a + b + shimmer) / 2.2 * envelope * level
            }
        }
    }

    // Sparse bell: pentatonic A C D E G, one gentle strike every 3 s on a
    // deterministic pattern, decayed fully before the loop point.
    let bellNotes: [Double] = [440.0, 523.25, 587.33, 659.25, 783.99]
    let pattern = [0, 2, 4, 1, 3, 0, 4, 2, 1, 4, 0, 3, 2, 0, 1]
    for (strike, noteIndex) in pattern.enumerated() {
        let start = Int((Double(strike) * 3.0 + 1.2) * sampleRate)
        let frequency = bellNotes[noteIndex]
        let count = Int(1.6 * sampleRate)
        guard start + count < total else { continue }
        for i in 0..<count {
            let t = Double(i) / sampleRate
            let envelope = exp(-t * 3.4) * min(1, t / 0.004)
            let body = sin(2 * .pi * frequency * t) + 0.3 * sin(2 * .pi * frequency * 2.01 * t)
            mixBuffer[start + i] += body * envelope * 0.045
        }
    }

    return mixBuffer.map { Int16(max(-0.95, min(0.95, $0)) * 32_000) }
}

let outDir = URL(fileURLWithPath: CommandLine.arguments.count > 1 ? CommandLine.arguments[1] : "Sounds")
try FileManager.default.createDirectory(at: outDir, withIntermediateDirectories: true)

// Merge blips: pentatonic steps rising with cascade round (C5 E5 G5 A5 C6).
let mergePitches: [Double] = [523.25, 659.25, 783.99, 880.0, 1046.5]
for (index, pitch) in mergePitches.enumerated() {
    try writeWAV(synth(frequency: pitch, duration: 0.14), to: outDir.appendingPathComponent("merge\(index + 1).wav"))
}
// Slide tick — short, soft, noisy.
try writeWAV(synth(frequency: 220, duration: 0.05, partial: 1.5, noise: 0.15), to: outDir.appendingPathComponent("slide.wav"))
// Game over — descending minor phrase.
try writeWAV(mix([
    (0.00, synth(frequency: 392.0, duration: 0.25)),
    (0.16, synth(frequency: 311.1, duration: 0.25)),
    (0.32, synth(frequency: 261.6, duration: 0.45)),
]), to: outDir.appendingPathComponent("gameover.wav"))
// Daily fanfare — rising major phrase.
try writeWAV(mix([
    (0.00, synth(frequency: 523.25, duration: 0.2)),
    (0.12, synth(frequency: 659.25, duration: 0.2)),
    (0.24, synth(frequency: 784.0, duration: 0.35)),
]), to: outDir.appendingPathComponent("fanfare.wav"))

// Gravity-rotation whoosh — filtered noise sweep with a faint falling tone.
try writeWAV(mix([
    (0.00, noiseSweep(duration: 0.22, gain: 0.42)),
    (0.02, tone(frequency: 480, duration: 0.18, decayRate: 18, partial: 0.5, partialGain: 0.2, gain: 0.12)),
]), to: outDir.appendingPathComponent("whoosh.wav"))
// Tile landing — soft low thock.
try writeWAV(tone(frequency: 92, duration: 0.09, decayRate: 42, partial: 2.4, partialGain: 0.15, gain: 0.55),
             to: outDir.appendingPathComponent("land.wav"))
// UI tap — tiny tick.
try writeWAV(tone(frequency: 1000, duration: 0.035, decayRate: 70, partialGain: 0.1, gain: 0.3),
             to: outDir.appendingPathComponent("tap.wav"))
// Milestone chime — two shimmering notes for a first-of-the-game big tile.
try writeWAV(mix([
    (0.00, tone(frequency: 1046.5, duration: 0.55, decayRate: 6, partial: 2.01, partialGain: 0.3, detune: 0.003, gain: 0.5)),
    (0.09, tone(frequency: 1318.5, duration: 0.6, decayRate: 5, partial: 2.01, partialGain: 0.3, detune: 0.003, gain: 0.5)),
]), to: outDir.appendingPathComponent("milestone.wav"))
// New best — quick rising sting.
try writeWAV(mix([
    (0.00, tone(frequency: 880.0, duration: 0.22, decayRate: 12, gain: 0.45)),
    (0.08, tone(frequency: 1108.7, duration: 0.22, decayRate: 12, gain: 0.45)),
    (0.16, tone(frequency: 1318.5, duration: 0.4, decayRate: 7, detune: 0.003, gain: 0.5)),
]), to: outDir.appendingPathComponent("newbest.wav"))
// Ice chip — bright glassy tick.
try writeWAV(mix([
    (0.00, tone(frequency: 2200, duration: 0.06, decayRate: 55, partial: 1.5, partialGain: 0.25, gain: 0.4)),
    (0.00, noiseSweep(duration: 0.05, gain: 0.10)),
]), to: outDir.appendingPathComponent("chip.wav"))
// Ice shatter — glassy burst falling away.
try writeWAV(mix([
    (0.00, noiseSweep(duration: 0.16, gain: 0.30)),
    (0.00, tone(frequency: 1900, duration: 0.12, decayRate: 26, partial: 2.7, partialGain: 0.35, detune: 0.006, gain: 0.4)),
    (0.05, tone(frequency: 1250, duration: 0.16, decayRate: 20, partial: 2.3, partialGain: 0.3, gain: 0.3)),
]), to: outDir.appendingPathComponent("shatter.wav"))
// Ambient loop under gameplay.
try writeWAV(ambientLoop(), to: outDir.appendingPathComponent("bgm.wav"))

print("Wrote sounds to \(outDir.path)")
