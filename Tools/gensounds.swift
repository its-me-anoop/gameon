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

print("Wrote sounds to \(outDir.path)")
