import SwiftUI

/// A short one-shot spark burst for cascade merges, drawn with Canvas so it
/// costs one view. Skipped entirely under Reduce Motion (the parent only adds
/// bursts during full animation runs).
struct ParticleBurstView: View {
    let round: Int
    /// Milestone celebrations get a bigger, longer volley.
    var milestone = false
    @State private var startDate = Date()

    private var sparkCount: Int { milestone ? 46 : min(10 + round * 6, 28) }
    private var lifetime: TimeInterval { milestone ? 0.9 : 0.5 }

    var body: some View {
        TimelineView(.animation) { context in
            Canvas { canvas, size in
                let elapsed = context.date.timeIntervalSince(startDate)
                guard elapsed < lifetime else { return }
                let progress = elapsed / lifetime
                let center = CGPoint(x: size.width / 2, y: size.height / 2)
                let maxRadius = min(size.width, size.height) / 2

                for spark in 0..<sparkCount {
                    // Deterministic pseudo-random angle/speed per spark index.
                    let seed = Double(spark) * 2.399963 + Double(round)
                    let angle = seed.truncatingRemainder(dividingBy: .pi * 2)
                    let speed = 0.55 + (seed * 7.31).truncatingRemainder(dividingBy: 0.45)
                    let radius = maxRadius * progress * speed
                    let position = CGPoint(
                        x: center.x + cos(angle) * radius,
                        y: center.y + sin(angle) * radius
                    )
                    let sparkSize = 5.0 * (1 - progress)
                    let rect = CGRect(
                        x: position.x - sparkSize / 2, y: position.y - sparkSize / 2,
                        width: sparkSize, height: sparkSize
                    )
                    canvas.opacity = 1 - progress
                    canvas.fill(Circle().path(in: rect), with: .color(Theme.accent))
                }
            }
        }
        .onAppear { startDate = Date() }
    }
}
